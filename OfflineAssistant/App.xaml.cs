using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace OfflineAssistant;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private System.Windows.Forms.NotifyIcon? _tray;
    private OrbWindow? _orb;
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showRequest;
    public static Window? CurrentWindow { get; private set; }
    public static VoiceController Voice { get; private set; } = null!;
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLog.Error("Необработанная ошибка", e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject.ToString()));
        TaskScheduler.UnobservedTaskException += (_, e) => AppLog.Error("Ошибка фоновой задачи", e.Exception);
        UnhandledException += (_, e) => AppLog.Error("Ошибка интерфейса", e.Exception);
        AppLog.Info("Запуск приложения");
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var background = Environment.GetCommandLineArgs().Contains("--background");
        _showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\SphereShowWindow");
        _instanceMutex = new Mutex(true, "Local\\SphereSingleInstance", out var firstInstance);
        if (!firstInstance)
        {
            if (!background) _showRequest.Set();
            Current.Exit();
            return;
        }
        _window = new MainWindow();
        CurrentWindow = _window;
        Voice = new VoiceController((MainWindow)_window, AssistantSettings.Load());
        _window.Activate();
        ((MainWindow)_window).InitializePresentation();
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Environment.ProcessPath)!, "Assets", "AppIcon.ico")),
            Text = "Сфера",
            ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(),
            Visible = true
        };
        _tray.ContextMenuStrip.Items.Add("Открыть основное окно", null, (_, _) =>
            _window.DispatcherQueue.TryEnqueue(() => ((MainWindow)_window).Restore()));
        _tray.ContextMenuStrip.Items.Add("Закрыть приложение", null, async (_, _) => await QuitAsync());
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                _tray.ContextMenuStrip.Show(System.Windows.Forms.Cursor.Position);
        };
        if (background)
            ((MainWindow)_window).HideToTray();
        _ = Task.Run(() =>
        {
            while (true)
            {
                _showRequest.WaitOne();
                _window.DispatcherQueue.TryEnqueue(() => ((MainWindow)_window).Restore());
            }
        });
        StartVoice();
    }

    public void ShowOrb(OrbCorner corner)
    {
        _orb ??= new OrbWindow();
        _orb.ShowAt(corner);
    }

    public void HideOrb() => _orb?.Hide();

    public static async Task QuitAsync()
    {
        var app = (App)Current;
        var window = (MainWindow)CurrentWindow!;
        window.IsQuitting = true;
        app._tray?.ContextMenuStrip?.Dispose();
        app._tray?.Dispose();
        await Voice.ShutdownAsync();
        window.DispatcherQueue.TryEnqueue(() =>
        {
            app._orb?.AppWindow.Destroy();
            window.Close();
            Current.Exit();
        });
    }

    private async void StartVoice()
    {
        try { await Voice.ConfigureAsync(Voice.Settings); }
        catch (Exception) { /* ConfigureAsync already logged the error and updated the status. */ }
#if DEBUG
        if (Environment.GetCommandLineArgs().Contains("--check-wake-timeout"))
        {
            await Task.Delay(3000);
            Voice.SimulateWakeWordForCheck();
        }
#endif
    }
}
