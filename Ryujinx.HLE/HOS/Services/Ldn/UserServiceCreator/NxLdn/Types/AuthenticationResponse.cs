using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 388)]
    internal struct AuthenticationResponse : AuthenticationPayload
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x84)]
        private byte[] _pad1;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x100)]
        public byte[] Challenge;
    }
}
