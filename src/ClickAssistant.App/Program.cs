using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace ClickAssistant.App;

class Program
{
    // Session-local (no "Global\" prefix) mutex/event names - unique enough that no other app should
    // ever collide with them, scoped to the current user session (matches how the app is normally run).
    private const string SingleInstanceMutexName = "ClickAssistant-SingleInstance-b3f2a6e0-6d1e-4a1a-9b4a-2e8f1c7d5a3b";
    private const string ActivateSignalName = "ClickAssistant-Activate-b3f2a6e0-6d1e-4a1a-9b4a-2e8f1c7d5a3b";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Held for the whole process lifetime via `using` + the blocking StartWithClassicDesktopLifetime
        // call below - released automatically when the app exits (process end also releases it as a
        // fallback, so a crash can't permanently lock out future launches).
        using var singleInstanceMutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            // Another instance already owns the mutex - ask it to bring its window to front instead of
            // opening a second one, then exit immediately without touching Avalonia at all.
            if (OperatingSystem.IsWindows()) SignalRunningInstance();
            return;
        }

        // Named EventWaitHandle is Windows-only in .NET (unlike named Mutex, which .NET emulates on
        // Unix via a named semaphore) - the second-instance activation signal is a Windows-only nicety.
        if (OperatingSystem.IsWindows()) StartActivationListener();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    [SupportedOSPlatform("windows")]
    private static void SignalRunningInstance()
    {
        // The first instance creates the event right after it wins the mutex (see
        // StartActivationListener), so it should already exist - but guard against the tiny startup
        // race with one short retry rather than just silently doing nothing.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var signal = EventWaitHandle.OpenExisting(ActivateSignalName);
                signal.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                if (attempt == 0) Thread.Sleep(200);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static void StartActivationListener()
    {
        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateSignalName);
        Task.Run(() =>
        {
            while (true)
            {
                signal.WaitOne();
                Dispatcher.UIThread.Post(() =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
                    {
                        window.WindowState = WindowState.Maximized;
                        window.Show();
                        window.Activate();
                    }
                });
            }
        });
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
