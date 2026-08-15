namespace ClickAssistant.Core.Engine;

public enum EngineStatus
{
    Idle,
    Running,
    Stopped
}

public sealed class EngineStatusEventArgs(EngineStatus status) : EventArgs
{
    public EngineStatus Status { get; } = status;
}
