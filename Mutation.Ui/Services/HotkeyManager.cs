using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Windows.System;

namespace Mutation.Ui.Services;

/// <summary>
/// Placeholder hotkey manager for WinUI. Global hotkeys require Win32 interop
/// which is not yet implemented.
/// </summary>
public class HotkeyManager
{
    private Window? _window;
    private IntPtr _hwnd;
    private WndProc? _newWndProc;
    private IntPtr _oldWndProc;
    private int _currentId = 0;
    private readonly Dictionary<int, Action> _callbacks = new();

    public void Initialize(Window window)
    {
        _window = window;
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _newWndProc = WndProc;
        _oldWndProc = SetWindowLongPtr(_hwnd, -4, Marshal.GetFunctionPointerForDelegate(_newWndProc));
    }

    public void RegisterHotkey(VirtualKey key, HotkeyModifiers modifiers, Action callback)
    {
        int id = ++_currentId;
        if (!RegisterHotKey(_hwnd, id, (uint)modifiers, (uint)key))
            throw new InvalidOperationException("Failed to register hotkey.");
        _callbacks[id] = callback;
    }

    public void UnregisterAll()
    {
        foreach (var id in _callbacks.Keys)
            UnregisterHotKey(_hwnd, id);
        _callbacks.Clear();
        if (_newWndProc != null)
            SetWindowLongPtr(_hwnd, -4, _oldWndProc);
    }

    private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_callbacks.TryGetValue(id, out var action))
                action();
        }
        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}
