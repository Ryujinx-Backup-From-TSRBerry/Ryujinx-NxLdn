using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 20)]
    internal struct ProxyDataHeader
    {
        public ProxyInfo Info;

        public uint DataLength;
    }
}
