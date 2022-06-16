using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 316)]
    internal struct CreateAccessPointPrivateRequest
    {
        public SecurityConfig SecurityConfig;

        public SecurityParameter SecurityParameter;

        public UserConfig UserConfig;

        public NetworkConfig NetworkConfig;

        public AddressList AddressList;

        public RyuNetworkConfig RyuNetworkConfig;
    }
}
