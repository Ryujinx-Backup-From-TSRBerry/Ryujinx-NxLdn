using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    internal struct ProxyConfig
    {
        public uint ProxyIp;

        public uint ProxySubnetMask;
    }
}
