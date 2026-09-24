using BatteryBuddy.Pet.Animation;
using BatteryBuddy.Pet.Scene;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace BatteryBuddy.App;

sealed class TrayIcon : IDisposable
{
    readonly WinForms.NotifyIcon _icon;
    readonly WinForms.ContextMenuStrip _menu;
    readonly Func<string> _petName;

    public TrayIcon(Sprite iconSprite, Func<Task> rescan, Func<BatteryStyle> getStyle, Action<BatteryStyle> setStyle,
        IReadOnlyList<(string Skin, string Name)> pets, Func<string> getSkin, Action<string> setSkin,
        Func<FullChargeHat> getHat, Action<FullChargeHat> setHat, Action quit)
    {
        _petName = () => pets.FirstOrDefault(p => p.Skin == getSkin()).Name ?? "";
        _menu = new WinForms.ContextMenuStrip();
        _menu.Items.Add("Rescan", null, (_, _) => _ = rescan());

        var pet = new WinForms.ToolStripMenuItem("Pet");
        var petItems = pets.Select(p => (p.Skin, Item: new WinForms.ToolStripMenuItem(p.Name))).ToList();
        void SyncPet()
        {
            foreach (var (skin, item) in petItems) item.Checked = getSkin() == skin;
        }
        foreach (var (skin, item) in petItems)
        {
            item.Click += (_, _) => { setSkin(skin); SyncPet(); };
            pet.DropDownItems.Add(item);
        }
        SyncPet();
        _menu.Items.Add(pet);

        // Worn while every device is nearly full.
        var hat = new WinForms.ToolStripMenuItem("Full-charge hat");
        var hatItems = new[]
        {
            (Hat: FullChargeHat.SaiyanHair, Item: new WinForms.ToolStripMenuItem("Super Saiyan hair")),
            (Hat: FullChargeHat.StrawHat, Item: new WinForms.ToolStripMenuItem("Straw hat")),
            (Hat: FullChargeHat.None, Item: new WinForms.ToolStripMenuItem("None")),
        };
        void SyncHat()
        {
            foreach (var (h, item) in hatItems) item.Checked = getHat() == h;
        }
        foreach (var (h, item) in hatItems)
        {
            item.Click += (_, _) => { setHat(h); SyncHat(); };
            hat.DropDownItems.Add(item);
        }
        SyncHat();
        _menu.Items.Add(hat);

        var display = new WinForms.ToolStripMenuItem("Battery display");
        var outline = new WinForms.ToolStripMenuItem("Colored outline");
        var bar = new WinForms.ToolStripMenuItem("Battery bar");
        var gauge = new WinForms.ToolStripMenuItem("Mini battery");
        void Sync()
        {
            outline.Checked = getStyle() == BatteryStyle.Outline;
            bar.Checked = getStyle() == BatteryStyle.Bar;
            gauge.Checked = getStyle() == BatteryStyle.Gauge;
        }
        outline.Click += (_, _) => { setStyle(BatteryStyle.Outline); Sync(); };
        bar.Click += (_, _) => { setStyle(BatteryStyle.Bar); Sync(); };
        gauge.Click += (_, _) => { setStyle(BatteryStyle.Gauge); Sync(); };
        display.DropDownItems.Add(outline);
        display.DropDownItems.Add(bar);
        display.DropDownItems.Add(gauge);
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
            Text = Tooltip(),
            ContextMenuStrip = _menu,
            Visible = true,
        };
    }

    /// <summary>Shows the tray menu at the cursor (used for right-click on the pet).</summary>
    public void ShowMenu() => _menu.Show(WinForms.Cursor.Position);

    /// <summary>Shows a new pet in the tray: its picture and its name in the tooltip.</summary>
    public void SetIcon(Sprite iconSprite)
    {
        var old = _icon.Icon;
        _icon.Icon = ToIcon(iconSprite);
        _icon.Text = Tooltip();
        Release(old);
    }

    string Tooltip() => _petName() is { Length: > 0 } name ? $"Battery Buddy — {name}" : "Battery Buddy";

    static void Release(Drawing.Icon? icon)
    {
        if (icon is null) return;
        NativeMethods.DestroyIcon(icon.Handle);
        icon.Dispose();
    }

    static Drawing.Icon ToIcon(Sprite sprite)
    {
        var (size, pixels) = IconCanvas.Square(sprite);
        using var bitmap = new Drawing.Bitmap(size, size, Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (pixels[y * size + x] is var color and not 0)
                    bitmap.SetPixel(x, y, Drawing.Color.FromArgb(unchecked((int)color)));
        return Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        var icon = _icon.Icon;
        _icon.Dispose();
        Release(icon);
        _menu.Dispose();
    }
}
