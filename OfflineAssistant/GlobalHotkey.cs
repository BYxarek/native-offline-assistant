using System.ComponentModel;
using System.Runtime.InteropServices;

namespace OfflineAssistant;

public sealed class GlobalHotkey : IDisposable
{
    private readonly Thread _thread;
    private uint _threadId;

    private GlobalHotkey(uint modifier, uint key, Action pressed, TaskCompletionSource started)
    {
        _thread = new Thread(() => Run(modifier, key, pressed, started)) { IsBackground = true, Name = "Sphere hotkey" };
        _thread.Start();
    }

    public static async Task<GlobalHotkey> StartAsync(string modifier, string key, Action pressed)
    {
        var modifiers = modifier switch { "Ctrl" => 2u, "Alt" => 1u, "Shift" => 4u, "None" => 0u, _ => throw new ArgumentException("Неизвестная первая клавиша") };
        uint virtualKey = key switch
        {
            "Space" => 0x20,
            _ when key.Length == 1 && key[0] is >= 'A' and <= 'Z' => key[0],
            _ when key.StartsWith('F') && int.TryParse(key[1..], out var n) && n is >= 1 and <= 12 => (uint)(0x70 + n - 1),
            _ => throw new ArgumentException("Неизвестная клавиша")
        };
        if (modifiers == 0 && virtualKey is < 0x70 or > 0x7B)
            throw new ArgumentException("Для одной клавиши выберите F1–F12");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hotkey = new GlobalHotkey(modifiers | 0x4000, virtualKey, pressed, started);
        await started.Task;
        return hotkey;
    }

    private void Run(uint modifier, uint key, Action pressed, TaskCompletionSource started)
    {
        _threadId = GetCurrentThreadId();
        if (!RegisterHotKey(IntPtr.Zero, 1, modifier, key))
        {
            started.SetException(new Win32Exception(Marshal.GetLastWin32Error(), "Сочетание клавиш уже занято"));
            return;
        }
        started.SetResult();
        try
        {
            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                if (message.id == 0x0312) pressed(); // WM_HOTKEY
        }
        finally { UnregisterHotKey(IntPtr.Zero, 1); }
    }

    public void Dispose()
    {
        if (_threadId == 0) return;
        PostThreadMessage(_threadId, 0x0012, IntPtr.Zero, IntPtr.Zero); // WM_QUIT
        _thread.Join(2000);
        _threadId = 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr hwnd;
        public uint id;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int x;
        public int y;
        public uint privateData;
    }

    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] private static extern int GetMessage(out Message message, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);
}
