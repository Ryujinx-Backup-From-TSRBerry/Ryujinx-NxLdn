using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 188)]
    internal struct ConnectPrivateRequest
    {
        public SecurityConfig SecurityConfig;

        public SecurityParameter SecurityParameter;

        public UserConfig UserConfig;

        public uint LocalCommunicationVersion;

        public uint OptionUnknown;

        public NetworkConfig NetworkConfig;
    }
}
