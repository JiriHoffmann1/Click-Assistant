using AutoClicker.Core.Models;

namespace AutoClicker.Core.Engine.PointOrderStrategies;

public interface IPointOrderStrategy
{
    IEnumerable<ClickPoint> GetOrder(IReadOnlyList<ClickPoint> points, IReadOnlyList<Guid>? customOrder, Random rng);
}
