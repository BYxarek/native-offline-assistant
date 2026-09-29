using System.Text.Json;
using System.Text.RegularExpressions;

namespace OfflineAssistant;

public enum ActivationMode { WakeWord, Hotkey }
public enum OrbCorner { TopLeft, TopRight, BottomLeft, BottomRight }

public sealed class AssistantSettings
{
    public ActivationMode Mode { get; set; } = ActivationMode.WakeWord;
    public string WakeWord { get; set; } = "сфера";
    public string Modifier { get; set; } = "None";
    public string Key { get; set; } = "F8";
    // ponytail: WaveIn indices can change after devices are reordered; use stable endpoint IDs if that becomes a problem.
    public int MicrophoneDevice { get; set; } = -1;
    public bool ShowOrb { get; set; } = true;
    public OrbCorner Corner { get; set; } = OrbCorner.BottomRight;
    public bool WaitForNext { get; set; }
    public int WaitSeconds { get; set; } = 5;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sphere", "settings.json");

    public static AssistantSettings Load()
    {
        if (!File.Exists(FilePath)) return new();
        try
        {
            var settings = JsonSerializer.Deserialize<AssistantSettings>(File.ReadAllText(FilePath)) ?? new();
            if (Regex.IsMatch(settings.WakeWord ?? "", @"^[\p{L}\p{Nd}-]{1,32}$")
                && Enum.IsDefined(settings.Corner) && settings.WaitSeconds is >= 1 and <= 120
                && settings.MicrophoneDevice >= -1) return settings;
            AppLog.Warning("Некорректные настройки; загружены значения по умолчанию");
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            AppLog.Error("Не удалось загрузить настройки", ex);
        }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this));
        File.Move(temporary, FilePath, true);
    }
}
