using AutoClicker.Core.Models;

namespace AutoClicker.Core.Engine.PointOrderStrategies;

public sealed class CustomOrderStrategy : IPointOrderStrategy
{
    public IEnumerable<ClickPoint> GetOrder(IReadOnlyList<ClickPoint> points, IReadOnlyList<Guid>? customOrder, Random rng)
    {
        if (customOrder is null || customOrder.Count == 0) return points;

        var byId = points.ToDictionary(p => p.Id);
        var ordered = new List<ClickPoint>(customOrder.Count);
        foreach (var id in customOrder)
        {
            if (byId.TryGetValue(id, out var point)) ordered.Add(point);
        }
        return ordered;
    }
}
