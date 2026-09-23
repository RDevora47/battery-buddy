using BatteryBuddy.Core.Scene;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace BatteryBuddy.App;

sealed class TrayIcon : IDisposable
{
    readonly WinForms.NotifyIcon _icon;
    readonly WinForms.ContextMenuStrip _menu;

    public TrayIcon(Sprite iconSprite, Func<Task> rescan, Func<BatteryStyle> getStyle, Action<BatteryStyle> setStyle, Action quit)
    {
        _menu = new WinForms.ContextMenuStrip();
        _menu.Items.Add("Rescan", null, (_, _) => _ = rescan());

        var display = new WinForms.ToolStripMenuItem("Battery display");
        var outline = new WinForms.ToolStripMenuItem("Colored outline");
        var bar = new WinForms.ToolStripMenuItem("Battery bar");
        void Sync()
        {
            outline.Checked = getStyle() == BatteryStyle.Outline;
            bar.Checked = getStyle() == BatteryStyle.Bar;
        }
        outline.Click += (_, _) => { setStyle(BatteryStyle.Outline); Sync(); };
        bar.Click += (_, _) => { setStyle(BatteryStyle.Bar); Sync(); };
        display.DropDownItems.Add(outline);
        display.DropDownItems.Add(bar);
        Sync();
        _menu.Items.Add(display);

        var startup = new WinForms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = StartupRegistration.IsEnabled,
        };
        startup.CheckedChanged += (_, _) => StartupRegistration.SetEnabled(startup.Checked);
        _menu.Items.Add(startup);
        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add("Quit", null, (_, _) => quit());

        _icon = new WinForms.NotifyIcon
        {
            Icon = ToIcon(iconSprite),
            Text = "Battery Buddy",
            ContextMenuStrip = _menu,
            Visible = true,
        };
    }

    /// <summary>Shows the tray menu at the cursor (used for right-click on the pet).</summary>
    public void ShowMenu() => _menu.Show(WinForms.Cursor.Position);

    static Drawing.Icon ToIcon(Sprite sprite)
    {
        using var bitmap = new Drawing.Bitmap(32, 32, Drawing.Imaging.PixelFormat.Format32bppArgb);
        int offsetX = (32 - sprite.Width) / 2;
        int offsetY = (32 - sprite.Height) / 2;
        for (int y = 0; y < sprite.Height; y++)
            for (int x = 0; x < sprite.Width; x++)
            {
                uint color = sprite.Pixels[y * sprite.Width + x];
                if (color != 0) bitmap.SetPixel(x + offsetX, y + offsetY, Drawing.Color.FromArgb(unchecked((int)color)));
            }
        return Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
