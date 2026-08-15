namespace ClickAssistant.App.ViewModels;

/// <summary>Obdélník jednoho monitoru přepočítaný do souřadnic SequenceMapView.</summary>
public sealed class MapMonitorRectViewModel
{
    public int Index { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public bool IsFocused { get; init; }
}
