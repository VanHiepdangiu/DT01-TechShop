// DT01 TechShop — Nguyễn Văn Hiệp — MSSV 2410900035 — K24CNT1
// Endpoint webhook mô phỏng ngân hàng báo có, kèm công cụ giả lập để trình diễn và kiểm thử.
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechShop.Data;
using TechShop.Models;
using TechShop.Services;

namespace TechShop.Controllers;

/// <summary>
/// Webhook nhận thông báo chuyển khoản. Đây là endpoint công khai (ngân hàng gọi vào),
/// nên bảo vệ bằng chữ ký HMAC-SHA256 trên toàn bộ dữ liệu giao dịch.
/// </summary>
[ApiController]
[Route("api/bank")]
public class BankWebhookController(BankWebhookService hook, ShopDbContext db,
                                   IConfiguration config, ILogger<BankWebhookController> logger) : ControllerBase
{
    /// <summary>Ngân hàng gọi vào khi có tiền chuyển đến tài khoản.</summary>
    [AllowAnonymous, HttpPost("webhook")]
    public async Task<IActionResult> Receive([FromBody] JsonElement body)
    {
        string signature = Request.Headers["X-Bank-Signature"].ToString();
        try
        {
            var tx = Read(body);
            if (!hook.VerifySignature(tx.TransactionId, tx.Content, tx.Amount,
                                      body.TryGetProperty("transactionAt", out var t) ? t.GetString() : null,
                                      signature))
            {
                await hook.LogRawAsync(body.GetRawText(), signature, "Chữ ký không khớp hoặc thiếu header.");
                return Unauthorized(new { status = "invalid_signature", message = "Chữ ký không hợp lệ." });
            }

            var result = await hook.ProcessAsync(tx);
            var code = result.Status switch
            {
                "matched" => 200,
                "duplicate" => 200,
                "conflict" => 409,
                "amount_mismatch" => 422,
                "unmatched" => 202,
                _ => 400
            };
            return StatusCode(code, new { status = result.Status, message = result.Message, orderId = result.OrderId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi xử lý webhook ngân hàng");
            return BadRequest(new { status = "error", message = "Dữ liệu không hợp lệ." });
        }
    }

    /// <summary>Kiểm tra webhook đã bật hay chưa, dùng cho trang chẩn đoán.</summary>
    [AllowAnonymous, HttpGet("status")]
    public IActionResult Status() => Ok(new
    {
        enabled = hook.Enabled,
        tolerance = hook.Tolerance,
        endpoint = "/api/bank/webhook"
    });

    /// <summary>
    /// Công cụ giả lập ngân hàng: sinh chữ ký và gọi chính webhook như ngân hàng thật.
    /// Chỉ bật khi cấu hình BankWebhook:AllowSimulator = true (mặc định chỉ dùng khi demo).
    /// </summary>
    [Authorize(Roles = "Admin"), HttpPost("simulate")]
    public async Task<IActionResult> Simulate(int orderId, decimal? amount, string? transactionId)
    {
        if (!(config.GetValue<bool?>("BankWebhook:AllowSimulator") ?? false))
            return BadRequest(new { message = "Trình giả lập đang tắt. Bật BankWebhook:AllowSimulator để dùng." });

        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId);
        if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (order.PaymentMethod != "BankTransfer")
            return BadRequest(new { message = "Đơn này không dùng phương thức chuyển khoản." });

        var tid = string.IsNullOrWhiteSpace(transactionId)
            ? "SIM" + DateTime.UtcNow.ToString("yyMMddHHmmssff")
            : transactionId.Trim();
        var content = "CK " + BankTransferService.Reference(order);
        var value = amount ?? order.Total;
        var when = DateTime.UtcNow.ToString("O");
        var sig = BankWebhookService.Sign(
            BankWebhookService.Canonical(tid, content, value, when), hook.Secret);

        var tx = new BankTransaction(tid, content, value, DateTime.UtcNow);
        var result = await hook.ProcessAsync(tx);
        return Ok(new
        {
            status = result.Status,
            message = result.Message,
            orderId = result.OrderId,
            signature = sig,
            payload = new { transactionId = tid, content, amount = value, transactionAt = when }
        });
    }

    /// <summary>Nhân viên xem nhật ký giao dịch báo có để đối soát.</summary>
    [Authorize(Roles = "Admin,Staff"), HttpGet("transactions")]
    public async Task<IActionResult> Transactions(string? status, int take = 100)
    {
        var q = db.BankTransactions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        var list = await q.OrderByDescending(x => x.Id).Take(Math.Clamp(take, 1, 500))
            .Select(x => new
            {
                x.Id, x.TransactionId, x.Content, x.Amount, x.Status, x.Note,
                x.OrderId, x.TransactionAt, x.CreatedAt
            }).ToListAsync();
        return Ok(list);
    }

    static BankTransaction Read(JsonElement body)
    {
        var tid = body.TryGetProperty("transactionId", out var a) ? a.GetString() ?? "" : "";
        var content = body.TryGetProperty("content", out var b) ? b.GetString() ?? "" : "";
        decimal amount = 0;
        if (body.TryGetProperty("amount", out var c))
        {
            amount = c.ValueKind == JsonValueKind.Number
                ? c.GetDecimal()
                : decimal.TryParse(c.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0;
        }
        DateTime? when = null;
        if (body.TryGetProperty("transactionAt", out var d) && d.ValueKind == JsonValueKind.String
            && DateTime.TryParse(d.GetString(), null, DateTimeStyles.RoundtripKind, out var w)) when = w;
        return new BankTransaction(tid, content, amount, when);
    }
}
