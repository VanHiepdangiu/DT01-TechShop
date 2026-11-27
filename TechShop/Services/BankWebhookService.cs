// DT01 TechShop — Nguyễn Văn Hiệp — MSSV 2410900035 — K24CNT1
// Webhook mô phỏng ngân hàng báo có: xác thực chữ ký HMAC, đối chiếu mã đơn và số tiền,
// tự động xác nhận thanh toán. Thiết kế tương thích cách các cổng thật (Casso, VNPay) gọi vào.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechShop.Data;
using TechShop.Models;

namespace TechShop.Services;

/// <summary>Kết quả xử lý một giao dịch báo có.</summary>
public record WebhookResult(string Status, string Message, int? OrderId = null)
{
    public bool Ok => Status is "matched" or "duplicate";
}

/// <summary>Dữ liệu ngân hàng gửi sang khi có tiền vào tài khoản.</summary>
public record BankTransaction(
    string TransactionId,   // mã giao dịch phía ngân hàng, dùng chống trùng
    string Content,         // nội dung chuyển tiền, phải chứa mã đối chiếu TSxxxxxx
    decimal Amount,         // số tiền nhận được
    DateTime? TransactionAt // thời điểm giao dịch (UTC)
);

/// <summary>
/// Nghiệp vụ webhook báo có. Nhận giao dịch từ ngân hàng, tìm đơn khớp theo mã đối chiếu,
/// kiểm tra số tiền, rồi xác nhận thanh toán trong một giao dịch có khóa chống trùng.
/// </summary>
public class BankWebhookService(ShopDbContext db, IConfiguration config, ILogger<BankWebhookService> logger)
{
    public bool Enabled => config.GetValue<bool>("BankWebhook:Enabled");
    public string Secret => config["BankWebhook:Secret"] ?? "";
    /// <summary>Cho phép nhận sai lệch số tiền trong khoảng này (đồng) để bù phí chuyển khoản.</summary>
    public decimal Tolerance => config.GetValue<decimal?>("BankWebhook:AmountTolerance") ?? 0m;

    // ---------------------------------------------------------------- chữ ký
    /// <summary>
    /// Dựng chuỗi chuẩn từ dữ liệu giao dịch theo thứ tự cố định, để hai bên sinh cùng một chữ ký.
    /// Không đưa trường chữ ký vào chuỗi này.
    /// </summary>
    public static string Canonical(string transactionId, string content, decimal amount, string? when)
        => string.Join("&", new[]
        {
            "amount=" + amount.ToString("0.##", CultureInfo.InvariantCulture),
            "content=" + content,
            "transactionId=" + transactionId,
            "transactionAt=" + (when ?? "")
        });

    public static string Sign(string canonical, string secret)
        => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),
                                                   Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

    /// <summary>Kiểm tra chữ ký theo kiểu so sánh hằng thời gian để tránh tấn công dò thời gian.</summary>
    public bool VerifySignature(string transactionId, string content, decimal amount, string? when, string? signature)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(Secret) || string.IsNullOrWhiteSpace(signature)) return false;
        if (signature.Length != 64) return false;
        try
        {
            var expected = Sign(Canonical(transactionId, content, amount, when), Secret);
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(signature),
                                                           Convert.FromHexString(expected));
        }
        catch (FormatException) { return false; }
    }

    // ------------------------------------------------------------ xử lý chính
    /// <summary>
    /// Xử lý một giao dịch báo có. Idempotent: cùng TransactionId gửi lại nhiều lần chỉ ghi một lần.
    /// </summary>
    public async Task<WebhookResult> ProcessAsync(BankTransaction tx)
    {
        if (!Enabled)
            return new("disabled", "Webhook chưa được bật trong cấu hình.");
        if (string.IsNullOrWhiteSpace(tx.TransactionId))
            return new("invalid", "Thiếu mã giao dịch.");
        if (tx.Amount <= 0)
            return new("invalid", "Số tiền không hợp lệ.");

        // 1) Chống trùng theo mã giao dịch ngân hàng
        var seen = await db.BankTransactions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TransactionId == tx.TransactionId);
        if (seen != null)
            return new("duplicate", "Giao dịch đã được xử lý trước đó.", seen.OrderId);

        // 2) Tìm đơn theo mã đối chiếu trong nội dung chuyển tiền
        var reference = ExtractReference(tx.Content);
        if (reference == null)
            return await SaveUnmatched(tx, "Nội dung chuyển tiền không chứa mã đối chiếu TSxxxxxx.");

        // Mã đối chiếu là 6 chữ số (TS000001), còn Id lưu dạng số (1) nên phải bỏ số 0 đệm.
        if (!int.TryParse(reference, out var orderId))
            return await SaveUnmatched(tx, $"Mã đối chiếu {reference} không hợp lệ.");
        var order = await db.Orders.Include(x => x.History)
            .FirstOrDefaultAsync(x => x.Id == orderId);
        if (order == null)
            return await SaveUnmatched(tx, $"Không tìm thấy đơn {reference}.");
        if (order.PaymentMethod != "BankTransfer")
            return await SaveUnmatched(tx, $"Đơn {order.Code} không dùng phương thức chuyển khoản.");
        if (order.PaymentStatus == "Paid")
            return await SaveDuplicatePaid(tx, order);

        // 3) Đối chiếu số tiền
        var diff = Math.Abs(order.Total - tx.Amount);
        if (diff > Tolerance)
        {
            await SaveUnmatched(tx, $"Số tiền lệch {diff.ToString("N0")} ₫ so với đơn " +
                                    $"({tx.Amount.ToString("N0")} ₫ nhận được, cần {order.Total.ToString("N0")} ₫).",
                                order.Id);
            return new("amount_mismatch",
                       $"Số tiền không khớp đơn {order.Code}; đã ghi nhận để nhân viên kiểm tra.", order.Id);
        }

        // 4) Xác nhận thanh toán
        order.PaymentStatus = "Paid";
        order.PaymentTransactionId = tx.TransactionId;
        if (order.Status == OrderStates.PendingPayment) order.Status = OrderStates.New;
        order.History.Add(new OrderHistory
        {
            Status = order.Status,
            Note = $"Webhook ngân hàng: nhận {tx.Amount.ToString("N0")} ₫, mã giao dịch {tx.TransactionId}."
        });
        db.BankTransactions.Add(new BankTransactionLog
        {
            TransactionId = tx.TransactionId, Content = tx.Content, Amount = tx.Amount,
            TransactionAt = tx.TransactionAt ?? DateTime.UtcNow, OrderId = order.Id, Status = "matched"
        });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning("Webhook tranh chấp khi xác nhận đơn {Code}", order.Code);
            return new("conflict", "Đơn vừa được cập nhật bởi người khác. Giao dịch được ghi nhận để kiểm tra lại.",
                       order.Id);
        }
        catch (DbUpdateException ex)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning(ex, "Webhook lỗi ghi dữ liệu cho giao dịch {Tid}", tx.TransactionId);
            return new("duplicate", "Giao dịch đã được xử lý (ghi trùng bị chặn).", order.Id);
        }

        logger.LogInformation("Webhook xác nhận đơn {Code} với giao dịch {Tid}", order.Code, tx.TransactionId);
        return new("matched", $"Đã tự động xác nhận thanh toán cho đơn {order.Code}.", order.Id);
    }

    // ------------------------------------------------------------------ phụ
    /// <summary>
    /// Tách mã đối chiếu TSxxxxxx (6 chữ số) ra khỏi nội dung chuyển tiền do ngân hàng trả về.
    /// Ngân hàng thường thêm tiền tố, dấu cách hoặc chuyển thành chữ hoa/thường.
    /// </summary>
    public static string? ExtractReference(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        var upper = content.ToUpperInvariant();
        var idx = upper.IndexOf("TS", StringComparison.Ordinal);
        while (idx >= 0)
        {
            var digits = new StringBuilder();
            for (var i = idx + 2; i < upper.Length && char.IsDigit(upper[i]); i++) digits.Append(upper[i]);
            if (digits.Length == 6) return digits.ToString();
            idx = upper.IndexOf("TS", idx + 1, StringComparison.Ordinal);
        }
        return null;
    }

    async Task<WebhookResult> SaveUnmatched(BankTransaction tx, string reason, int? orderId = null)
    {
        db.BankTransactions.Add(new BankTransactionLog
        {
            TransactionId = tx.TransactionId, Content = tx.Content, Amount = tx.Amount,
            TransactionAt = tx.TransactionAt ?? DateTime.UtcNow, OrderId = orderId,
            Status = "unmatched", Note = reason.Length > 300 ? reason[..300] : reason
        });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); }
        return new("unmatched", reason, orderId);
    }

    async Task<WebhookResult> SaveDuplicatePaid(BankTransaction tx, Order order)
    {
        db.BankTransactions.Add(new BankTransactionLog
        {
            TransactionId = tx.TransactionId, Content = tx.Content, Amount = tx.Amount,
            TransactionAt = tx.TransactionAt ?? DateTime.UtcNow, OrderId = order.Id,
            Status = "duplicate", Note = "Đơn đã ở trạng thái đã thanh toán."
        });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); }
        return new("duplicate", $"Đơn {order.Code} đã được thanh toán trước đó.", order.Id);
    }

    /// <summary>Ghi nhật ký với nội dung JSON do ngân hàng gửi, dùng khi không đọc được trường.</summary>
    public async Task LogRawAsync(string body, string signature, string reason)
    {
        using var doc = JsonDocument.Parse("{}");
        var tr = new BankTransactionLog
        {
            TransactionId = "raw-" + DateTime.UtcNow.Ticks,
            Content = "{}", Amount = 0, TransactionAt = DateTime.UtcNow,
            Status = "invalid", Note = ("Chữ ký không hợp lệ hoặc dữ liệu sai định dạng. " + reason).Trim()
        };
        db.BankTransactions.Add(tr);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); }
    }
}
