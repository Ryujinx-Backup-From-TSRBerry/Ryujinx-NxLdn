using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 56)]
    public struct NodeInfo
    {
        public uint Ipv4Address;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] MacAddress;

        public byte IsConnected;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 33)]
        public byte[] UserName;

        // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L122
        public ushort LocalCommunicationVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public byte[] Reserved1;
    }
}
