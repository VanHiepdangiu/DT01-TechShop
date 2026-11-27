// DT01 TechShop — Nguyễn Văn Hiệp — MSSV 2410900035 — K24CNT1
// Kiểm thử đơn vị cho webhook ngân hàng: tách mã đơn, chữ ký, chống trùng, lệch tiền.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TechShop.Models;
using TechShop.Services;
using Xunit;

namespace TechShop.UnitTests;

public class BankWebhookTests
{
    static IConfiguration Cfg(bool enabled = true, decimal tolerance = 0) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BankWebhook:Enabled"] = enabled ? "true" : "false",
            ["BankWebhook:Secret"] = "test-secret",
            ["BankWebhook:AmountTolerance"] = tolerance.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["BankWebhook:AllowSimulator"] = "true"
        }).Build();

    /// <summary>Tạo một đơn chờ thanh toán bằng chuyển khoản trong fixture.</summary>
    static async Task<Order> SeedOrder(SqliteShopFixture fx, decimal total = 500000m, string method = "BankTransfer")
    {
        await using var db = fx.NewDb();
        var o = new Order
        {
            UserId = fx.UserId, FullName = "Test", Phone = "0988089376", ShippingAddress = "HN",
            Subtotal = total, Total = total, PaymentMethod = method,
            Status = method == "BankTransfer" ? OrderStates.PendingPayment : OrderStates.New,
            IdempotencyKey = Guid.NewGuid()
        };
        db.Orders.Add(o);
        await db.SaveChangesAsync();
        return o;
    }

    static string Ref(Order o) => "TS" + o.Id.ToString("000000");

    [Fact]
    public void ExtractReference_tim_ma_dung_dinh_dang()
    {
        Assert.Equal("000123", BankWebhookService.ExtractReference("CK TS000123"));
        Assert.Equal("000123", BankWebhookService.ExtractReference("ts000123 chuyen tien"));
        Assert.Equal("000123", BankWebhookService.ExtractReference("MBVCB.123.AGRIBANK TS000123 NOI DUNG"));
        Assert.Null(BankWebhookService.ExtractReference("khong co ma"));
        Assert.Null(BankWebhookService.ExtractReference("TS12345"));    // thiếu một số
        Assert.Null(BankWebhookService.ExtractReference("TS0001234"));  // thừa một số
        Assert.Null(BankWebhookService.ExtractReference(""));
    }

    [Fact]
    public async Task Ky_va_kiem_tra_chu_ky_dung()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        await using var db = fx.NewDb();
        var svc = new BankWebhookService(db, Cfg(), NullLogger<BankWebhookService>.Instance);
        const string tid = "FT123", content = "CK TS000001", when = "2026-11-27T08:00:00Z";
        var sig = BankWebhookService.Sign(BankWebhookService.Canonical(tid, content, 500000m, when), "test-secret");
        Assert.True(svc.VerifySignature(tid, content, 500000m, when, sig));
    }

    [Fact]
    public async Task Chu_ky_sai_bi_tu_choi()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        await using var db = fx.NewDb();
        var svc = new BankWebhookService(db, Cfg(), NullLogger<BankWebhookService>.Instance);
        Assert.False(svc.VerifySignature("FT123", "CK TS000001", 500000m, null, new string('a', 64)));
        Assert.False(svc.VerifySignature("FT123", "CK TS000001", 500000m, null, null));
        Assert.False(svc.VerifySignature("FT123", "CK TS000001", 500000m, null, "abc"));
        // đổi số tiền -> chữ ký không còn đúng
        var sig = BankWebhookService.Sign(BankWebhookService.Canonical("FT123", "CK TS000001", 500000m, null), "test-secret");
        Assert.False(svc.VerifySignature("FT123", "CK TS000001", 1m, null, sig));
    }

    [Fact]
    public async Task Chua_bat_webhook_thi_tu_choi()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        await using var db = fx.NewDb();
        var svc = new BankWebhookService(db, Cfg(enabled: false), NullLogger<BankWebhookService>.Instance);
        Assert.False(svc.VerifySignature("FT1", "CK TS000001", 100m, null,
            BankWebhookService.Sign(BankWebhookService.Canonical("FT1", "CK TS000001", 100m, null), "test-secret")));
        Assert.Equal("disabled", (await svc.ProcessAsync(new BankTransaction("F", "TS000001", 1m, null))).Status);
    }

    [Fact]
    public async Task Xu_ly_thanh_cong_va_ghi_nhat_ky()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        var o = await SeedOrder(fx);
        await using var db = fx.NewDb();
        var svc = new BankWebhookService(db, Cfg(), NullLogger<BankWebhookService>.Instance);

        var r = await svc.ProcessAsync(new BankTransaction("FT001", "CK " + Ref(o), o.Total, DateTime.UtcNow));

        Assert.Equal("matched", r.Status);
        Assert.Equal(o.Id, r.OrderId);
        await using var check = fx.NewDb();
        var saved = await check.Orders.Include(x => x.History).SingleAsync(x => x.Id == o.Id);
        Assert.Equal("Paid", saved.PaymentStatus);
        Assert.Equal(OrderStates.New, saved.Status);
        Assert.Equal("FT001", saved.PaymentTransactionId);
        Assert.Contains(saved.History, h => h.Note.Contains("Webhook ngân hàng"));
        Assert.Single(await check.BankTransactions.Where(x => x.Status == "matched").ToListAsync());
    }

    [Fact]
    public async Task Gui_lai_cung_ma_giao_dich_khong_cong_hai_lan()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        var o = await SeedOrder(fx);
        await using var db = fx.NewDb();
        var svc = new BankWebhookService(db, Cfg(), NullLogger<BankWebhookService>.Instance);
        var tx = new BankTransaction("FT002", Ref(o), o.Total, DateTime.UtcNow);

        Assert.Equal("matched", (await svc.ProcessAsync(tx)).Status);
        var second = await svc.ProcessAsync(tx);

        Assert.Equal("duplicate", second.Status);
        await using var check = fx.NewDb();
        Assert.Single(await check.BankTransactions.Where(x => x.Status == "matched").ToListAsync());
    }

    [Fact]
    public async Task Lech_so_tien_thi_khong_xac_nhan()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        var o = await SeedOrder(fx, 500000m);
        await using var db = fx.NewDb();
        var svc = new BankWebhookService(db, Cfg(), NullLogger<BankWebhookService>.Instance);

        var r = await svc.ProcessAsync(new BankTransaction("FT003", Ref(o), 100000m, DateTime.UtcNow));

        Assert.Equal("amount_mismatch", r.Status);
        await using var check = fx.NewDb();
        Assert.Equal("Unpaid", (await check.Orders.SingleAsync(x => x.Id == o.Id)).PaymentStatus);
        Assert.Contains(await check.BankTransactions.ToListAsync(), x => x.Status == "unmatched");
    }

    [Fact]
    public async Task Cho_phep_sai_lech_trong_nguong()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        var o = await SeedOrder(fx, 500000m);
        await using var db = fx.NewDb();
        var svc = new BankWebhookService(db, Cfg(tolerance: 5000m), NullLogger<BankWebhookService>.Instance);

        var r = await svc.ProcessAsync(new BankTransaction("FT004", Ref(o), 497000m, DateTime.UtcNow));

        Assert.Equal("matched", r.Status);
    }

    [Fact]
    public async Task Noi_dung_khong_co_ma_don_thi_ghi_unmatched()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        await SeedOrder(fx);
        await using var db = fx.NewDb();
        var svc = new BankWebhookService(db, Cfg(), NullLogger<BankWebhookService>.Instance);

        var r = await svc.ProcessAsync(new BankTransaction("FT005", "Chuyen tien mua hang", 100000m, null));

        Assert.Equal("unmatched", r.Status);
        await using var check = fx.NewDb();
        var log = (await check.BankTransactions.ToListAsync()).Single();
        Assert.Equal("unmatched", log.Status);
        Assert.Contains("mã đối chiếu", log.Note);
    }

    [Fact]
    public async Task Don_COD_khong_bi_xac_nhan()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        var o = await SeedOrder(fx, 300000m, "COD");
        await using var db = fx.NewDb();
        var svc = new BankWebhookService(db, Cfg(), NullLogger<BankWebhookService>.Instance);

        var r = await svc.ProcessAsync(new BankTransaction("FT006", Ref(o), o.Total, null));

        Assert.Equal("unmatched", r.Status);
        await using var check = fx.NewDb();
        Assert.Equal("Unpaid", (await check.Orders.SingleAsync(x => x.Id == o.Id)).PaymentStatus);
    }

    [Fact]
    public async Task Ma_don_khong_ton_tai_thi_unmatched()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        await using var db = fx.NewDb();
        var svc = new BankWebhookService(db, Cfg(), NullLogger<BankWebhookService>.Instance);

        var r = await svc.ProcessAsync(new BankTransaction("FT007", "CK TS999999", 100000m, null));

        Assert.Equal("unmatched", r.Status);
        Assert.Contains("Không tìm thấy đơn", r.Message);
    }

    [Fact]
    public async Task So_tien_am_hoac_ma_giao_dich_rong_bi_tu_choi()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        await using var db = fx.NewDb();
        var svc = new BankWebhookService(db, Cfg(), NullLogger<BankWebhookService>.Instance);
        Assert.Equal("invalid", (await svc.ProcessAsync(new BankTransaction("FT008", "TS000001", 0m, null))).Status);
        Assert.Equal("invalid", (await svc.ProcessAsync(new BankTransaction("", "TS000001", 100m, null))).Status);
    }
}
