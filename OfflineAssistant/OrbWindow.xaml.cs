using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace OfflineAssistant;

public sealed partial class OrbWindow : Window
{
    private const int Size = 160;
    private bool _visible;

    public OrbWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new SizeInt32(Size, Size));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
        }
        AppWindow.Closing += (_, args) => { args.Cancel = true; Hide(); };
    }

    public void ShowAt(OrbCorner corner)
    {
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow!)) / 96d;
        var size = (int)Math.Round(Size * scale);
        var area = DisplayArea.GetFromWindowId(App.CurrentWindow!.AppWindow.Id, DisplayAreaFallback.Primary);
        var rect = area.WorkArea;
        var x = corner is OrbCorner.TopRight or OrbCorner.BottomRight ? rect.X + rect.Width - size - 24 : rect.X + 24;
        var y = corner is OrbCorner.BottomLeft or OrbCorner.BottomRight ? rect.Y + rect.Height - size - 24 : rect.Y + 24;
        AppWindow.Resize(new SizeInt32(size, size));
        AppWindow.Move(new PointInt32(x, y));
        var inset = (int)Math.Ceiling(3 * scale);
        var region = CreateEllipticRgn(inset, inset, size - inset, size - inset);
        if (region == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (SetWindowRgn(WinRT.Interop.WindowNative.GetWindowHandle(this), region, true) == 0)
        {
            var error = Marshal.GetLastWin32Error();
            DeleteObject(region);
            throw new Win32Exception(error);
        }
        if (_visible) return;
        _visible = true;
        AppWindow.Show();
        Activate();
    }

    public void Hide()
    {
        if (!_visible) return;
        _visible = false;
        AppWindow.Hide();
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateEllipticRgn(int left, int top, int right, int bottom);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);
}
