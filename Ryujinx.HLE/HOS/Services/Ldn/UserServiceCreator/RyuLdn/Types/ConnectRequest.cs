using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 1276)]
    internal struct ConnectRequest
    {
        public SecurityConfig SecurityConfig;

        public UserConfig UserConfig;

        public uint LocalCommunicationVersion;

        public uint OptionUnknown;

        public NetworkInfo NetworkInfo;
    }
}
