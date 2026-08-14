using AutoClicker.Core.Engine.PointOrderStrategies;
using AutoClicker.Core.Models;
using Xunit;

namespace AutoClicker.Core.Tests;

public class PointOrderStrategyTests
{
    private static List<ClickPoint> ThreePoints() =>
    [
        new() { Name = "A" },
        new() { Name = "B" },
        new() { Name = "C" }
    ];

    [Fact]
    public void SequentialOrderStrategy_PreservesOriginalOrder()
    {
        var points = ThreePoints();
        var strategy = new SequentialOrderStrategy();

        var order = strategy.GetOrder(points, null, new Random(1)).ToList();

        Assert.Equal(points, order);
    }

    [Fact]
    public void RandomOrderStrategy_ContainsAllPointsExactlyOnce()
    {
        var points = ThreePoints();
        var strategy = new RandomOrderStrategy();

        var order = strategy.GetOrder(points, null, new Random(42)).ToList();

        Assert.Equal(points.Count, order.Count);
        Assert.Equal(points.OrderBy(p => p.Id), order.OrderBy(p => p.Id));
    }

    [Fact]
    public void CustomOrderStrategy_FollowsGivenIdOrder()
    {
        var points = ThreePoints();
        var customOrder = new List<Guid> { points[2].Id, points[0].Id, points[1].Id };
        var strategy = new CustomOrderStrategy();

        var order = strategy.GetOrder(points, customOrder, new Random()).ToList();

        Assert.Equal(new[] { "C", "A", "B" }, order.Select(p => p.Name));
    }

    [Fact]
    public void CustomOrderStrategy_WithNullOrder_FallsBackToOriginal()
    {
        var points = ThreePoints();
        var strategy = new CustomOrderStrategy();

        var order = strategy.GetOrder(points, null, new Random()).ToList();

        Assert.Equal(points, order);
    }

    [Fact]
    public void CustomOrderStrategy_WithEmptyOrder_FallsBackToOriginal()
    {
        var points = ThreePoints();
        var strategy = new CustomOrderStrategy();

        var order = strategy.GetOrder(points, new List<Guid>(), new Random()).ToList();

        Assert.Equal(points, order);
    }

    [Fact]
    public void CustomOrderStrategy_IdsNotInPoints_AreSilentlyDropped()
    {
        var points = ThreePoints();
        var customOrder = new List<Guid> { points[1].Id, Guid.NewGuid(), points[0].Id };
        var strategy = new CustomOrderStrategy();

        var order = strategy.GetOrder(points, customOrder, new Random()).ToList();

        Assert.Equal(new[] { "B", "A" }, order.Select(p => p.Name));
    }

    [Fact]
    public void CustomOrderStrategy_NoMatchingIds_ReturnsEmpty()
    {
        var points = ThreePoints();
        var customOrder = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var strategy = new CustomOrderStrategy();

        var order = strategy.GetOrder(points, customOrder, new Random()).ToList();

        Assert.Empty(order);
    }

    [Fact]
    public void CustomOrderStrategy_DuplicateIds_RepeatsPointInOutput()
    {
        var points = ThreePoints();
        var customOrder = new List<Guid> { points[0].Id, points[0].Id, points[1].Id };
        var strategy = new CustomOrderStrategy();

        var order = strategy.GetOrder(points, customOrder, new Random()).ToList();

        Assert.Equal(new[] { "A", "A", "B" }, order.Select(p => p.Name));
    }

    [Fact]
    public void RandomOrderStrategy_WithSinglePoint_ReturnsThatPoint()
    {
        var points = new List<ClickPoint> { new() { Name = "Only" } };
        var strategy = new RandomOrderStrategy();

        var order = strategy.GetOrder(points, null, new Random(1)).ToList();

        Assert.Equal(points, order);
    }

    [Fact]
    public void RandomOrderStrategy_WithEmptyPoints_ReturnsEmpty()
    {
        var strategy = new RandomOrderStrategy();

        var order = strategy.GetOrder(new List<ClickPoint>(), null, new Random(1)).ToList();

        Assert.Empty(order);
    }

    [Fact]
    public void Factory_CreatesMatchingStrategyType()
    {
        Assert.IsType<SequentialOrderStrategy>(PointOrderStrategyFactory.Create(SequenceOrderMode.Sequential));
        Assert.IsType<CustomOrderStrategy>(PointOrderStrategyFactory.Create(SequenceOrderMode.CustomOrder));
        Assert.IsType<RandomOrderStrategy>(PointOrderStrategyFactory.Create(SequenceOrderMode.Random));
        Assert.IsType<RandomOrderStrategy>(PointOrderStrategyFactory.Create(SequenceOrderMode.RandomNoImmediateRepeat));
    }
}
