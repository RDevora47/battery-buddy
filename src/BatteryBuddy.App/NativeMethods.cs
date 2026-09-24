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
}
