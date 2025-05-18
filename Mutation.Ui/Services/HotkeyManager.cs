using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Windows.System;
using System.Threading;

namespace Mutation.Ui.Services;

public class Hotkey
{
    public bool Alt { get; set; }
    public bool Control { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }
    public VirtualKey Key { get; set; }

    public static Hotkey Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Invalid hotkey", nameof(text));

        var parts = text.Split(new[] { '+', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var hk = new Hotkey();
        foreach (var p in parts)
        {
            var token = p.Trim().ToUpperInvariant();
            switch (token)
            {
                case "CTRL":
                case "CONTROL":
                    hk.Control = true; break;
                case "ALT":
                    hk.Alt = true; break;
                case "SHIFT":
                case "SHFT":
                    hk.Shift = true; break;
                case "WIN":
                case "WINDOWS":
                case "START":
                    hk.Win = true; break;
                default:
                    if (Enum.TryParse<VirtualKey>(token, true, out var vk))
                        hk.Key = vk;
                    else if (Enum.TryParse<VirtualKey>("Number" + token, true, out vk))
                        hk.Key = vk;
                    else
                        throw new NotSupportedException($"Unsupported key '{token}'");
                    break;
            }
        }
        return hk;
    }
}

public class HotkeyManager : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, Action> _callbacks = new();
    private int _id;
    private IntPtr _prevWndProc;
    private WndProcDelegate? _newWndProc;

    private const int WM_HOTKEY = 0x0312;
    private const int GWLP_WNDPROC = -4;

    private const uint MOD_ALT = 0x1;
    private const uint MOD_CONTROL = 0x2;
    private const uint MOD_SHIFT = 0x4;
    private const uint MOD_WIN = 0x8;

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);
    [DllImport("user32.dll")] static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public HotkeyManager(Window window)
    {
        _hwnd = WindowNative.GetWindowHandle(window);
        _newWndProc = WndProc;
        _prevWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
    }

    public int RegisterHotkey(Hotkey hotkey, Action callback)
    {
        int id = Interlocked.Increment(ref _id);
        uint mods = (hotkey.Alt ? MOD_ALT : 0) |
                    (hotkey.Control ? MOD_CONTROL : 0) |
                    (hotkey.Shift ? MOD_SHIFT : 0) |
                    (hotkey.Win ? MOD_WIN : 0);
        RegisterHotKey(_hwnd, id, mods, (uint)hotkey.Key);
        _callbacks[id] = callback;
        return id;
    }

    public void UnregisterAll()
    {
        foreach (var kvp in _callbacks)
            UnregisterHotKey(_hwnd, kvp.Key);
        _callbacks.Clear();
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_callbacks.TryGetValue(id, out var cb))
                cb();
            return IntPtr.Zero;
        }
        return CallWindowProc(_prevWndProc, hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        UnregisterAll();
        if (_prevWndProc != IntPtr.Zero)
            SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _prevWndProc);
    }
}
