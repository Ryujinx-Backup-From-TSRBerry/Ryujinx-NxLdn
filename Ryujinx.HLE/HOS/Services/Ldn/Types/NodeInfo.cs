using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct NodeInfo
    {
        public uint Ipv4Address;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] MacAddress;

        public byte NodeId;

        public byte IsConnected;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 33)]
        public byte[] UserName;

        public byte Reserved1;

        // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L122
        public ushort LocalCommunicationVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Reserved2;
    }
}
