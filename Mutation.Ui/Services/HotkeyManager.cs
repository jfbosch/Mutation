using Microsoft.UI.Xaml;
using Windows.System;

namespace Mutation.Ui.Services;

/// <summary>
/// Placeholder hotkey manager for WinUI. Global hotkeys require Win32 interop
/// which is not yet implemented.
/// </summary>
public class HotkeyManager
{
    public void Initialize(Window window) { /* TODO: implement global hotkey registration */ }

    public void RegisterHotkey(VirtualKey key, HotkeyModifiers modifiers, Action callback)
    {
        // TODO: implement
    }

    public void UnregisterAll() { }
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
