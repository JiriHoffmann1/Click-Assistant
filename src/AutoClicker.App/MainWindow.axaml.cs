using Avalonia.Controls;
using AutoClicker.App.ViewModels;

namespace AutoClicker.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}