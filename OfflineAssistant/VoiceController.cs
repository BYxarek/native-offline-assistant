using System.Text.RegularExpressions;

namespace OfflineAssistant;

public sealed class VoiceController
{
    private readonly MainWindow _window;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SpeechSession? _session;
    private GlobalHotkey? _hotkey;
    private int _generation;
    private CancellationTokenSource? _waitTimer;
    private int _waitDurationSeconds;

    public AssistantSettings Settings { get; private set; }
    public string Status { get; private set; } = "Загрузка модели…";
    public bool IsActive { get; private set; }
    public List<string> History { get; } = [];
    public string Partial { get; private set; } = "";
    public event Action<string, bool>? TranscriptReceived;
    public event Action? StateChanged;

    public VoiceController(MainWindow window, AssistantSettings settings)
    {
        _window = window;
        Settings = settings;
    }

    public async Task ConfigureAsync(AssistantSettings settings)
    {
        await _gate.WaitAsync();
        try
        {
            CancelWait();
            ((App)Microsoft.UI.Xaml.Application.Current).HideOrb();
            _generation++;
            _hotkey?.Dispose();
            _hotkey = null;
            if (_session is not null) await _session.StopAsync();
            _session = null;
            IsActive = false;
            Partial = "";
            Settings = settings;
            Status = "Загрузка модели…";
            StateChanged?.Invoke();
            if (settings.Mode == ActivationMode.WakeWord)
            {
                await StartSessionAsync();
                Status = $"Жду слово «{settings.WakeWord}»";
            }
            else
            {
                _hotkey = await GlobalHotkey.StartAsync(settings.Modifier, settings.Key, () =>
                    _window.DispatcherQueue.TryEnqueue(async () =>
                    {
                        try { await ToggleAsync(); }
                        catch (Exception ex) { AppLog.Error("Горячая клавиша", ex); Status = ex.Message; StateChanged?.Invoke(); }
                    }));
                Status = settings.Modifier == "None" ? $"Ожидаю {settings.Key}" : $"Ожидаю {settings.Modifier}+{settings.Key}";
            }
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            AppLog.Error("Настройка голосового режима", ex);
            Status = "Режим не запущен: " + ex.Message;
            StateChanged?.Invoke();
            throw;
        }
        finally { _gate.Release(); }
    }

    public async Task ToggleAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (IsActive)
            {
                CancelWait();
                if (Settings.Mode == ActivationMode.Hotkey && _session is not null)
                {
                    await _session.StopAsync();
                    _session = null;
                }
                IsActive = false;
                Partial = "";
                TranscriptReceived?.Invoke("", false);
                Status = Settings.Mode == ActivationMode.WakeWord
                    ? $"Жду слово «{Settings.WakeWord}»" : "Ожидаю сочетание клавиш";
                RefreshOrb();
            }
            else
            {
                if (_session is null) await StartSessionAsync();
                IsActive = true;
                Status = "Слушаю вас…";
                if (!_window.IsHidden) _window.ShowChatFromActivation();
                RefreshOrb();
            }
            StateChanged?.Invoke();
        }
        finally { _gate.Release(); }
    }

    private async Task StartSessionAsync()
    {
        var generation = ++_generation;
        var session = new SpeechSession((text, final) =>
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (generation == _generation) OnSpeech(text, final);
            }), Settings.MicrophoneDevice);
        await session.StartAsync();
        _session = session;
    }

#if DEBUG
    internal void SimulateWakeWordForCheck() => OnSpeech(Settings.WakeWord, true);
#endif

    private void OnSpeech(string text, bool final)
    {
        if (!IsActive)
        {
            if (Settings.Mode != ActivationMode.WakeWord || !final) return;
            var match = Regex.Match(text, $@"(?<!\p{{L}}){Regex.Escape(Settings.WakeWord)}(?!\p{{L}})", RegexOptions.IgnoreCase);
            if (!match.Success) return;
            IsActive = true;
            Status = "Слушаю вас…";
            StateChanged?.Invoke();
            if (!_window.IsHidden) _window.ShowActivationFeedback();
            RefreshOrb();
            text = text[(match.Index + match.Length)..].Trim(' ', ',', '.', '!', '?');
            BeginWait(5);
            if (text.Length == 0) return;
        }

        if (final)
        {
            Partial = "";
            if (string.IsNullOrWhiteSpace(text)) return;
            CancelWait();
            History.Add(text);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(text) && _waitTimer is not null) BeginWait(_waitDurationSeconds);
            Partial = text;
        }
        TranscriptReceived?.Invoke(text, final);
        if (final)
        {
            if (Settings.WaitForNext) BeginWait(Settings.WaitSeconds);
            else _ = ToggleAsync().ContinueWith(t => AppLog.Error("Остановка после распознавания", t.Exception!), TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    public void RefreshOrb()
    {
        var app = (App)Microsoft.UI.Xaml.Application.Current;
        if (IsActive && _window.IsHidden && Settings.ShowOrb) app.ShowOrb(Settings.Corner);
        else app.HideOrb();
    }

    public async Task<bool> PauseCaptureAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var wasRunning = _session is not null;
            _generation++;
            if (_session is not null) await _session.StopAsync();
            _session = null;
            return wasRunning;
        }
        finally { _gate.Release(); }
    }

    private void CancelWait()
    {
        _waitTimer?.Cancel();
        _waitTimer?.Dispose();
        _waitTimer = null;
    }

    private void BeginWait(int seconds)
    {
        CancelWait();
        _waitDurationSeconds = seconds;
        var timer = _waitTimer = new CancellationTokenSource();
        _ = WaitAsync(timer, seconds);
    }

    private async Task WaitAsync(CancellationTokenSource timer, int seconds)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds), timer.Token);
            _window.DispatcherQueue.TryEnqueue(async () =>
            {
                try { if (_waitTimer == timer && IsActive) await ToggleAsync(); }
                catch (Exception ex) { AppLog.Error("Остановка после ожидания", ex); }
            });
        }
        catch (TaskCanceledException) { }
    }

    public async Task ShutdownAsync()
    {
        await _gate.WaitAsync();
        try
        {
            CancelWait();
            _hotkey?.Dispose();
            if (_session is not null) await _session.StopAsync();
        }
        finally { _gate.Release(); }
    }
}
