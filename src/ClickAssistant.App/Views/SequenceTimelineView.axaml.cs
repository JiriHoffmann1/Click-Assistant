using Avalonia.Controls;
using Avalonia.Input;
using ClickAssistant.App.ViewModels;

namespace ClickAssistant.App.Views;

public partial class SequenceTimelineView : UserControl
{
    public SequenceTimelineView()
    {
        InitializeComponent();
    }

    private void Card_OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border { DataContext: SequenceStepViewModel step } ||
            DataContext is not ProfileEditorViewModel editor) return;

        editor.SelectStepCommand.Execute(step);
    }
}
