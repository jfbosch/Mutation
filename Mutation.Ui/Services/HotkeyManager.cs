using CognitiveSupport;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WinRT.Interop;
using System.IO;

namespace Mutation.Ui.Services;

public class HotkeyManager : IDisposable
{
	private readonly IntPtr _hwnd;
	private readonly Dictionary<int, Action> _callbacks = new();
	private readonly List<int> _routerIds = new();
	private readonly Settings _settings;
	private static SynchronizationContext? s_uiCtx;
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
	private const uint MOD_NOREPEAT = 0x4000;

	[DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
	[DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
	[DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);
	[DllImport("user32.dll")] static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
	[DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
	[DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
	[DllImport("user32.dll")] static extern uint MapVirtualKey(uint uCode, uint uMapType);

	private const uint MAPVK_VK_TO_VSC = 0x0;

	private const int INPUT_KEYBOARD = 1;
	private const uint KEYEVENTF_KEYUP = 0x0002;
	private const uint KEYEVENTF_UNICODE = 0x0004;
	private const uint KEYEVENTF_SCANCODE = 0x0008;
	private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

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
		// Capture UI thread context so we can run SendKeys fallback on STA with a message pump
		s_uiCtx ??= SynchronizationContext.Current;
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
			int id = RegisterHotkey(Hotkey.Parse(map.FromHotKey!), () => SendHotkeyAfterDelay(map.ToHotKey!, Constants.FailureSendHotkeyDelay));
			Log($"Router registered: From='{map.FromHotKey}' -> To='{map.ToHotKey}', id={id}");
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
			Log($"WM_HOTKEY received: id={id}");
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
			Log($"SendHotkeyAfterDelay firing: '{hotkey}'");
			SendHotkey(hotkey);
		});
	}

	public static void SendHotkey(string hotkey)
	{
		if (string.IsNullOrWhiteSpace(hotkey))
			return;

		// Quick override for diagnostics: set MUTATION_FORCE_SENDKEYS=1 to bypass SendInput
		if (Environment.GetEnvironmentVariable("MUTATION_FORCE_SENDKEYS") == "1")
		{
			try
			{
				string mappedHotkey = SendKeysMapper.Map(hotkey);
				Log($"ENV override: Fallback SendKeys: '{mappedHotkey}' (from '{hotkey}')");
				SendKeysOnUiThread(mappedHotkey);
				return;
			}
			catch { /* ignore */ }
		}

		// Support sequences like "Ctrl+C, Ctrl+V"
		var parts = hotkey.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length == 0)
			return;

		bool allSentViaInput = true;
		foreach (var part in parts)
		{
			try
			{
				var hk = Hotkey.Parse(part);
				Log($"Sending via SendInput: '{part}'");
				bool ok = SendHotkeyViaSendInput(hk);
				if (!ok)
				{
					Log($"SendInput failed for '{part}', falling back to SendKeys mapping.");
					allSentViaInput = false;
					break;
				}
				// small gap between chords
				Thread.Sleep(25);
			}
			catch
			{
				allSentViaInput = false;
				break;
			}
		}

		if (!allSentViaInput)
		{
			// Fallback: try WinForms SendKeys mapping for complex/unsupported chords
			try
			{
				string mappedHotkey = SendKeysMapper.Map(hotkey);
				Log($"Fallback SendKeys: '{mappedHotkey}' (from '{hotkey}')");
				SendKeysOnUiThread(mappedHotkey);
			}
			catch { /* give up silently */ }
		}
	}

	private static void SendKeysOnUiThread(string mapped)
	{
		// This used to block the calling thread with a ManualResetEvent and a 5s timeout
		// which consistently timed out (delegate never executed on expected context).
		// We now simply marshal to the captured SynchronizationContext (if any) in a
		// fire-and-forget manner; if none is available we execute inline. This avoids
		// deadlocks / timeouts and is more in line with modern async patterns.
		try
		{
			if (string.IsNullOrEmpty(mapped)) return;
			// If no UI context was captured or we're already on it, just send directly
			if (s_uiCtx is null || SynchronizationContext.Current == s_uiCtx)
			{
				System.Windows.Forms.SendKeys.SendWait(mapped);
				return;
			}
			// Post asynchronously; no need to wait/block.
			_ = PostSendKeysAsync(mapped);
		}
		catch { /* swallow intentionally as this is a best-effort fallback path */ }
	}

	private static Task PostSendKeysAsync(string mapped)
	{
		var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		try
		{
			s_uiCtx!.Post(_ =>
			{
				try
				{
					System.Windows.Forms.SendKeys.SendWait(mapped);
					tcs.SetResult(null);
				}
				catch (Exception ex)
				{
					// Do not rethrow; log if desired.
					Log($"SendKeys (fallback) failed: {ex.Message}");
					if (!tcs.Task.IsCompleted) tcs.SetException(ex);
				}
			}, null);
		}
		catch (Exception ex)
		{
			Log($"Failed to post SendKeys: {ex.Message}");
			if (!tcs.Task.IsCompleted) tcs.SetException(ex);
		}
		return tcs.Task;
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

	private static bool SendHotkeyViaSendInput(Hotkey hotkey)
	{
		// Wait until user releases modifier keys from the original chord to avoid contamination
		WaitForModifierRelease(timeoutMs: 200);

		var inputs = new List<INPUT>();

		bool needCtrl = hotkey.Control;
		bool needShift = hotkey.Shift;
		bool needAlt = hotkey.Alt;
		bool needWin = hotkey.Win;

		// If any physical modifiers are still down and not needed for the target chord, release them first
		if (!needShift && (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0)
			inputs.Add(KeyUp(VK_SHIFT));
		if (!needAlt && (GetAsyncKeyState(VK_MENU) & 0x8000) != 0)
			inputs.Add(KeyUp(VK_MENU));
		if (!needWin && (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0)
			inputs.Add(KeyUp(VK_LWIN));
		if (!needCtrl && (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0)
			inputs.Add(KeyUp(VK_CONTROL));

		if (inputs.Count > 0)
		{
			var preSent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
			Log($"Pre-release injected {preSent}/{inputs.Count} modifier key-ups.");
			inputs.Clear();
			Thread.Sleep(10);
		}

		// Press modifiers (Ctrl, Shift, Alt) down in canonical order
		if (needCtrl) inputs.Add(KeyDown(VK_CONTROL));
		if (needShift) inputs.Add(KeyDown(VK_SHIFT));
		if (needAlt) inputs.Add(KeyDown(VK_MENU));
		if (needWin) inputs.Add(KeyDown(VK_LWIN));

		// Press primary key
		inputs.Add(KeyDown((ushort)hotkey.Key));
		// Release primary key
		inputs.Add(KeyUp((ushort)hotkey.Key));

		// Release modifiers in reverse order
		if (hotkey.Win) inputs.Add(KeyUp(VK_LWIN));
		if (hotkey.Alt) inputs.Add(KeyUp(VK_MENU));
		if (hotkey.Shift) inputs.Add(KeyUp(VK_SHIFT));
		if (hotkey.Control) inputs.Add(KeyUp(VK_CONTROL));

		var count = (uint)inputs.Count;
		var sent = SendInput(count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
		bool ok = sent == count && count > 0;
		if (!ok)
		{
			int err = Marshal.GetLastWin32Error();
			Log($"SendInput returned {sent}/{count}, GetLastError={err}");
		}
		return ok;
	}

	// Virtual-key codes for common modifiers
	private const ushort VK_CONTROL = 0x11;
	private const ushort VK_SHIFT = 0x10;
	private const ushort VK_MENU = 0x12; // ALT
	private const ushort VK_LWIN = 0x5B;

	private static INPUT KeyDown(ushort vk)
	{
		return new INPUT
		{
			type = INPUT_KEYBOARD,
			U = new INPUTUNION
			{
				ki = new KEYBDINPUT
				{
					wVk = vk,
					wScan = 0,
					dwFlags = (IsExtended(vk) ? KEYEVENTF_EXTENDEDKEY : 0),
					time = 0,
					dwExtraInfo = IntPtr.Zero
				}
			}
		};
	}

	private static INPUT KeyUp(ushort vk)
	{
		return new INPUT
		{
			type = INPUT_KEYBOARD,
			U = new INPUTUNION
			{
				ki = new KEYBDINPUT
				{
					wVk = vk,
					wScan = 0,
					dwFlags = KEYEVENTF_KEYUP | (IsExtended(vk) ? KEYEVENTF_EXTENDEDKEY : 0),
					time = 0,
					dwExtraInfo = IntPtr.Zero
				}
			}
		};
	}

	private static bool IsExtended(ushort vk)
	{
		// Extended key flag for navigation cluster and some others
		return vk is 0x21 /*PGUP*/ or 0x22 /*PGDN*/ or 0x23 /*END*/ or 0x24 /*HOME*/
			   or 0x25 /*LEFT*/ or 0x26 /*UP*/ or 0x27 /*RIGHT*/ or 0x28 /*DOWN*/
			   or 0x2D /*INSERT*/ or 0x2E /*DELETE*/ or 0x5B /*LWIN*/ or 0x5C /*RWIN*/
			   or 0xA1 /*RSHIFT*/ or 0xA3 /*RCONTROL*/ or 0xA5 /*RMENU(ALT)*/;
	}

	private static void WaitForModifierRelease(int timeoutMs)
	{
		var sw = System.Diagnostics.Stopwatch.StartNew();
		while (sw.ElapsedMilliseconds < timeoutMs)
		{
			// High-order bit set means key is down
			bool ctrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
			bool shiftDown = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
			bool altDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
			bool winDown = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0;

			if (!(ctrlDown || shiftDown || altDown || winDown))
				break;

			Thread.Sleep(10);
		}
	}

	private static readonly string LogFile = Path.Combine(Path.GetTempPath(), "Mutation.Hotkey.log");
	private static void Log(string message)
	{
		try
		{
			var line = $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}";
			File.AppendAllText(LogFile, line);
		}
		catch { }
	}

	public void Dispose()
	{
		UnregisterAll();
		if (_prevWndProc != IntPtr.Zero)
			SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _prevWndProc);
	}
}
