using System;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace Mutation.Ui.Services
{
    public static class Direct3D11Helper
    {
        public static IDirect3DDevice CreateDevice()
        {
            var dxgiDevice = CreateDxgiDevice();
            return CreateDirect3DDevice(dxgiDevice);
        }

        private static IntPtr CreateDxgiDevice()
        {
            // This is a placeholder. In a real implementation, you would create a DXGI device here.
            // For example, using SharpDX or another interop library.
            throw new NotImplementedException("DXGI device creation must be implemented for your environment.");
        }

        public static IDirect3DDevice CreateDirect3DDevice(IntPtr dxgiDevice)
        {
            object obj;
            var iid = typeof(IDirect3DDevice).GUID;
            int hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out obj);
            if (hr != 0)
                throw new Exception($"Failed to create IDirect3DDevice: HRESULT=0x{hr:X}");
            return (IDirect3DDevice)obj;
        }

        [System.Runtime.InteropServices.DllImport("d3d11.dll")]
        private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out object graphicsDevice);
    }
}
