using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 1)]
    internal struct SetAcceptPolicyRequest
    {
        public AcceptPolicy StationAcceptPolicy;
    }
}
