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

namespace Mutation.Ui.Views
{
    public sealed partial class RegionSelectionWindow : Window
    {
        private Point _start;
        private bool _dragging;
        private TaskCompletionSource<Rect?>? _tcs;
        private readonly IntPtr _hwnd;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        public RegionSelectionWindow()
        {
            this.InitializeComponent();
            this.KeyDown += Window_KeyDown;
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

            var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
            var appWindow = this.AppWindow;
            if (appWindow != null)
            {
                appWindow.MoveAndResize(new RectInt32(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
            }

            SetWindowPos(_hwnd, HWND_TOPMOST, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                SWP_SHOWWINDOW);
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
        }

        private void Overlay_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_dragging) return;
            Point pos = e.GetCurrentPoint(Overlay).Position;
            double x = Math.Min(pos.X, _start.X);
            double y = Math.Min(pos.Y, _start.Y);
            double w = Math.Abs(pos.X - _start.X);
            double h = Math.Abs(pos.Y - _start.Y);
            Canvas.SetLeft(Selection, x);
            Canvas.SetTop(Selection, y);
            Selection.Width = w;
            Selection.Height = h;
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
            Rect rect = new(x, y, w, h);
            _tcs?.TrySetResult(rect);
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
    }
}
