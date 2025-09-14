using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Windowing;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace Mutation.Ui.Views
{
    public sealed partial class RegionSelectionWindow : Window
    {
        private Point _start;
        private bool _dragging;
        private TaskCompletionSource<Rect?>? _tcs;
        private readonly IntPtr _hwnd;
    private int _bmpW;
    private int _bmpH;

    [DllImport("user32.dll", SetLastError = false)]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
    [DllImport("user32.dll", SetLastError = false)]
    private static extern IntPtr SetCursor(IntPtr hCursor);
    private const int IDC_CROSS = 32515;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        public RegionSelectionWindow()
        {
            this.InitializeComponent();
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        }

        public async Task InitializeAsync(SoftwareBitmap bitmap)
        {
            WriteableBitmap wb = new(bitmap.PixelWidth, bitmap.PixelHeight);
            using InMemoryRandomAccessStream stream = new();
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
            stream.Seek(0);
            await wb.SetSourceAsync(stream);
            Img.Source = wb;
            _bmpW = wb.PixelWidth;
            _bmpH = wb.PixelHeight;

            var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
            var appWindow = this.AppWindow;
            if (appWindow != null)
            {
                appWindow.MoveAndResize(new RectInt32(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
            }

            SetWindowPos(_hwnd, HWND_TOPMOST, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                SWP_SHOWWINDOW);

            // Compute composition scale (DPI) for the window to map pointer to physical pixels
            // Expand content into title bar area (hide chrome).
            if (this.AppWindow?.TitleBar is AppWindowTitleBar tb)
            {
                tb.ExtendsContentIntoTitleBar = true;
                tb.ButtonBackgroundColor = Windows.UI.Color.FromArgb(1, 0, 0, 0);
                tb.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(1, 0, 0, 0);
            }

            // Stretch overlay and image to the window size; actual DIP size will be known after layout.
            Overlay.Width = bounds.Width;
            Overlay.Height = bounds.Height;
            Img.Width = bounds.Width;
            Img.Height = bounds.Height;
        }

        public Task<Rect?> SelectRegionAsync()
        {
            _tcs = new TaskCompletionSource<Rect?>();
            this.Activate();
            return _tcs.Task;
        }

        private void Overlay_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _start = e.GetCurrentPoint(Overlay).Position;
            _dragging = true;
            Selection.Visibility = Visibility.Visible;
            Canvas.SetLeft(Selection, _start.X);
            Canvas.SetTop(Selection, _start.Y);
            Selection.Width = 0;
            Selection.Height = 0;
            UpdateCrosshair(_start);
            EnsureCrossCursor();
        }

        private void Overlay_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pos = e.GetCurrentPoint(Overlay).Position;
            UpdateCrosshair(pos);
            if (!_dragging) { EnsureCrossCursor(); return; }
            double x = Math.Min(pos.X, _start.X);
            double y = Math.Min(pos.Y, _start.Y);
            double w = Math.Abs(pos.X - _start.X);
            double h = Math.Abs(pos.Y - _start.Y);
            Canvas.SetLeft(Selection, x);
            Canvas.SetTop(Selection, y);
            Selection.Width = w;
            Selection.Height = h;
            EnsureCrossCursor();
        }

        private void Overlay_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            Point pos = e.GetCurrentPoint(Overlay).Position;
            double x = Math.Min(pos.X, _start.X);
            double y = Math.Min(pos.Y, _start.Y);
            double w = Math.Abs(pos.X - _start.X);
            double h = Math.Abs(pos.Y - _start.Y);
            // Map selection (Overlay coords in DIP) to bitmap pixel coords for accurate crop
            double scaleX = _bmpW / Math.Max(1.0, Overlay.ActualWidth);
            double scaleY = _bmpH / Math.Max(1.0, Overlay.ActualHeight);
            Rect rectPx = new(
                Math.Max(0, Math.Round(x * scaleX)),
                Math.Max(0, Math.Round(y * scaleY)),
                Math.Max(1, Math.Round(w * scaleX)),
                Math.Max(1, Math.Round(h * scaleY))
            );
            _tcs?.TrySetResult(rectPx);
            Close();
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
            if (CrosshairV is null || CrosshairH is null || Overlay is null) return;
            double w = Overlay.ActualWidth;
            double h = Overlay.ActualHeight;
            if (double.IsNaN(w) || double.IsNaN(h) || w <= 0 || h <= 0) return;

            CrosshairV.Height = h;
            Canvas.SetLeft(CrosshairV, Math.Round(pos.X) + 0.5);
            Canvas.SetTop(CrosshairV, 0);

            CrosshairH.Width = w;
            Canvas.SetTop(CrosshairH, Math.Round(pos.Y) + 0.5);
            Canvas.SetLeft(CrosshairH, 0);
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
    }
}
