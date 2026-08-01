using Avalonia.Controls;
using Avalonia.Interactivity;
using AutoClicker.App.ViewModels;

namespace AutoClicker.App.Views;

public partial class ResolutionMismatchDialog : Window
{
    public ResolutionMismatchDialog()
    {
        InitializeComponent();
    }

    public ResolutionMismatchDialog(int oldMonitorCount, int newMonitorCount) : this()
    {
        DetailText.Text = $"Uložený stav: {oldMonitorCount} monitor(y). Aktuální stav: {newMonitorCount} monitor(y).";
    }

    private void OnRescaleClick(object? sender, RoutedEventArgs e) => Close(ResolutionMismatchChoice.Rescale);
    private void OnContinueClick(object? sender, RoutedEventArgs e) => Close(ResolutionMismatchChoice.ContinueAnyway);
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(ResolutionMismatchChoice.Cancel);
}
