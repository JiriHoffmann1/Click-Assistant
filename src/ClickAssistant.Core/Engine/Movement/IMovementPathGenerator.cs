using ClickAssistant.Core.Models;

namespace ClickAssistant.Core.Engine.Movement;

public interface IMovementPathGenerator
{
    IReadOnlyList<(ScreenPoint Point, int StepDelayMs)> GeneratePath(
        ScreenPoint start, ScreenPoint end, HumanizationConfig config, Random rng);
}
