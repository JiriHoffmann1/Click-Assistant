using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using ClickAssistant.App.ViewModels;

namespace ClickAssistant.App.Views;

public partial class SequenceMapView : UserControl
{
    public SequenceMapView()
    {
        InitializeComponent();
    }

    private void MonitorRect_OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Rectangle { DataContext: MapMonitorRectViewModel rect } ||
            DataContext is not ProfileEditorViewModel editor) return;

        editor.ToggleMonitorFocusCommand.Execute(rect.Index);
        e.Handled = true;
    }
}
