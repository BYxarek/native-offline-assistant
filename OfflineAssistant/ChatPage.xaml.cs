using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace OfflineAssistant;

public sealed partial class ChatPage : Page
{
    public ChatPage() => InitializeComponent();

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        foreach (var text in App.Voice.History) ShowResult(text, true);
        ShowResult(App.Voice.Partial, false);
        App.Voice.TranscriptReceived += ShowResult;
        App.Voice.StateChanged += RefreshState;
        RefreshState();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        App.Voice.TranscriptReceived -= ShowResult;
        App.Voice.StateChanged -= RefreshState;
    }

    private async void MicButton_Click(object sender, RoutedEventArgs e)
    {
        MicButton.IsEnabled = false;
        try { await App.Voice.ToggleAsync(); }
        catch (Exception ex) { AppLog.Error("Запуск прослушивания", ex); StatusText.Text = "Ошибка: " + ex.Message; }
        finally { MicButton.IsEnabled = true; }
    }

    private void RefreshState()
    {
        StatusText.Text = App.Voice.Status;
        var active = App.Voice.IsActive;
        var settings = App.Voice.Settings;
        var wakeWord = settings.Mode == ActivationMode.WakeWord;
        var trigger = settings.Modifier == "None" ? settings.Key : $"{settings.Modifier}+{settings.Key}";
        WelcomeText.Text = wakeWord
            ? $"Скажите «{settings.WakeWord}», чтобы начать запись речи."
            : $"Нажмите {trigger}, чтобы начать запись речи.";
        InstructionText.Text = active ? (wakeWord ? "Нажмите кнопку, чтобы остановить запись" : $"Нажмите {trigger} или кнопку, чтобы остановить запись")
            : wakeWord ? $"Скажите «{settings.WakeWord}» или нажмите кнопку"
            : $"Нажмите {trigger} или кнопку";
        MicButton.Content = active ? "■  Остановить" : "●  Слушать сейчас";
        AutomationProperties.SetName(MicButton, active ? "Остановить запись" : "Начать запись с микрофона");
    }

    private void ChatRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 600;
        ChatRoot.Padding = compact ? new Thickness(16, 16, 16, 16) : new Thickness(40, 32, 40, 28);
        LanguageBadge.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetColumn(MicButton, compact ? 0 : 1);
        Grid.SetRow(MicButton, compact ? 1 : 0);
        MicButton.Margin = compact ? new Thickness(0, 12, 0, 0) : new Thickness(14, 0, 0, 0);
        MicButton.HorizontalAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
    }

    private void ShowResult(string text, bool final)
    {
        if (final)
        {
            PartialBubble.Visibility = Visibility.Collapsed;
            PartialText.Text = "";
            if (string.IsNullOrWhiteSpace(text)) return;
            var bubble = new Border
            {
                Style = (Style)Application.Current.Resources["GlassCardStyle"],
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(18, 14, 18, 14),
                HorizontalAlignment = HorizontalAlignment.Right,
                MaxWidth = 510,
                Child = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 15 }
            };
            Transcript.Children.Insert(Transcript.Children.Count - 1, bubble);
        }
        else
        {
            PartialText.Text = text;
            PartialBubble.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
        }
        TranscriptScroll.ChangeView(null, TranscriptScroll.ScrollableHeight, null);
    }
}
