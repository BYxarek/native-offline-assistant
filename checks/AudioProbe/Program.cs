using NAudio.CoreAudioApi;
using NAudio.Wave;

Console.WriteLine($"WaveIn devices: {WaveIn.DeviceCount}");
for (var i = 0; i < WaveIn.DeviceCount; i++)
    Console.WriteLine($"{i}: {WaveIn.GetCapabilities(i).ProductName}");

for (var device = 0; device < WaveIn.DeviceCount; device++)
{
    try
    {
        using var input = new WaveIn { DeviceNumber = device, WaveFormat = new WaveFormat(16000, 16, 1) };
        input.StartRecording();
        Thread.Sleep(500);
        input.StopRecording();
        Console.WriteLine($"WaveIn {device}: OK");
    }
    catch (Exception ex) { Console.WriteLine($"WaveIn {device}: {ex.GetType().Name}: {ex.Message}"); }
}

try
{
    using var input = new WasapiCapture(new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia));
    input.StartRecording();
    Thread.Sleep(500);
    input.StopRecording();
    Console.WriteLine("WASAPI: OK");
}
catch (Exception ex) { Console.WriteLine($"WASAPI: {ex.GetType().Name}: {ex.Message}"); }
