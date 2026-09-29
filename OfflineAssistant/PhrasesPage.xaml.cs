using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OfflineAssistant;

public sealed partial class PhrasesPage : Page
{
    public sealed record Phrase(Guid Id, string Text, string Speaker);
    private static readonly string[] Speakers = ["xenia", "kseniya", "baya", "aidar", "eugene"];
    private readonly List<Phrase> _phrases = [];
    private bool _busy;
    private static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sphere", "phrases");
    private static string FilePath => Path.Combine(DirectoryPath, "phrases.json");
    private static string AudioPath(Guid id) => Path.Combine(DirectoryPath, $"{id:N}.wav");

    public PhrasesPage() => InitializeComponent();

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (File.Exists(FilePath)) _phrases.AddRange(JsonSerializer.Deserialize<List<Phrase>>(File.ReadAllText(FilePath)) ?? []);
            Render();
        }
        catch (Exception ex)
        {
            AppLog.Error("Загрузка фраз", ex);
            PhraseStatus.Text = "Не удалось загрузить сохранённые фразы: " + ex.Message;
            SynthesizeButton.IsEnabled = false;
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_phrases));
        File.Move(temporary, FilePath, true);
    }

    private async void SynthesizeButton_Click(object sender, RoutedEventArgs e)
    {
        var text = PhraseBox.Text.Trim();
        if (text.Length == 0) { PhraseStatus.Text = "Введите текст фразы."; return; }
        if (_busy) return;
        var speaker = SpeakerBox.SelectedItem?.ToString() ?? Speakers[0];
        var phrase = new Phrase(Guid.NewGuid(), text, speaker);
        var path = AudioPath(phrase.Id);
        _busy = true;
        Render();
        PhraseStatus.Text = "Создаю речь…";
        var resumeCapture = false;
        try
        {
            resumeCapture = await App.Voice.PauseCaptureAsync();
            Directory.CreateDirectory(DirectoryPath);
            await SileroTts.SynthesizeAsync(text, speaker, path);
            _phrases.Add(phrase);
            try { Save(); }
            catch { _phrases.Remove(phrase); throw; }
            PhraseBox.Text = "";
            Render();
            PhraseStatus.Text = "Фраза сохранена.";
            await SileroTts.PlayAsync(path);
        }
        catch (Exception ex)
        {
            var saved = _phrases.Contains(phrase);
            if (!saved && File.Exists(path))
                try { File.Delete(path); }
                catch (Exception cleanup) { AppLog.Error("Очистка незавершённой записи", cleanup); }
            AppLog.Error("Озвучивание фразы", ex);
            PhraseStatus.Text = (saved ? "Фраза сохранена, но воспроизвести её не удалось: " : "Не удалось озвучить: ") + ex.Message;
        }
        finally { await FinishAsync(resumeCapture); }
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not Button button || button.Tag is not Phrase phrase) return;
        _busy = true;
        Render();
        PhraseStatus.Text = "Воспроизвожу…";
        var resumeCapture = false;
        try
        {
            resumeCapture = await App.Voice.PauseCaptureAsync();
            await SileroTts.PlayAsync(AudioPath(phrase.Id));
            PhraseStatus.Text = "Воспроизведено.";
        }
        catch (Exception ex) { AppLog.Error("Воспроизведение фразы", ex); PhraseStatus.Text = "Не удалось воспроизвести: " + ex.Message; }
        finally { await FinishAsync(resumeCapture); }
    }

    private async Task FinishAsync(bool resumeCapture)
    {
        if (resumeCapture)
            try { await App.Voice.ConfigureAsync(App.Voice.Settings); }
            catch (Exception ex) { AppLog.Error("Возобновление прослушивания после озвучивания", ex); PhraseStatus.Text += " Микрофон не возобновлён: " + ex.Message; }
        _busy = false;
        Render();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not Button button || button.Tag is not Phrase phrase) return;
        var index = _phrases.IndexOf(phrase);
        if (index < 0) return;
        _phrases.RemoveAt(index);
        try { Save(); }
        catch (Exception ex) { _phrases.Insert(index, phrase); AppLog.Error("Удаление фразы", ex); PhraseStatus.Text = ex.Message; return; }
        try { File.Delete(AudioPath(phrase.Id)); PhraseStatus.Text = "Фраза удалена."; }
        catch (Exception ex) { AppLog.Error("Удаление аудиофайла", ex); PhraseStatus.Text = "Фраза удалена из списка, но аудиофайл не удалён: " + ex.Message; }
        Render();
    }

    private void Render()
    {
        SynthesizeButton.IsEnabled = !_busy;
        PhraseList.Children.Clear();
        foreach (var speaker in Speakers)
        {
            var phrases = _phrases.Where(p => p.Speaker == speaker).ToList();
            if (phrases.Count == 0) continue;
            PhraseList.Children.Add(new TextBlock { Text = speaker, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 0) });
            foreach (var phrase in phrases)
            {
                var row = new StackPanel { Spacing = 10 };
                row.Children.Add(new TextBlock { Text = phrase.Text, TextWrapping = TextWrapping.Wrap });
                var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                var play = new Button { Content = "▶ Прослушать", Tag = phrase, IsEnabled = !_busy };
                play.Click += PlayButton_Click;
                var delete = new Button { Content = "Удалить", Tag = phrase, IsEnabled = !_busy };
                delete.Click += DeleteButton_Click;
                controls.Children.Add(play);
                controls.Children.Add(delete);
                row.Children.Add(controls);
                PhraseList.Children.Add(new Border { Style = (Style)Application.Current.Resources["GlassCardStyle"], Padding = new Thickness(18, 14, 18, 14), Child = row });
            }
        }
    }
}
