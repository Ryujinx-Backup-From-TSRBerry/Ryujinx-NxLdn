using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    internal struct UserConfig
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 33)]
        public byte[] UserName;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
        public byte[] Unknown1;
    }
}
