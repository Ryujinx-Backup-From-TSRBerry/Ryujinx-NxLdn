using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]
    internal struct RyuNetworkConfig
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] GameVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] PrivateIp;

        public AddressFamily AddressFamily;

        public ushort ExternalProxyPort;

        public ushort InternalProxyPort;
    }
}
