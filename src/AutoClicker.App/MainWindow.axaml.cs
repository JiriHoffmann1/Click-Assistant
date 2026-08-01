using Avalonia.Controls;
using AutoClicker.App.Services;
using AutoClicker.App.ViewModels;
using AutoClicker.Core.Engine;
using AutoClicker.Infrastructure.Input;
using AutoClicker.Infrastructure.Persistence;

namespace AutoClicker.App;

public partial class MainWindow : Window
{
    private readonly SharpHookGlobalListener _globalListener = new();

    public MainWindow()
    {
        InitializeComponent();

        var screenInfoProvider = new AvaloniaScreenInfoProvider(this);
        var executor = new ClickSequenceExecutor(new SharpHookInputSimulator(), screenInfoProvider: screenInfoProvider);
        var viewModel = new MainWindowViewModel(
            new JsonProfileRepository(),
            executor,
            _globalListener,
            screenInfoProvider)
        {
            OwnerWindow = this
        };

        DataContext = viewModel;

        _globalListener.Start();
        Opened += async (_, _) => await viewModel.InitializeAsync();
        Closed += (_, _) => _globalListener.Stop();
    }
}
