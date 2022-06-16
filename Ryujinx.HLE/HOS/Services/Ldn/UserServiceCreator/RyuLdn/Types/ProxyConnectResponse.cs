using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    internal struct ProxyConnectResponse
    {
        public ProxyInfo Info;
    }
}
