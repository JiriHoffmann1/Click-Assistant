using AutoClicker.Core.Models;

namespace AutoClicker.Core.Engine.PointOrderStrategies;

public static class PointOrderStrategyFactory
{
    public static IPointOrderStrategy Create(SequenceOrderMode mode) => mode switch
    {
        SequenceOrderMode.Sequential => new SequentialOrderStrategy(),
        SequenceOrderMode.CustomOrder => new CustomOrderStrategy(),
        SequenceOrderMode.Random => new RandomOrderStrategy(),
        SequenceOrderMode.RandomNoImmediateRepeat => new RandomOrderStrategy(),
        _ => new SequentialOrderStrategy()
    };
}
