using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.temp {
    [StructLayout(LayoutKind.Sequential, Size = 56)]
    public struct NxNodeInfo
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

        public Types.NodeInfo ToNodeInfo(byte nodeId) {
            return new Types.NodeInfo()
            {
                Ipv4Address = this.Ipv4Address,
                MacAddress = this.MacAddress,
                NodeId = nodeId,
                IsConnected = this.IsConnected,
                UserName = this.UserName,
                Reserved1 = 0,
                LocalCommunicationVersion = this.LocalCommunicationVersion,
                Reserved2 = new byte[16]
            };
        }

        public static NxNodeInfo FromNodeInfo(Types.NodeInfo info) {
            return new NxNodeInfo()
            {
                Ipv4Address = info.Ipv4Address,
                MacAddress = info.MacAddress,
                IsConnected = info.IsConnected,
                UserName = info.UserName,
                LocalCommunicationVersion = info.LocalCommunicationVersion,
                Reserved1 = new byte[10]
            };
        }
    }
}
