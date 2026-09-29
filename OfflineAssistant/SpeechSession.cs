using System.Threading.Channels;
using NAudio.Wave;
using SherpaOnnx;

namespace OfflineAssistant;

public sealed class SpeechSession
{
    private readonly Action<string, bool> _onText;
    private readonly Channel<float[]> _audio = Channel.CreateUnbounded<float[]>();
    private WaveIn? _microphone;
    private Task? _decodeTask;

    private readonly int _microphoneDevice;

    public SpeechSession(Action<string, bool> onText, int microphoneDevice = -1)
    {
        _onText = onText;
        _microphoneDevice = microphoneDevice;
    }

    public async Task StartAsync()
    {
        var model = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "Model");
        string FileAt(string path) => Path.Combine(model, path);
        var encoder = FileAt("am-onnx/encoder.int8.onnx");
        var decoder = FileAt("am-onnx/decoder.int8.onnx");
        var joiner = FileAt("am-onnx/joiner.int8.onnx");
        var tokens = FileAt("lang/tokens.txt");
        foreach (var path in new[] { encoder, decoder, joiner, tokens })
            if (!File.Exists(path)) throw new FileNotFoundException("Файл модели не найден", path);

        var config = new OnlineRecognizerConfig();
        config.FeatConfig.SampleRate = 16000;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Transducer.Encoder = encoder;
        config.ModelConfig.Transducer.Decoder = decoder;
        config.ModelConfig.Transducer.Joiner = joiner;
        config.ModelConfig.Tokens = tokens;
        config.ModelConfig.NumThreads = 2;
        config.EnableEndpoint = 1;
        config.Rule2MinTrailingSilence = 0.8f;

        var recognizer = await Task.Run(() => new OnlineRecognizer(config));
        try
        {
            if (WaveIn.DeviceCount == 0) throw new InvalidOperationException("Микрофон не найден. Подключите его и проверьте доступ в настройках Windows.");
            var device = _microphoneDevice < WaveIn.DeviceCount ? _microphoneDevice : -1;
            if (device != _microphoneDevice) AppLog.Warning("Выбранный микрофон недоступен; используется устройство по умолчанию");
            _microphone = new WaveIn { DeviceNumber = device, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };
            _microphone.DataAvailable += (_, e) =>
            {
                var samples = new float[e.BytesRecorded / 2];
                for (var i = 0; i < samples.Length; i++)
                    samples[i] = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
                _audio.Writer.TryWrite(samples);
            };
            _microphone.RecordingStopped += (_, e) =>
            {
                if (e.Exception is not null) AppLog.Error("Микрофон остановлен с ошибкой", e.Exception);
                _audio.Writer.TryComplete(e.Exception);
            };
            try { _microphone.StartRecording(); }
            catch (NAudio.MmException ex)
            {
                throw new InvalidOperationException("Не удалось открыть микрофон. Проверьте доступ к микрофону для классических приложений в настройках Windows.", ex);
            }
            _decodeTask = Task.Run(async () =>
            {
                try
                {
                    using (recognizer)
                    using (var stream = recognizer.CreateStream())
                    {
                        await foreach (var samples in _audio.Reader.ReadAllAsync())
                        {
                            stream.AcceptWaveform(16000, samples);
                            while (recognizer.IsReady(stream)) recognizer.Decode(stream);
                            var text = recognizer.GetResult(stream).Text.Trim();
                            if (recognizer.IsEndpoint(stream))
                            {
                                if (text.Length > 0) _onText(text, true);
                                recognizer.Reset(stream);
                            }
                            else _onText(text, false);
                        }
                        stream.InputFinished();
                        while (recognizer.IsReady(stream)) recognizer.Decode(stream);
                        var last = recognizer.GetResult(stream).Text.Trim();
                        if (last.Length > 0) _onText(last, true);
                    }
                }
                catch (Exception ex) { AppLog.Error("Фоновое распознавание", ex); throw; }
            });
        }
        catch
        {
            _microphone?.Dispose();
            recognizer.Dispose();
            throw;
        }
    }

    public async Task StopAsync()
    {
        var microphone = _microphone;
        try
        {
            microphone?.StopRecording();
            if (_decodeTask is not null) await _decodeTask;
        }
        finally
        {
            microphone?.Dispose();
            _microphone = null;
        }
    }
}
