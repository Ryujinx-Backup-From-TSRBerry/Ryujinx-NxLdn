using Ryujinx.Common.Memory;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    internal struct AuthenticationRequest : AuthenticationPayload
    {
        public Array32<byte> UserName;

        public ushort AppVersion;

        private Array30<byte> _pad1;
    }
}