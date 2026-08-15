using Avalonia.Controls;
using Avalonia.Interactivity;
using ClickAssistant.App.Localization;
using ClickAssistant.App.ViewModels;

namespace ClickAssistant.App.Views;

public partial class ResolutionMismatchDialog : Window
{
    public ResolutionMismatchDialog()
    {
        InitializeComponent();
    }

    public ResolutionMismatchDialog(int oldMonitorCount, int newMonitorCount) : this()
    {
        DetailText.Text = string.Format(LocalizationManager.Instance["resolutionDialog.detail"], oldMonitorCount, newMonitorCount);
    }

    private void OnRescaleClick(object? sender, RoutedEventArgs e) => Close(ResolutionMismatchChoice.Rescale);
    private void OnContinueClick(object? sender, RoutedEventArgs e) => Close(ResolutionMismatchChoice.ContinueAnyway);
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(ResolutionMismatchChoice.Cancel);
}
