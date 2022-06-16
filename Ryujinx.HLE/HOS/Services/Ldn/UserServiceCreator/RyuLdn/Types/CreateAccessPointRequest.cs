using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 188)]
    internal struct CreateAccessPointRequest
    {
        public SecurityConfig SecurityConfig;

        public UserConfig UserConfig;

        public NetworkConfig NetworkConfig;

        public RyuNetworkConfig RyuNetworkConfig;
    }
}
