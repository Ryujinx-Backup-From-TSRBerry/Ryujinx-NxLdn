using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    internal class ProxyInfo
    {
        public uint SourceIpV4;

        public ushort SourcePort;

        public uint DestIpV4;

        public ushort DestPort;

        public ProtocolType Protocol;
    }
}
