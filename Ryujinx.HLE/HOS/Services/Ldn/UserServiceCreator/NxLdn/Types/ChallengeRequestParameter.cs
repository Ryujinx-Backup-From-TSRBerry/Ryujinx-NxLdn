using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 0x300)]
    public struct ChallengeRequestParameter
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        private byte[] _pad1;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Hmac;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        private byte[] _pad2;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x2D0)]
        public byte[] Body;
    }
}
