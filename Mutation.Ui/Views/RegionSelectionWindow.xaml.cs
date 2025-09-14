using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Mutation.Ui.Views;

public sealed partial class RegionSelectionWindow : Window
{
	private Point _start;
	private bool _dragging;
	private TaskCompletionSource<Rect?>? _tcs;
	private readonly IntPtr _hwnd;
	private int _bmpW;
	private int _bmpH;

	// Cache XAML elements to avoid reliance on generated fields
	private Microsoft.UI.Xaml.Controls.Image? _img;
	private Microsoft.UI.Xaml.Controls.Canvas? _overlay;
	private Microsoft.UI.Xaml.Shapes.Rectangle? _selection;
	private Microsoft.UI.Xaml.Shapes.Rectangle? _crosshairV;
	private Microsoft.UI.Xaml.Shapes.Rectangle? _crosshairH;

	[DllImport("user32.dll", SetLastError = false)]
	private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
	[DllImport("user32.dll", SetLastError = false)]
	private static extern IntPtr SetCursor(IntPtr hCursor);
	private const int IDC_CROSS = 32515;
	private const int IDC_ARROW = 32512;

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT { public int X; public int Y; }
	[DllImport("user32.dll", SetLastError = false)]
	private static extern bool GetCursorPos(out POINT lpPoint);
	[DllImport("user32.dll", SetLastError = false)]
	private static extern uint GetDpiForWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	private static readonly IntPtr HWND_TOPMOST = new(-1);
	private const uint SWP_SHOWWINDOW = 0x0040; // keep for reference, but avoid using to prevent flicker
	private const uint SWP_NOMOVE = 0x0002;
	private const uint SWP_NOSIZE = 0x0001;
	public RegionSelectionWindow()
	{
		this.InitializeComponent();
		_hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
		EnsureElementRefs();
	}

	public Task InitializeAsync(SoftwareBitmap bitmap)
	{
		// Convert SoftwareBitmap directly into a WriteableBitmap to avoid encode/decode latency
		var converted = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
		WriteableBitmap wb = new(converted.PixelWidth, converted.PixelHeight);
		converted.CopyToBuffer(wb.PixelBuffer);
		wb.Invalidate();
		// Ensure refs in case constructor timing varies
		EnsureElementRefs();
		if (_img is not null)
		{
			_img.Source = wb;
		}
		_bmpW = wb.PixelWidth;
		_bmpH = wb.PixelHeight;

		var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
		var appWindow = this.AppWindow;
		if (appWindow != null)
		{
			appWindow.MoveAndResize(new RectInt32(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
		}

		// Compute composition scale (DPI) for the window to map pointer to physical pixels
		// Expand content into title bar area (hide chrome).
		if (this.AppWindow?.TitleBar is AppWindowTitleBar tb)
		{
			tb.ExtendsContentIntoTitleBar = true;
			tb.ButtonBackgroundColor = Windows.UI.Color.FromArgb(1, 0, 0, 0);
			tb.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(1, 0, 0, 0);
		}

		// Stretch overlay and image to the window size; actual DIP size will be known after layout.
		if (_overlay is not null)
		{
			_overlay.Width = bounds.Width;
			_overlay.Height = bounds.Height;
		}
		if (_img is not null)
		{
			_img.Width = bounds.Width;
			_img.Height = bounds.Height;
		}

		// Place the crosshair instantly at current cursor position and span full screen
		InitializeCrosshairAtCursor(bounds);

		// Do not show here; window will be activated in SelectRegionAsync after content is ready.
		return Task.CompletedTask;
	}

	public Task<Rect?> SelectRegionAsync()
	{
		_tcs = new TaskCompletionSource<Rect?>();
		// Activate now that image is loaded and sizes set, then ensure TopMost without forcing a show beforehand.
		this.Activate();
		var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
		InitializeCrosshairAtCursor(bounds);
		SetWindowPos(_hwnd, HWND_TOPMOST, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SWP_NOMOVE | SWP_NOSIZE);
		return _tcs.Task;
	}

	private void Overlay_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		if (_overlay is null) return;
		EnsureElementRefs();
		_start = e.GetCurrentPoint(_overlay).Position;
		_dragging = true;
		if (_selection is not null)
		{
			_selection.Visibility = Visibility.Visible;
			Canvas.SetLeft(_selection, _start.X);
			Canvas.SetTop(_selection, _start.Y);
			_selection.Width = 0;
			_selection.Height = 0;
		}
		UpdateCrosshair(_start);
		EnsureCrossCursor();
	}

	private void Overlay_PointerMoved(object sender, PointerRoutedEventArgs e)
	{
		if (_overlay is null) return;
		var pos = e.GetCurrentPoint(_overlay).Position;
		UpdateCrosshair(pos);
		if (!_dragging) { EnsureCrossCursor(); return; }
		double x = Math.Min(pos.X, _start.X);
		double y = Math.Min(pos.Y, _start.Y);
		double w = Math.Abs(pos.X - _start.X);
		double h = Math.Abs(pos.Y - _start.Y);
		if (_selection is not null)
		{
			Canvas.SetLeft(_selection, x);
			Canvas.SetTop(_selection, y);
			_selection.Width = w;
			_selection.Height = h;
		}
		EnsureCrossCursor();
	}

	private void Overlay_PointerReleased(object sender, PointerRoutedEventArgs e)
	{
		if (!_dragging) return;
		_dragging = false;
		if (_overlay is null) return;
		Point pos = e.GetCurrentPoint(_overlay).Position;
		double x = Math.Min(pos.X, _start.X);
		double y = Math.Min(pos.Y, _start.Y);
		double w = Math.Abs(pos.X - _start.X);
		double h = Math.Abs(pos.Y - _start.Y);
		// Map selection (Overlay coords in DIP) to bitmap pixel coords for accurate crop
		double scaleX = _bmpW / Math.Max(1.0, _overlay.ActualWidth);
		double scaleY = _bmpH / Math.Max(1.0, _overlay.ActualHeight);
		Rect rectPx = new(
			 Math.Max(0, Math.Round(x * scaleX)),
			 Math.Max(0, Math.Round(y * scaleY)),
			 Math.Max(1, Math.Round(w * scaleX)),
			 Math.Max(1, Math.Round(h * scaleY))
		);
		_tcs?.TrySetResult(rectPx);
		Close();
	}
				private void InitializeCrosshairAtCursor(System.Drawing.Rectangle bounds)
				{
					EnsureElementRefs();
					if (_overlay is null || _crosshairV is null || _crosshairH is null) return;
					// Determine DPI scale for mapping screen pixels to DIPs
					double scale = 1.0;
					try { var dpi = GetDpiForWindow(_hwnd); if (dpi > 0) scale = dpi / 96.0; } catch { }
					// Compute overlay size in DIPs
					double overlayW = _overlay.ActualWidth > 0 ? _overlay.ActualWidth : bounds.Width / scale;
					double overlayH = _overlay.ActualHeight > 0 ? _overlay.ActualHeight : bounds.Height / scale;
					_crosshairV.Height = overlayH;
					_crosshairH.Width = overlayW;
					// Map current cursor (screen px) to overlay DIPs
					double cx, cy;
					if (GetCursorPos(out POINT p))
					{
						cx = (p.X - bounds.Left) / scale;
						cy = (p.Y - bounds.Top) / scale;
					}
					else
					{
						cx = overlayW / 2.0;
						cy = overlayH / 2.0;
					}
					Canvas.SetLeft(_crosshairV, Math.Round(cx));
					Canvas.SetTop(_crosshairV, 0);
					Canvas.SetTop(_crosshairH, Math.Round(cy));
					Canvas.SetLeft(_crosshairH, 0);
				}

	private void Window_KeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key == Windows.System.VirtualKey.Escape)
		{
			_dragging = false;
			_tcs?.TrySetResult(null);
			Close();
		}
	}

	private void Overlay_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		// Keep crosshair stretched to current size
		UpdateCrosshair(new Point(e.NewSize.Width / 2.0, e.NewSize.Height / 2.0));
	}

	private void UpdateCrosshair(Point pos)
	{
		EnsureElementRefs();
		if (_crosshairV is null || _crosshairH is null || _overlay is null) return;
		double w = _overlay.ActualWidth;
		double h = _overlay.ActualHeight;
		if (double.IsNaN(w) || double.IsNaN(h) || w <= 0 || h <= 0) return;

		_crosshairV.Height = h;
		Canvas.SetLeft(_crosshairV, Math.Round(pos.X));
		Canvas.SetTop(_crosshairV, 0);

		_crosshairH.Width = w;
		Canvas.SetTop(_crosshairH, Math.Round(pos.Y));
		Canvas.SetLeft(_crosshairH, 0);
	}

	private void EnsureElementRefs()
	{
		if (_img != null && _overlay != null && _selection != null && _crosshairV != null && _crosshairH != null)
			return;
		if (this.Content is not FrameworkElement root)
			return;
		_img ??= root.FindName("Img") as Microsoft.UI.Xaml.Controls.Image;
		_overlay ??= root.FindName("Overlay") as Microsoft.UI.Xaml.Controls.Canvas;
		_selection ??= root.FindName("Selection") as Microsoft.UI.Xaml.Shapes.Rectangle;
		_crosshairV ??= root.FindName("CrosshairV") as Microsoft.UI.Xaml.Shapes.Rectangle;
		_crosshairH ??= root.FindName("CrosshairH") as Microsoft.UI.Xaml.Shapes.Rectangle;
	}

	private void EnsureCrossCursor()
	{
		try
		{
			IntPtr cur = LoadCursor(IntPtr.Zero, IDC_CROSS);
			if (cur != IntPtr.Zero) SetCursor(cur);
		}
		catch { }
	}

	private void Overlay_PointerEntered(object sender, PointerRoutedEventArgs e)
	{
		EnsureElementRefs();
		EnsureCrossCursor();
	}

	private void Overlay_PointerExited(object sender, PointerRoutedEventArgs e)
	{
		try
		{
			IntPtr cur = LoadCursor(IntPtr.Zero, IDC_ARROW);
			if (cur != IntPtr.Zero) SetCursor(cur);
		}
		catch { }
	}
}
