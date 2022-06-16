using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.NxLdn.Capabilities
{
    [StructLayout(LayoutKind.Sequential)]
    public struct UserCapDataStruct
    {
        public uint effective;
        public uint permitted;
        public uint inheritable;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UserCapDataStructArray
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public UserCapDataStruct[] dataStructs;
    }
}
