using Microsoft.EntityFrameworkCore;
using TechShop.Data;
using TechShop.Models;
using TechShop.Services;
using Xunit;

namespace TechShop.UnitTests;

public class CheckoutCouponTests
{
    private static async Task<int> PlaceAsync(SqliteShopFixture fixture, CheckoutCommand? command = null)
    {
        await using var db = fixture.NewDb();
        var result = await new CheckoutService(db).PlaceAsync(command ?? fixture.Command());
        Assert.True(result.Success, result.Message);
        return Assert.IsType<int>(result.OrderId);
    }

    private static async Task AssertRejectedWithoutMutations(SqliteShopFixture fixture, CheckoutCommand command, int expectedUses = 0)
    {
        await using (var db = fixture.NewDb())
        {
            var result = await new CheckoutService(db).PlaceAsync(command);
            Assert.False(result.Success);
            Assert.Null(result.OrderId);
        }
        await using var fresh = fixture.NewDb();
        Assert.Empty(await fresh.Orders.ToListAsync());
        Assert.Empty(await fresh.OrderItems.ToListAsync());
        Assert.Equal(5, (await fresh.Products.SingleAsync()).Stock);
        Assert.Equal(expectedUses, (await fresh.Coupons.SingleAsync()).UsedCount);
    }

    [Fact]
    public async Task Valid_coupon_discounts_server_subtotal_and_consumes_one_use()
    {
        await using var fixture = await SqliteShopFixture.CreateAsync();
        var id = await PlaceAsync(fixture);
        await using var fresh = fixture.NewDb();
        var order = await fresh.Orders.Include(x => x.Items).Include(x => x.History).SingleAsync(x => x.Id == id);
        Assert.Equal(2500000m, order.Subtotal);
        Assert.Equal(250000m, order.Discount);
        Assert.Equal(30000m, order.ShippingFee);
        Assert.Equal(2280000m, order.Total);
        Assert.Equal(fixture.CouponId, order.CouponId);
        Assert.Equal("SAVE10", order.CouponCode);
        Assert.Equal(OrderStates.New, order.Status);
        Assert.Equal(2, Assert.Single(order.Items).Quantity);
        Assert.Single(order.History);
        Assert.Equal(3, (await fresh.Products.SingleAsync()).Stock);
        Assert.Equal(1, (await fresh.Coupons.SingleAsync()).UsedCount);
    }

    [Fact]
    public async Task Coupon_lookup_trims_and_normalizes_case()
    {
        await using var fixture = await SqliteShopFixture.CreateAsync();
        var id = await PlaceAsync(fixture, fixture.Command() with { CouponCode = "  save10  " });
        await using var fresh = fixture.NewDb();
        Assert.Equal("SAVE10", (await fresh.Orders.SingleAsync(x => x.Id == id)).CouponCode);
        Assert.Equal(1, (await fresh.Coupons.SingleAsync()).UsedCount);
    }

    [Fact]
    public async Task Expired_coupon_rejects_checkout_without_consuming_stock_or_coupon()
    {
        await using var fixture = await SqliteShopFixture.CreateAsync();
        await using (var db = fixture.NewDb())
        {
            (await db.Coupons.SingleAsync()).ExpiredAt = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }
        await AssertRejectedWithoutMutations(fixture, fixture.Command());
    }

    [Fact]
    public async Task Reached_coupon_usage_limit_rejects_without_mutations()
    {
        await using var fixture = await SqliteShopFixture.CreateAsync();
        await using (var db = fixture.NewDb())
        {
            (await db.Coupons.SingleAsync()).UsedCount = 2;
            await db.SaveChangesAsync();
        }
        await AssertRejectedWithoutMutations(fixture, fixture.Command(), expectedUses: 2);
    }

    [Fact]
    public async Task Unknown_coupon_rejects_without_mutations()
    {
        await using var fixture = await SqliteShopFixture.CreateAsync();
        await AssertRejectedWithoutMutations(fixture, fixture.Command() with { CouponCode = "UNKNOWN" });
    }

    [Fact]
    public async Task Cancellation_restores_stock_and_coupon_exactly_once()
    {
        await using var fixture = await SqliteShopFixture.CreateAsync();
        var id = await PlaceAsync(fixture);
        await using (var db = fixture.NewDb())
        {
            var result = await new OrderService(db).ChangeAsync(id, OrderStates.Cancelled, fixture.UserId, staff: false);
            Assert.True(result.Success, result.Message);
        }
        await using (var db = fixture.NewDb())
        {
            var result = await new OrderService(db).ChangeAsync(id, OrderStates.Cancelled, fixture.UserId, staff: false);
            Assert.False(result.Success);
        }
        await using var fresh = fixture.NewDb();
        var order = await fresh.Orders.Include(x => x.History).SingleAsync(x => x.Id == id);
        Assert.Equal(OrderStates.Cancelled, order.Status);
        Assert.True(order.StockRestored);
        Assert.Equal(5, (await fresh.Products.SingleAsync()).Stock);
        Assert.Equal(0, (await fresh.Coupons.SingleAsync()).UsedCount);
        Assert.Equal(2, order.History.Count);
    }

    [Fact]
    public async Task Completed_order_cannot_be_cancelled_even_by_staff_and_never_restocks()
    {
        await using var fixture = await SqliteShopFixture.CreateAsync();
        var id = await PlaceAsync(fixture);
        foreach (var target in new[] { OrderStates.Packing, OrderStates.Shipping, OrderStates.Completed })
        {
            await using var db = fixture.NewDb();
            var result = await new OrderService(db).ChangeAsync(id, target, fixture.OtherUserId, staff: true);
            Assert.True(result.Success, result.Message);
        }
        await using (var db = fixture.NewDb())
            Assert.False((await new OrderService(db).ChangeAsync(id, OrderStates.Cancelled, fixture.OtherUserId, staff: true)).Success);
        await using var fresh = fixture.NewDb();
        var order = await fresh.Orders.Include(x => x.History).SingleAsync();
        Assert.Equal(OrderStates.Completed, order.Status);
        Assert.False(order.StockRestored);
        Assert.Equal(4, order.History.Count);
        Assert.Equal(3, (await fresh.Products.SingleAsync()).Stock);
        Assert.Equal(1, (await fresh.Coupons.SingleAsync()).UsedCount);
    }

    [Fact]
    public async Task Invalid_payment_method_rejects_without_mutations()
    {
        await using var fixture = await SqliteShopFixture.CreateAsync();
        await AssertRejectedWithoutMutations(fixture, fixture.Command() with { PaymentMethod = "UntrustedGateway" });
    }

    [Fact]
    public async Task Idempotency_key_belongs_to_original_user_and_does_not_expose_order_to_other_user()
    {
        await using var fixture = await SqliteShopFixture.CreateAsync();
        var command = fixture.Command();
        var id = await PlaceAsync(fixture, command);
        await using (var db = fixture.NewDb())
        {
            var result = await new CheckoutService(db).PlaceAsync(command with { UserId = fixture.OtherUserId });
            Assert.False(result.Success);
            Assert.Null(result.OrderId);
        }
        await using var fresh = fixture.NewDb();
        var order = Assert.Single(await fresh.Orders.ToListAsync());
        Assert.Equal(id, order.Id);
        Assert.Equal(fixture.UserId, order.UserId);
        Assert.Equal(3, (await fresh.Products.SingleAsync()).Stock);
        Assert.Equal(1, (await fresh.Coupons.SingleAsync()).UsedCount);
    }

    [Fact]
    public async Task Same_user_idempotent_retry_does_not_consume_coupon_twice()
    {
        await using var fixture = await SqliteShopFixture.CreateAsync();
        var command = fixture.Command();
        var id = await PlaceAsync(fixture, command);
        Assert.Equal(id, await PlaceAsync(fixture, command));
        await using var fresh = fixture.NewDb();
        Assert.Single(await fresh.Orders.ToListAsync());
        Assert.Equal(3, (await fresh.Products.SingleAsync()).Stock);
        Assert.Equal(1, (await fresh.Coupons.SingleAsync()).UsedCount);
    }

    [Fact]
    public async Task Different_customer_cannot_cancel_someone_elses_order()
    {
        await using var fixture = await SqliteShopFixture.CreateAsync();
        var id = await PlaceAsync(fixture);
        await using (var db = fixture.NewDb())
            Assert.False((await new OrderService(db).ChangeAsync(id, OrderStates.Cancelled, fixture.OtherUserId, staff: false)).Success);
        await AssertOriginalOrderUntouched(fixture);
    }

    [Fact]
    public async Task Customer_cannot_perform_staff_only_transition()
    {
        await using var fixture = await SqliteShopFixture.CreateAsync();
        var id = await PlaceAsync(fixture);
        await using (var db = fixture.NewDb())
            Assert.False((await new OrderService(db).ChangeAsync(id, OrderStates.Packing, fixture.UserId, staff: false)).Success);
        await AssertOriginalOrderUntouched(fixture);
    }

    private static async Task AssertOriginalOrderUntouched(SqliteShopFixture fixture)
    {
        await using var fresh = fixture.NewDb();
        var order = await fresh.Orders.Include(x => x.History).SingleAsync();
        Assert.Equal(OrderStates.New, order.Status);
        Assert.False(order.StockRestored);
        Assert.Single(order.History);
        Assert.Equal(3, (await fresh.Products.SingleAsync()).Stock);
        Assert.Equal(1, (await fresh.Coupons.SingleAsync()).UsedCount);
    }

    [Fact]
    public async Task Exception_after_stock_save_rolls_back_coupon_stock_and_order_atomically()
    {
        await using var fixture = await SqliteShopFixture.CreateAsync();
        await using (var db = fixture.NewDb())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new CheckoutService(db, new FailAfterStock()).PlaceAsync(fixture.Command()));
        await using var fresh = fixture.NewDb();
        Assert.Equal(5, (await fresh.Products.SingleAsync()).Stock);
        Assert.Equal(0, (await fresh.Coupons.SingleAsync()).UsedCount);
        Assert.Empty(await fresh.Orders.ToListAsync());
        Assert.Empty(await fresh.OrderItems.ToListAsync());
    }

    private sealed class FailAfterStock : ICheckoutObserver
    {
        public Task AfterReadAsync() => Task.CompletedTask;
        public Task AfterStockSavedAsync() => throw new InvalidOperationException("Synthetic test-only transaction failure");
    }
}
