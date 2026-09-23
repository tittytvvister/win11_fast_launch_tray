using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Runtime.InteropServices;

internal sealed class MenuRule
{
    public string category { get; set; }
    public string match { get; set; }
}

internal sealed class MenuConfig
{
    public string sourcePath { get; set; }
    public string trayIconPath { get; set; }
    public string menuTitle { get; set; }
    public string defaultCategory { get; set; }
    public string[] categoryOrder { get; set; }
    public MenuRule[] rules { get; set; }
}

internal sealed class ShortcutItem
{
    public string Name { get; set; }
    public string Path { get; set; }
    public string Category { get; set; }
}

internal sealed class Win11Palette
{
    public Color Background { get; private set; }
    public Color Border { get; private set; }
    public Color Text { get; private set; }
    public Color MutedText { get; private set; }
    public Color Hover { get; private set; }
    public Color Separator { get; private set; }

    public Win11Palette(bool light)
    {
        Background = light ? Color.FromArgb(249, 249, 249) : Color.FromArgb(44, 44, 44);
        Border = light ? Color.FromArgb(218, 218, 218) : Color.FromArgb(69, 69, 69);
        Text = light ? Color.FromArgb(27, 27, 27) : Color.FromArgb(255, 255, 255);
        MutedText = light ? Color.FromArgb(96, 96, 96) : Color.FromArgb(196, 196, 196);
        Hover = light ? Color.FromArgb(232, 232, 232) : Color.FromArgb(59, 59, 59);
        Separator = light ? Color.FromArgb(224, 224, 224) : Color.FromArgb(73, 73, 73);
    }
}

internal sealed class Win11ColorTable : ProfessionalColorTable
{
    private readonly Win11Palette palette;

    public Win11ColorTable(Win11Palette value)
    {
        palette = value;
        UseSystemColors = false;
    }

    public override Color ToolStripDropDownBackground { get { return palette.Background; } }
    public override Color MenuBorder { get { return palette.Border; } }
    public override Color MenuItemBorder { get { return palette.Hover; } }
    public override Color MenuItemSelected { get { return palette.Hover; } }
    public override Color ImageMarginGradientBegin { get { return palette.Background; } }
    public override Color ImageMarginGradientMiddle { get { return palette.Background; } }
    public override Color ImageMarginGradientEnd { get { return palette.Background; } }
    public override Color SeparatorDark { get { return palette.Separator; } }
    public override Color SeparatorLight { get { return palette.Separator; } }
}

internal sealed class Win11Renderer : ToolStripProfessionalRenderer
{
    private readonly Win11Palette palette;

    public Win11Renderer(Win11Palette value) : base(new Win11ColorTable(value))
    {
        palette = value;
        RoundedEdges = true;
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        e.Graphics.Clear(palette.Background);
        if (!e.Item.Selected)
            return;

        Rectangle bounds = new Rectangle(4, 1, Math.Max(1, e.Item.Width - 8), Math.Max(1, e.Item.Height - 2));
        using (GraphicsPath path = RoundedRectangle(bounds, 4))
        using (SolidBrush brush = new SolidBrush(palette.Hover))
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? palette.Text : palette.MutedText;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = palette.MutedText;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using (Pen pen = new Pen(palette.Separator))
            e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        Rectangle bounds = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        using (Pen pen = new Pen(palette.Border))
            e.Graphics.DrawRectangle(pen, bounds);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class TrayLauncher : ApplicationContext
{
    private readonly NotifyIcon trayIcon;
    private readonly ContextMenuStrip menu;
    private readonly Form ownerWindow;
    private MenuConfig config;
    private bool? lightTheme;
    private Win11Palette palette;
    private Font menuFont;
    private Icon customTrayIcon;
    private string loadedTrayIconPath;

    public TrayLauncher()
    {
        menu = new ContextMenuStrip
        {
            AutoClose = true,
            ShowImageMargin = true,
            ShowCheckMargin = false,
            Padding = new Padding(4),
            ImageScalingSize = new Size(20, 20)
        };
        menu.Opening += delegate { RebuildMenu(); };

        ownerWindow = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            Size = new Size(1, 1),
            Opacity = 0
        };
        ownerWindow.Show();

        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "My Programs",
            Visible = true
        };
        trayIcon.MouseUp += OnTrayClick;

        LoadConfig();
    }

    private string ConfigPath
    {
        get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desktop-menu-config.json"); }
    }

    private void LoadConfig()
    {
        string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
        config = new JavaScriptSerializer().Deserialize<MenuConfig>(json);
        if (string.IsNullOrWhiteSpace(config.sourcePath))
            config.sourcePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "menu");
        config.sourcePath = Environment.ExpandEnvironmentVariables(config.sourcePath);
        if (string.IsNullOrWhiteSpace(config.menuTitle))
            config.menuTitle = "My Programs";
        if (string.IsNullOrWhiteSpace(config.defaultCategory))
            config.defaultCategory = "Other";

        trayIcon.Text = config.menuTitle.Length > 63 ? config.menuTitle.Substring(0, 63) : config.menuTitle;
        ApplyTrayIcon();
    }

    private void ApplyTrayIcon()
    {
        string iconPath = config.trayIconPath;
        if (!string.IsNullOrWhiteSpace(iconPath))
            iconPath = Environment.ExpandEnvironmentVariables(iconPath);
        if (!string.IsNullOrWhiteSpace(iconPath) && !Path.IsPathRooted(iconPath))
            iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, iconPath);

        if (string.Equals(iconPath, loadedTrayIconPath, StringComparison.OrdinalIgnoreCase))
            return;

        Icon nextIcon = null;
        if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
            nextIcon = new Icon(iconPath);
        if (nextIcon == null)
            nextIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        Icon oldIcon = customTrayIcon;
        customTrayIcon = nextIcon;
        loadedTrayIconPath = iconPath;
        trayIcon.Icon = customTrayIcon;
        if (oldIcon != null)
            oldIcon.Dispose();
    }

    private void OnTrayClick(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
        {
            if (menu.Visible)
            {
                menu.Close(ToolStripDropDownCloseReason.AppClicked);
                return;
            }

            RebuildMenu();
            ownerWindow.Show();
            ownerWindow.Activate();
            NativeMethods.SetForegroundWindow(ownerWindow.Handle);
            menu.Show(Cursor.Position);
            NativeMethods.PostMessage(ownerWindow.Handle, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private void RebuildMenu()
    {
        menu.Items.Clear();

        try
        {
            LoadConfig();
            ApplyTheme();
            List<ShortcutItem> items = ReadShortcuts();
            Dictionary<string, int> order = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
            if (config.categoryOrder != null)
            {
                for (int i = 0; i < config.categoryOrder.Length; i++)
                    order[config.categoryOrder[i]] = i;
            }

            var groups = items.GroupBy(item => item.Category)
                .OrderBy(group => order.ContainsKey(group.Key) ? order[group.Key] : 1000)
                .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase);

            foreach (var group in groups)
            {
                ToolStripMenuItem categoryItem = new ToolStripMenuItem(group.Key)
                {
                    Padding = new Padding(8, 5, 8, 5)
                };
                foreach (ShortcutItem item in group.OrderBy(value => value.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    ToolStripMenuItem shortcutItem = new ToolStripMenuItem(item.Name)
                    {
                        Padding = new Padding(8, 5, 8, 5)
                    };
                    shortcutItem.Tag = item.Path;
                    shortcutItem.Click += LaunchShortcut;
                    try
                    {
                        shortcutItem.Image = CreateMenuImage(item.Path);
                        shortcutItem.ImageScaling = ToolStripItemImageScaling.SizeToFit;
                    }
                    catch
                    {
                        // A missing icon must not prevent the shortcut from appearing.
                    }
                    categoryItem.DropDownItems.Add(shortcutItem);
                }
                ToolStripDropDown categoryDropDown = categoryItem.DropDown;
                categoryItem.DropDownOpening += delegate { ApplyDropDownStyle(categoryDropDown); };
                menu.Items.Add(categoryItem);
            }

            if (items.Count == 0)
            {
                ToolStripMenuItem emptyItem = new ToolStripMenuItem("\u042f\u0440\u043b\u044b\u043a\u0438 \u043d\u0435 \u043d\u0430\u0439\u0434\u0435\u043d\u044b") { Enabled = false };
                menu.Items.Add(emptyItem);
            }
        }
        catch (Exception ex)
        {
            ToolStripMenuItem errorItem = new ToolStripMenuItem("\u041e\u0448\u0438\u0431\u043a\u0430: " + ex.Message) { Enabled = false };
            menu.Items.Add(errorItem);
        }

        menu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem openFolder = new ToolStripMenuItem("\u041e\u0442\u043a\u0440\u044b\u0442\u044c \u043f\u0430\u043f\u043a\u0443")
        {
            Padding = new Padding(8, 5, 8, 5)
        };
        openFolder.Click += delegate { OpenPath(config.sourcePath); };
        menu.Items.Add(openFolder);

        ToolStripMenuItem exitItem = new ToolStripMenuItem("\u0412\u044b\u0445\u043e\u0434")
        {
            Padding = new Padding(8, 5, 8, 5)
        };
        exitItem.Click += delegate { ExitLauncher(); };
        menu.Items.Add(exitItem);
    }

    private static Bitmap CreateMenuImage(string path)
    {
        NativeMethods.SHFILEINFO info = new NativeMethods.SHFILEINFO();
        IntPtr result = NativeMethods.SHGetFileInfo(
            path,
            0,
            ref info,
            (uint)Marshal.SizeOf(info),
            NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);

        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            return null;

        try
        {
            using (Icon icon = (Icon)Icon.FromHandle(info.hIcon).Clone())
            using (Bitmap source = icon.ToBitmap())
            {
                Bitmap output = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(output))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.DrawImage(source, new Rectangle(1, 1, 18, 18));
                }
                return output;
            }
        }
        finally
        {
            NativeMethods.DestroyIcon(info.hIcon);
        }
    }

    private void ApplyTheme()
    {
        bool isLight = true;
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
        {
            object value = key == null ? null : key.GetValue("AppsUseLightTheme");
            if (value != null)
                isLight = Convert.ToInt32(value) != 0;
        }

        if (lightTheme != isLight)
        {
            lightTheme = isLight;
            palette = new Win11Palette(isLight);
            menu.Renderer = new Win11Renderer(palette);
            menu.BackColor = palette.Background;
            menu.ForeColor = palette.Text;

            if (menuFont != null)
                menuFont.Dispose();
            menuFont = new Font("Segoe UI Variable Text", 10F, FontStyle.Regular, GraphicsUnit.Point);
            menu.Font = menuFont;
        }

        ApplyDropDownStyle(menu);
    }

    private void ApplyDropDownStyle(ToolStripDropDown dropDown)
    {
        dropDown.Renderer = menu.Renderer;
        dropDown.BackColor = palette.Background;
        dropDown.ForeColor = palette.Text;
        dropDown.Font = menu.Font;
        dropDown.Padding = new Padding(4);

        ToolStripDropDownMenu dropDownMenu = dropDown as ToolStripDropDownMenu;
        if (dropDownMenu != null)
        {
            dropDownMenu.ShowImageMargin = true;
            dropDownMenu.ShowCheckMargin = false;
        }

        int cornerPreference = 2;
        NativeMethods.DwmSetWindowAttribute(dropDown.Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
        int darkMode = lightTheme == false ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(dropDown.Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
    }

    private List<ShortcutItem> ReadShortcuts()
    {
        List<ShortcutItem> result = new List<ShortcutItem>();
        if (!Directory.Exists(config.sourcePath))
            return result;

        IEnumerable<string> paths = Directory.EnumerateFiles(config.sourcePath, "*", SearchOption.AllDirectories)
            .Where(path => string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(Path.GetExtension(path), ".url", StringComparison.OrdinalIgnoreCase));

        foreach (string path in paths)
        {
            string directory = Path.GetDirectoryName(path);
            string relativeDirectory = directory.Length > config.sourcePath.TrimEnd('\\').Length
                ? directory.Substring(config.sourcePath.TrimEnd('\\').Length).TrimStart('\\')
                : string.Empty;

            string name = Path.GetFileNameWithoutExtension(path);
            result.Add(new ShortcutItem
            {
                Name = name,
                Path = path,
                Category = GetCategory(name, relativeDirectory)
            });
        }

        return result;
    }

    private string GetCategory(string name, string relativeDirectory)
    {
        if (!string.IsNullOrWhiteSpace(relativeDirectory))
            return relativeDirectory.Split('\\')[0];

        if (config.rules != null)
        {
            foreach (MenuRule rule in config.rules)
            {
                if (!string.IsNullOrWhiteSpace(rule.match) &&
                    Regex.IsMatch(name, rule.match, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    return rule.category;
            }
        }

        return config.defaultCategory;
    }

    private void LaunchShortcut(object sender, EventArgs e)
    {
        ToolStripMenuItem item = sender as ToolStripMenuItem;
        if (item != null)
            OpenPath((string)item.Tag);
    }

    private static void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "My Programs", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExitLauncher()
    {
        trayIcon.Visible = false;
        trayIcon.Dispose();
        menu.Dispose();
        if (menuFont != null)
            menuFont.Dispose();
        if (customTrayIcon != null)
            customTrayIcon.Dispose();
        ownerWindow.Close();
        ownerWindow.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            menu.Dispose();
            if (menuFont != null)
                menuFont.Dispose();
            if (customTrayIcon != null)
                customTrayIcon.Dispose();
            ownerWindow.Close();
            ownerWindow.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal static class NativeMethods
{
    internal const uint WM_NULL = 0x0000;
    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const uint SHGFI_ICON = 0x000000100;
    internal const uint SHGFI_LARGEICON = 0x000000000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int valueSize);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SHGetFileInfo(string path, uint fileAttributes, ref SHFILEINFO info, uint infoSize, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    internal static void EnablePerMonitorDpi()
    {
        try
        {
            SetProcessDpiAwarenessContext(new IntPtr(-4));
        }
        catch (EntryPointNotFoundException)
        {
            // The embedded manifest handles DPI awareness on older Windows versions.
        }
    }
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        NativeMethods.EnablePerMonitorDpi();
        bool createdNew;
        using (Mutex mutex = new Mutex(true, "Win11FastLaunchTray", out createdNew))
        {
            if (!createdNew)
                return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayLauncher());
        }
    }
}
