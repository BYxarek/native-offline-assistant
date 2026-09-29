using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace OfflineAssistant;

public sealed partial class MainWindow : Window
{
    public bool IsHidden { get; private set; }
    public bool IsQuitting { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragArea);
        AppWindow.SetIcon(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Environment.ProcessPath)!, "Assets", "AppIcon.ico"));
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1000, 700));
        RootFrame.Navigate(typeof(MainPage));
        AppWindow.Closing += (_, args) =>
        {
            if (IsQuitting) return;
            args.Cancel = true;
            HideToTray();
        };
    }

    public void InitializePresentation()
    {
        AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
    }

    public void HideToTray()
    {
        IsHidden = true;
        AppWindow.Hide();
        App.Voice?.RefreshOrb();
    }

    public void Restore()
    {
        IsHidden = false;
        AppWindow.Show();
        Activate();
        App.Voice?.RefreshOrb();
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e) => OpenHome();
    private void ChatButton_Click(object sender, RoutedEventArgs e) => OpenChat();
    private void PhrasesButton_Click(object sender, RoutedEventArgs e) => OpenPhrases();
    private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();
    private void CloseButton_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen) { HideToTray(); return; }
        (AppWindow.Presenter as OverlappedPresenter)?.Minimize();
    }

    private void WindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen) SetWindowedPresenter();
        else AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
    }

    private void SetWindowedPresenter()
    {
        AppWindow.SetPresenter(AppWindowPresenterKind.Default);
        (AppWindow.Presenter as OverlappedPresenter)?.SetBorderAndTitleBar(true, false);
    }

    public void OpenHome() => RootFrame.Navigate(typeof(MainPage));
    public void OpenChat() => RootFrame.Navigate(typeof(ChatPage));
    public void OpenPhrases() => RootFrame.Navigate(typeof(PhrasesPage));
    public void OpenSettings() => RootFrame.Navigate(typeof(SettingsPage));

    public void ShowChatFromActivation()
    {
        if (RootFrame.Content is not ChatPage) OpenChat();
        ActivationMotion.Begin();
    }

    public void ShowActivationFeedback() => ActivationMotion.Begin();
}
