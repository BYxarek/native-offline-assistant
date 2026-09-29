using OfflineAssistant;
using System.ComponentModel;

using (var hotkey = await GlobalHotkey.StartAsync("None", "F8", () => { }))
{
    try
    {
        using var duplicate = await GlobalHotkey.StartAsync("None", "F8", () => { });
        throw new Exception("Duplicate hotkey registration unexpectedly succeeded");
    }
    catch (Win32Exception) { }
}
using var again = await GlobalHotkey.StartAsync("Ctrl", "F8", () => { });
Console.WriteLine("Hotkey registration and release: OK");
