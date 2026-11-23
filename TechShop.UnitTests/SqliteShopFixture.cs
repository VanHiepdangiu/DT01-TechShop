using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TechShop.Data;
using TechShop.Models;
using TechShop.Services;

namespace TechShop.UnitTests;

// A private SQLite :memory: database per test. No application configuration,
// external connection strings, database files, SQL Server, or deletion API.
internal sealed class SqliteShopFixture : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private DbContextOptions<ShopDbContext> options = null!;
    public int UserId { get; private set; }
    public int OtherUserId { get; private set; }
    public int ProductId { get; private set; }
    public int CouponId { get; private set; }

    public static async Task<SqliteShopFixture> CreateAsync()
    {
        var fixture = new SqliteShopFixture();
        try
        {
            await fixture.connection.OpenAsync();
            fixture.options = new DbContextOptionsBuilder<ShopDbContext>()
                .UseSqlite(fixture.connection).Options;
            await using var db = fixture.NewDb();
            await db.Database.EnsureCreatedAsync();
            var user = new ShopUser { FullName = "Test owner", UserName = "owner", NormalizedEmail = "OWNER@TEST.INVALID" };
            var other = new ShopUser { FullName = "Other customer", UserName = "other", NormalizedEmail = "OTHER@TEST.INVALID" };
            var category = new Category { Name = "Unit-test category" };
            db.AddRange(user, other, category);
            await db.SaveChangesAsync();
            var product = new Product { Name = "Unit-test product", CategoryId = category.Id, Price = 1250000m, Stock = 5 };
            var coupon = new Coupon { Code = "SAVE10", DiscountPercent = 10, ExpiredAt = DateTime.UtcNow.AddDays(1), UsageLimit = 2 };
            db.AddRange(product, coupon);
            await db.SaveChangesAsync();
            fixture.UserId = user.Id;
            fixture.OtherUserId = other.Id;
            fixture.ProductId = product.Id;
            fixture.CouponId = coupon.Id;
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public ShopDbContext NewDb() => new(options);
    public CheckoutCommand Command() => new(UserId, Guid.NewGuid(), "Test buyer", "0900000000", "123 Test street",
        new[] { new CartLine(ProductId, 2) }, "SAVE10");
    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
