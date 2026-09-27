using System.Runtime.InteropServices;

namespace BatteryBuddy.App;

static class NativeMethods
{
    const int GWL_EXSTYLE = -20;
    const long WS_EX_TOOLWINDOW = 0x00000080;   // no taskbar button, no Alt+Tab
    const long WS_EX_NOACTIVATE = 0x08000000;   // clicking never steals focus

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newLong);

    /// <summary>Frees an HICON from Bitmap.GetHicon; Icon.FromHandle doesn't take ownership of it.</summary>
    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    public static void MakeToolWindowNoActivate(IntPtr hwnd)
    {
        long style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
    }

    const long WS_EX_TOPMOST = 0x00000008;
    const uint GW_HWNDPREV = 3;
    static readonly IntPtr HWND_TOPMOST = new(-1);
    const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_NOOWNERZORDER = 0x0200;

    [DllImport("user32.dll")]
    static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    static bool IsTopmost(IntPtr hwnd) => (GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64() & WS_EX_TOPMOST) != 0;

    /// <summary>
    /// Whether a topmost window has been pushed out of the topmost band: it lost the style, or an ordinary
    /// window sits above it. Windows can do this while the window still reports itself as topmost to WPF.
    /// </summary>
    public static bool LostTopmost(IntPtr hwnd)
    {
        if (!IsTopmost(hwnd)) return true;
        for (var above = GetWindow(hwnd, GW_HWNDPREV); above != IntPtr.Zero; above = GetWindow(above, GW_HWNDPREV))
            if (!IsTopmost(above)) return true;
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    /// <summary>Moves the window's top-left to (x, y) screen pixels, keeping its size and z-order.</summary>
    public static void MoveWindow(IntPtr hwnd, int x, int y) =>
        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

    /// <summary>Puts the window back on top of the topmost band without activating it.</summary>
    public static void RestoreTopmost(IntPtr hwnd) =>
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
}
