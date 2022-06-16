using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 8)]
    internal struct ExternalProxyConnectionState
    {
        public uint IpAddress;

        public bool Connected;
    }
}
