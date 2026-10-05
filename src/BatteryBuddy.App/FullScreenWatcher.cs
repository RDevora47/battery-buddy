using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using BatteryBuddy.Pet.Ui;

namespace BatteryBuddy.App;

/// <summary>
/// Notices full-screen content (a video, a game, a slideshow) on the pet's monitor. No polling: WinEvent hooks
/// re-check when another window comes to the front, and when the front window moves or resizes (a browser video
/// going full screen in place); the second hook only listens to the front window's process. Everything runs on the
/// UI thread.
/// </summary>
sealed class FullScreenWatcher : IDisposable
{
    const uint EVENT_SYSTEM_FOREGROUND = 0x0003, EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    const uint WINEVENT_OUTOFCONTEXT = 0x0000, WINEVENT_SKIPOWNPROCESS = 0x0002;
    const int OBJID_WINDOW = 0;
    const uint MONITOR_DEFAULTTONEAREST = 2;
    const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // The shell's own windows: the desktop and the taskbars are never "full-screen content".
    static readonly string[] ShellClasses = { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

    readonly Window _window;
    readonly Func<Point> _petCentre;   // in screen pixels
    // Kept alive: the hooks call back into them.
    readonly WinEventProc _onForeground;
    readonly WinEventProc _onLocation;
    IntPtr _foregroundHook, _locationHook;
    IntPtr _foreground;
    bool _isFullScreen;

    /// <param name="petCentre">The middle of the pet, in screen pixels: its monitor is the one watched.</param>
    public FullScreenWatcher(Window window, Func<Point> petCentre)
    {
        _window = window;
        _petCentre = petCentre;
        _onForeground = (_, _, hwnd, _, _, _, _) => OnForeground(hwnd);
        _onLocation = (_, _, hwnd, idObject, _, _, _) =>
        {
            if (hwnd == _foreground && idObject == OBJID_WINDOW) Check();
        };
    }

    /// <summary>Full-screen content started or stopped covering the pet's monitor.</summary>
    public event Action<bool>? Changed;

    /// <summary>Another window came to the front: a moment when Windows may have pushed the pet down.</summary>
    public event Action? ForegroundChanged;

    public void Start()
    {
        new WindowInteropHelper(_window).EnsureHandle();
        _foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _onForeground, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        OnForeground(GetForegroundWindow());
    }

    // Follows the new front window's size from now on, then re-checks.
    void OnForeground(IntPtr hwnd)
    {
        _foreground = hwnd;
        if (_locationHook != IntPtr.Zero) UnhookWinEvent(_locationHook);
        _locationHook = IntPtr.Zero;
        if (hwnd != IntPtr.Zero && GetWindowThreadProcessId(hwnd, out uint process) != 0 && process != Environment.ProcessId)
            _locationHook = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _onLocation,
                process, 0, WINEVENT_OUTOFCONTEXT);
        ForegroundChanged?.Invoke();
        Check();
    }

    /// <summary>Re-checks now (the pet moved to another monitor, the displays changed…).</summary>
    public void Check()
    {
        bool now = IsFullScreen();
        if (now == _isFullScreen) return;
        _isFullScreen = now;
        Log.Write($"full screen: {(now ? "content covers the pet's monitor" : "gone")}");
        Changed?.Invoke(now);
    }

    bool IsFullScreen()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == new WindowInteropHelper(_window).Handle || IsShell(foreground)) return false;
        var centre = _petCentre();
        var petMonitor = MonitorFromPoint(new POINT { X = (int)centre.X, Y = (int)centre.Y }, MONITOR_DEFAULTTONEAREST);
        if (MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST) != petMonitor) return false;
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(petMonitor, ref info)) return false;
        // The visible frame, without the invisible resize borders GetWindowRect includes.
        if (DwmGetWindowAttribute(foreground, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT bounds, Marshal.SizeOf<RECT>()) != 0
            && !GetWindowRect(foreground, out bounds)) return false;
        return FullScreenCheck.Covers(ToRect(bounds), ToRect(info.rcMonitor));
    }

    static bool IsShell(IntPtr hwnd)
    {
        var name = new StringBuilder(64);
        return GetClassName(hwnd, name, name.Capacity) > 0 && ShellClasses.Contains(name.ToString());
    }

    static ScreenRect ToRect(RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

    public void Dispose()
    {
        foreach (var hook in new[] { _foregroundHook, _locationHook })
            if (hook != IntPtr.Zero) UnhookWinEvent(hook);
        _foregroundHook = _locationHook = IntPtr.Zero;
    }

    delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO { public uint cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }


    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);
}
