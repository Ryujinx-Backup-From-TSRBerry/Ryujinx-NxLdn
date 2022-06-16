using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 34)]
    internal struct Ssid
    {
        public byte Length;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 33)]
        public byte[] Name;
    }
}
