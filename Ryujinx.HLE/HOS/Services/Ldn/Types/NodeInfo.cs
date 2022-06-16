using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    internal struct NodeInfo
    {
        public uint Ipv4Address;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] MacAddress;

        public byte NodeId;

        public byte IsConnected;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 33)]
        public byte[] UserName;

        public byte Reserved1;

        public ushort LocalCommunicationVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Reserved2;
    }
}
