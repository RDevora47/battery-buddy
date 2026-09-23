using System.Windows;

namespace BatteryBuddy.App;

public partial class App : Application
{
    Mutex? _singleInstance;
    PetController? _controller;
    TrayIcon? _tray;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Two instances would fight over the Buds RFCOMM link.
        _singleInstance = new Mutex(true, @"Local\BatteryBuddy", out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        Log.Prune();
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Write($"unhandled: {args.Exception}");
            args.Handled = true;
        };

        var window = new PetWindow();
        _controller = new PetController(window);
        _tray = new TrayIcon(_controller.IconSprite, _controller.RescanAsync, Shutdown);
        window.MenuRequested += _tray.ShowMenu;
        await _controller.StartAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _controller?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
