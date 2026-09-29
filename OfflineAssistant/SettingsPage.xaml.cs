using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NAudio.Wave;

namespace OfflineAssistant;

public sealed partial class SettingsPage : Page
{
    private WaveIn? _testMicrophone;
    private bool _testRecording;
    private bool _resumeCapture;

    public SettingsPage()
    {
        InitializeComponent();
        VersionText.Text = $"Версия {typeof(App).Assembly.GetName().Version!.ToString(3)}";
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = App.Voice.Settings;
        WakeMode.IsChecked = settings.Mode == ActivationMode.WakeWord;
        HotkeyMode.IsChecked = settings.Mode == ActivationMode.Hotkey;
        WakeWordBox.Text = settings.WakeWord;
        KeyCountBox.SelectedIndex = settings.Modifier == "None" ? 0 : 1;
        ModifierBox.SelectedItem = settings.Modifier == "None" ? "Ctrl" : settings.Modifier;
        FillKeys();
        KeyBox.SelectedItem = settings.Key;
        if (KeyBox.SelectedIndex < 0) KeyBox.SelectedIndex = 0;
        ShowOrbSwitch.IsOn = settings.ShowOrb;
        CornerBox.SelectedIndex = (int)settings.Corner;
        WaitSwitch.IsOn = settings.WaitForNext;
        WaitSecondsBox.Value = settings.WaitSeconds;
        FillMicrophones(settings.MicrophoneDevice);
        SettingsStatus.Text = App.Voice.Status;
    }

    private async void Page_Unloaded(object sender, RoutedEventArgs e) => await StopMicrophoneTestAsync();

    private void FillMicrophones(int selected)
    {
        var names = new List<string> { "По умолчанию" };
        try
        {
            for (var i = 0; i < WaveIn.DeviceCount; i++)
                names.Add(WaveIn.GetCapabilities(i).ProductName);
        }
        catch (Exception ex) { AppLog.Error("Список микрофонов", ex); MicrophoneTestStatus.Text = "Не удалось получить список микрофонов"; }
        MicrophoneBox.ItemsSource = names;
        MicrophoneBox.SelectedIndex = Math.Clamp(selected + 1, 0, names.Count - 1);
    }

    private async void MicrophoneTestButton_Click(object sender, RoutedEventArgs e)
    {
        if (_testMicrophone is not null) { await StopMicrophoneTestAsync(); return; }
        MicrophoneTestButton.IsEnabled = false;
        try
        {
            _resumeCapture = await App.Voice.PauseCaptureAsync();
            var microphone = new WaveIn
            {
                DeviceNumber = MicrophoneBox.SelectedIndex - 1,
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 100
            };
            microphone.DataAvailable += (_, data) =>
            {
                var peak = 0;
                for (var i = 0; i < data.BytesRecorded; i += 2)
                    peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(data.Buffer, i)));
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_testMicrophone == microphone) MicrophoneLevel.Value = peak * 100d / 32768;
                });
            };
            microphone.RecordingStopped += (_, stopped) =>
            {
                if (stopped.Exception is null) return;
                AppLog.Error("Проверка микрофона", stopped.Exception);
                DispatcherQueue.TryEnqueue(async () =>
                {
                    if (_testMicrophone != microphone) return;
                    MicrophoneTestStatus.Text = "Микрофон остановился с ошибкой: " + stopped.Exception.Message;
                    await StopMicrophoneTestAsync();
                });
            };
            _testMicrophone = microphone;
            microphone.StartRecording();
            _testRecording = true;
            MicrophoneTestButton.Content = "Остановить проверку";
            MicrophoneTestStatus.Text = "Говорите: индикатор показывает уровень звука";
        }
        catch (Exception ex)
        {
            AppLog.Error("Проверка микрофона", ex);
            MicrophoneTestStatus.Text = "Микрофон недоступен. Проверьте доступ для классических приложений в настройках Windows.";
            await StopMicrophoneTestAsync();
        }
        finally { MicrophoneTestButton.IsEnabled = true; }
    }

    private async Task StopMicrophoneTestAsync(bool resume = true)
    {
        var microphone = _testMicrophone;
        _testMicrophone = null;
        if (microphone is not null)
        {
            try { if (_testRecording) microphone.StopRecording(); }
            catch (Exception ex) { AppLog.Error("Остановка проверки микрофона", ex); }
            microphone.Dispose();
        }
        _testRecording = false;
        MicrophoneLevel.Value = 0;
        MicrophoneTestButton.Content = "Проверить микрофон";
        if (!_resumeCapture) return;
        _resumeCapture = false;
        if (!resume) return;
        try { await App.Voice.ConfigureAsync(App.Voice.Settings); }
        catch (Exception ex) { AppLog.Error("Возобновление прослушивания после проверки", ex); }
    }

    private void KeyCountBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => FillKeys();

    private void FillKeys()
    {
        if (KeyBox is null || ModifierBox is null || KeyCountBox is null) return;
        var old = KeyBox.SelectedItem as string;
        var two = KeyCountBox.SelectedIndex == 1;
        ModifierBox.IsEnabled = two;
        var keys = Enumerable.Range(1, 12).Select(x => $"F{x}").ToList();
        if (two)
        {
            keys.AddRange(Enumerable.Range('A', 26).Select(x => ((char)x).ToString()));
            keys.Add("Space");
        }
        KeyBox.ItemsSource = keys;
        KeyBox.SelectedItem = old is not null && keys.Contains(old) ? old : "F8";
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var word = WakeWordBox.Text.Trim();
        if (!Regex.IsMatch(word, @"^[\p{L}\p{Nd}-]{1,32}$"))
        {
            AppLog.Warning("Некорректное ключевое слово в настройках");
            SettingsStatus.Text = "Ключевое слово: от 1 до 32 букв, цифр или дефисов, без пробелов.";
            return;
        }
        if (double.IsNaN(WaitSecondsBox.Value) || WaitSecondsBox.Value != Math.Floor(WaitSecondsBox.Value)
            || WaitSecondsBox.Value is < 1 or > 120)
        {
            AppLog.Warning("Некорректное время ожидания в настройках");
            SettingsStatus.Text = "Время ожидания: целое число от 1 до 120 секунд.";
            return;
        }
        var settings = new AssistantSettings
        {
            Mode = HotkeyMode.IsChecked == true ? ActivationMode.Hotkey : ActivationMode.WakeWord,
            WakeWord = word,
            Modifier = KeyCountBox.SelectedIndex == 1 ? ModifierBox.SelectedItem?.ToString() ?? "Ctrl" : "None",
            Key = KeyBox.SelectedItem?.ToString() ?? "F8",
            MicrophoneDevice = MicrophoneBox.SelectedIndex - 1,
            ShowOrb = ShowOrbSwitch.IsOn,
            Corner = (OrbCorner)Math.Max(0, CornerBox.SelectedIndex),
            WaitForNext = WaitSwitch.IsOn,
            WaitSeconds = (int)WaitSecondsBox.Value
        };
        try
        {
            await StopMicrophoneTestAsync(false);
            settings.Save();
        }
        catch (Exception ex)
        {
            AppLog.Error("Сохранение настроек", ex);
            SettingsStatus.Text = "Не удалось сохранить настройки: " + ex.Message;
            return;
        }
        try
        {
            await App.Voice.ConfigureAsync(settings);
            SettingsStatus.Text = "Сохранено. " + App.Voice.Status;
        }
        catch (Exception ex)
        {
            SettingsStatus.Text = "Настройки сохранены, но режим не запущен: " + ex.Message;
        }
    }

    private async void QuitButton_Click(object sender, RoutedEventArgs e) => await App.QuitAsync();
}
