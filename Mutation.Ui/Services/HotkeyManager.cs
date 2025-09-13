using CognitiveSupport;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinRT.Interop;

namespace Mutation.Ui.Services;

public class HotkeyManager : IDisposable
{
	private readonly IntPtr _hwnd;
	private readonly Dictionary<int, Action> _callbacks = new();
	private readonly List<int> _routerIds = new();
	private readonly Settings _settings;
	private int _id;
	private IntPtr _prevWndProc;
	private WndProcDelegate? _newWndProc;

	/// <summary>
	/// Gets the list of hotkeys that failed to register.
	/// </summary>
	public List<string> FailedRegistrations { get; } = new();

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
	[DllImport("user32.dll")] static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

	private const int INPUT_KEYBOARD = 1;
	private const uint KEYEVENTF_KEYUP = 0x0002;
	private const uint KEYEVENTF_UNICODE = 0x0004;

	[StructLayout(LayoutKind.Sequential)]
	private struct INPUT
	{
		public uint type;
		public INPUTUNION U;
	}

	[StructLayout(LayoutKind.Explicit)]
	private struct INPUTUNION
	{
		[FieldOffset(0)] public KEYBDINPUT ki;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct KEYBDINPUT
	{
		public ushort wVk;
		public ushort wScan;
		public uint dwFlags;
		public uint time;
		public IntPtr dwExtraInfo;
	}

	private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	public HotkeyManager(Window window, Settings settings)
	{
		_hwnd = WindowNative.GetWindowHandle(window);
		_settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
		bool success = RegisterHotKey(_hwnd, id, mods, (uint)hotkey.Key);
		if (success)
		{
			_callbacks[id] = callback;
		}
		else
		{
			FailedRegistrations.Add(hotkey.ToString());
		}
		return id;
	}

	public void RegisterRouterHotkeys()
	{
		foreach (var map in _settings.HotKeyRouterSettings.Mappings)
		{
			if (string.IsNullOrWhiteSpace(map.FromHotKey) || string.IsNullOrWhiteSpace(map.ToHotKey))
				continue;
			int id = RegisterHotkey(Hotkey.Parse(map.FromHotKey!), () => SendHotkeyAfterDelay(map.ToHotKey!, 25));
			_routerIds.Add(id);
		}
	}

	public void UnregisterAll()
	{
		foreach (var kvp in _callbacks)
			UnregisterHotKey(_hwnd, kvp.Key);
		_callbacks.Clear();
		_routerIds.Clear();
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

	public static void SendHotkeyAfterDelay(string hotkey, int delayMs)
	{
		if (string.IsNullOrWhiteSpace(hotkey))
			return;

		_ = Task.Run(async () =>
		{
			await Task.Delay(delayMs).ConfigureAwait(false);
			SendHotkey(hotkey);
		});
	}

	public static void SendHotkey(string hotkey)
	{
		if (string.IsNullOrWhiteSpace(hotkey))
			return;

		string mappedHotkey =  SendKeysMapper.Map(hotkey);

		SendKeys.SendWait(mappedHotkey);
	}

	public static void SendText(string text)
	{
		if (string.IsNullOrEmpty(text))
			return;

		List<INPUT> inputs = new();
		foreach (char c in text)
		{
			inputs.Add(new INPUT
			{
				type = INPUT_KEYBOARD,
				U = new INPUTUNION { ki = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE } }
			});
			inputs.Add(new INPUT
			{
				type = INPUT_KEYBOARD,
				U = new INPUTUNION { ki = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP } }
			});
		}
		SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
	}

	public void Dispose()
	{
		UnregisterAll();
		if (_prevWndProc != IntPtr.Zero)
			SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _prevWndProc);
	}
}
