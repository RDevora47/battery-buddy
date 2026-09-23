using System.Windows;

namespace BatteryBuddy.App;

public partial class App : Application
{
    PetController? _controller;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Log.Prune();
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Write($"unhandled: {args.Exception}");
            args.Handled = true;
        };
        _controller = new PetController(new PetWindow());
        await _controller.StartAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        base.OnExit(e);
    }
}
