using System.Diagnostics;
using NAudio.Wave;

namespace OfflineAssistant;

public static class SileroTts
{
    public static async Task SynthesizeAsync(string text, string speaker, string output)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Tts");
        var executable = Path.Combine(directory, "silero-tts", "silero-tts.exe");
        var model = Path.Combine(directory, "v5_5_ru.pt");
        if (!File.Exists(executable) || !File.Exists(model))
            throw new FileNotFoundException("Компоненты Silero не найдены. Установите приложение через инсталлер.");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                ArgumentList = { model, output, speaker },
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        await process.StandardInput.WriteAsync(text);
        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Синтез речи занял слишком много времени."); }
        var error = await process.StandardError.ReadToEndAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException("Silero: " + error.Trim());
    }

    public static async Task PlayAsync(string path)
    {
        using var reader = new AudioFileReader(path);
        using var player = new WaveOut();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        player.PlaybackStopped += (_, e) =>
        {
            if (e.Exception is null) finished.TrySetResult();
            else finished.TrySetException(e.Exception);
        };
        player.Init(reader);
        player.Play();
        await finished.Task;
    }
}
