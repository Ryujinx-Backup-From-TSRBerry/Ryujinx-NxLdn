using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 38)]
    internal struct ExternalProxyConfig
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] ProxyIp;

        public AddressFamily AddressFamily;

        public ushort ProxyPort;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Token;
    }
}
