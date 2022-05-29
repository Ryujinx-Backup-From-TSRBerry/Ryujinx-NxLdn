using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 1356)]
    internal struct NetworkInfo
    {
        public NetworkId NetworkId;

        public CommonNetworkInfo Common;

        public LdnNetworkInfo Ldn;
    }
}
