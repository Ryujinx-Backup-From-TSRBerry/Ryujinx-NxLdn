using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 40)]
    internal struct ExternalProxyToken
    {
        public uint VirtualIp;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Token;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] PhysicalIp;

        public AddressFamily AddressFamily;
    }
}
