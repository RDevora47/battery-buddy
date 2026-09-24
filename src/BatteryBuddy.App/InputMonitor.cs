using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BatteryBuddy.App;

/// <summary>
/// Notices key presses, mouse clicks and wheel turns anywhere on the desktop via Raw Input sent to the pet window
/// (no global hook in everyone's input path). Only "a key went down" / "a button went down" leaves
/// this class (plus "the wheel turned"): which key is never exposed or logged. Held keys are remembered only to skip auto-repeat.
/// </summary>
sealed class InputMonitor : IDisposable
{
    const int WM_INPUT = 0x00FF;
    const uint RID_INPUT = 0x10000003;
    const uint RIM_TYPEMOUSE = 0, RIM_TYPEKEYBOARD = 1;
    const uint RIDEV_REMOVE = 0x00000001, RIDEV_INPUTSINK = 0x00000100;
    const ushort HID_USAGE_PAGE_GENERIC = 0x01, HID_USAGE_MOUSE = 0x02, HID_USAGE_KEYBOARD = 0x06;
    const ushort RI_KEY_BREAK = 0x01, RI_KEY_E0 = 0x02;
    // Left, right, middle, X1 and X2 button-down flags; up flags and movement don't count.
    const ushort AnyButtonDown = 0x0001 | 0x0004 | 0x0010 | 0x0040 | 0x0100;
    const ushort AnyWheel = 0x0400 | 0x0800;   // vertical and horizontal wheel

    static readonly int HeaderSize = Marshal.SizeOf<RAWINPUTHEADER>();

    // Header plus RAWMOUSE / RAWKEYBOARD fits in 64 bytes on both x86 and x64.
    readonly byte[] _buffer = new byte[64];
    readonly HashSet<int> _heldKeys = new();
    HwndSource? _source;

    public event Action? KeyPressed;
    public event Action? MouseClicked;
    public event Action? MouseScrolled;

    public void Start(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(hwnd);
        _source.AddHook(WndProc);
        if (!Register(RIDEV_INPUTSINK, hwnd))
            Log.Write($"raw input registration failed: {Marshal.GetLastWin32Error()}");
    }

    public void Dispose()
    {
        if (_source is null) return;
        Register(RIDEV_REMOVE, IntPtr.Zero);
        _source.RemoveHook(WndProc);
        _source = null;
    }

    static bool Register(uint flags, IntPtr hwnd)
    {
        var devices = new[]
        {
            new RAWINPUTDEVICE { usUsagePage = HID_USAGE_PAGE_GENERIC, usUsage = HID_USAGE_KEYBOARD, dwFlags = flags, hwndTarget = hwnd },
            new RAWINPUTDEVICE { usUsagePage = HID_USAGE_PAGE_GENERIC, usUsage = HID_USAGE_MOUSE, dwFlags = flags, hwndTarget = hwnd },
        };
        return RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
    }

    // handled stays false so DefWindowProc still cleans up after WM_INPUT.
    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_INPUT) OnRawInput(lParam);
        return IntPtr.Zero;
    }

    void OnRawInput(IntPtr handle)
    {
        uint size = (uint)_buffer.Length;
        if (GetRawInputData(handle, RID_INPUT, _buffer, ref size, (uint)HeaderSize) == uint.MaxValue) return;

        uint type = BitConverter.ToUInt32(_buffer, 0);
        if (type == RIM_TYPEMOUSE)
        {
            ushort buttonFlags = BitConverter.ToUInt16(_buffer, HeaderSize + 4);   // RAWMOUSE: usFlags, padding, usButtonFlags
            if ((buttonFlags & AnyButtonDown) != 0) MouseClicked?.Invoke();
            else if ((buttonFlags & AnyWheel) != 0) MouseScrolled?.Invoke();
        }
        else if (type == RIM_TYPEKEYBOARD)
        {
            ushort flags = BitConverter.ToUInt16(_buffer, HeaderSize + 2);         // RAWKEYBOARD: MakeCode, Flags, Reserved, VKey
            ushort vkey = BitConverter.ToUInt16(_buffer, HeaderSize + 6);
            int key = vkey | (flags & RI_KEY_E0) << 16;
            if ((flags & RI_KEY_BREAK) != 0) _heldKeys.Remove(key);
            else if (_heldKeys.Add(key)) KeyPressed?.Invoke();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

    [DllImport("user32.dll")]
    static extern uint GetRawInputData(IntPtr rawInput, uint command, [Out] byte[] data, ref uint size, uint headerSize);
}
