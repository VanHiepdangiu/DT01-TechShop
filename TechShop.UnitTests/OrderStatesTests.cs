using TechShop.Models;
using Xunit;

namespace TechShop.UnitTests;

public class OrderStatesTests
{
    // CanMove is the transition whitelist, not an actor-authorization API.
    // Actor ownership/privileges are exercised through OrderService in relational tests.
    [Theory]
    [InlineData(OrderStates.New, OrderStates.Packing, true)]
    [InlineData(OrderStates.New, OrderStates.Cancelled, true)]
    [InlineData(OrderStates.PendingPayment, OrderStates.Cancelled, true)]
    [InlineData(OrderStates.Packing, OrderStates.Shipping, true)]
    [InlineData(OrderStates.Shipping, OrderStates.Completed, true)]
    [InlineData(OrderStates.Completed, OrderStates.Returned, true)]
    [InlineData(OrderStates.Completed, OrderStates.Cancelled, false)]
    [InlineData(OrderStates.New, OrderStates.Completed, false)]
    [InlineData(OrderStates.PendingPayment, OrderStates.Packing, false)]
    [InlineData(OrderStates.Packing, OrderStates.Cancelled, false)]
    [InlineData(OrderStates.Shipping, OrderStates.Packing, false)]
    [InlineData(OrderStates.Cancelled, OrderStates.New, false)]
    [InlineData(OrderStates.Returned, OrderStates.Cancelled, false)]
    [InlineData(OrderStates.New, OrderStates.New, false)]
    [InlineData("unknown", OrderStates.Packing, false)]
    [InlineData(OrderStates.New, "unknown", false)]
    [InlineData("new", OrderStates.Packing, false)]
    public void CanMove_enforces_explicit_transition_whitelist(string from, string to, bool expected)
        => Assert.Equal(expected, OrderStates.CanMove(from, to));
}
