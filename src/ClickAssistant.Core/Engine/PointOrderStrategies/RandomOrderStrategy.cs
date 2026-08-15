using ClickAssistant.Core.Models;

namespace ClickAssistant.Core.Engine.PointOrderStrategies;

public sealed class RandomOrderStrategy : IPointOrderStrategy
{
    public IEnumerable<ClickPoint> GetOrder(IReadOnlyList<ClickPoint> points, IReadOnlyList<Guid>? customOrder, Random rng)
    {
        var shuffled = points.ToList();
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        return shuffled;
    }
}
