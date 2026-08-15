using ClickAssistant.Core.Models;

namespace ClickAssistant.Core.Engine.PointOrderStrategies;

public sealed class SequentialOrderStrategy : IPointOrderStrategy
{
    public IEnumerable<ClickPoint> GetOrder(IReadOnlyList<ClickPoint> points, IReadOnlyList<Guid>? customOrder, Random rng)
        => points;
}
