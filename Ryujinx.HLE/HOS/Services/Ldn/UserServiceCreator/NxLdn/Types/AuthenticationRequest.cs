using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    internal struct AuthenticationRequest : AuthenticationPayload
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] UserName;

        public ushort AppVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)]
        private byte[] _pad1;
    }
}
