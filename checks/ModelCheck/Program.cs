using SherpaOnnx;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../OfflineAssistant/Model"));
var config = new OnlineRecognizerConfig();
config.FeatConfig.SampleRate = 16000;
config.FeatConfig.FeatureDim = 80;
config.ModelConfig.Transducer.Encoder = Path.Combine(root, "am-onnx/encoder.int8.onnx");
config.ModelConfig.Transducer.Decoder = Path.Combine(root, "am-onnx/decoder.int8.onnx");
config.ModelConfig.Transducer.Joiner = Path.Combine(root, "am-onnx/joiner.int8.onnx");
config.ModelConfig.Tokens = Path.Combine(root, "lang/tokens.txt");

using var recognizer = new OnlineRecognizer(config);
using var stream = recognizer.CreateStream();
var wave = File.ReadAllBytes(Path.Combine(root, "test.wav"));
if (BitConverter.ToInt32(wave, 24) != 16000 || BitConverter.ToInt16(wave, 34) != 16)
    throw new Exception("Expected 16 kHz PCM16 test.wav");
var samples = new float[(wave.Length - 44) / 2];
for (var i = 0; i < samples.Length; i++)
    samples[i] = BitConverter.ToInt16(wave, 44 + i * 2) / 32768f;
stream.AcceptWaveform(16000, samples);
stream.InputFinished();
while (recognizer.IsReady(stream)) recognizer.Decode(stream);
var text = recognizer.GetResult(stream).Text;
if (!text.Contains("родион", StringComparison.OrdinalIgnoreCase))
    throw new Exception($"Unexpected transcript: {text}");
Console.WriteLine(text);
