using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    internal struct CommonNetworkInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] MacAddress;

        public Ssid Ssid;

        public ushort Channel;

        public byte LinkLevel;

        public byte NetworkType;

        public uint Reserved;
    }
}
