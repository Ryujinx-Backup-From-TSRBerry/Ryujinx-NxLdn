using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    internal struct SecurityParameter
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Data;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] SessionId;
    }
}
