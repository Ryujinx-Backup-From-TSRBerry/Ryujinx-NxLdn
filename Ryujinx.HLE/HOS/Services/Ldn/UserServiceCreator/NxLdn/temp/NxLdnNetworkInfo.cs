using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.temp {
    [StructLayout(LayoutKind.Sequential, Size = 1276)]
    public struct NxLdnNetworkInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] SecurityParameter;

        public ushort SecurityMode;

        public byte StationAcceptPolicy;

        public byte Unknown1;

        public ushort Reserved1;

        public byte NodeCountMax;

        public byte NodeCount;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public NxNodeInfo[] Nodes;

        public ushort Reserved2;

        public ushort AdvertiseDataSize;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 384)]
        public byte[] AdvertiseData;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 412)]
        public byte[] Unknown2;

        public ulong AuthenticationId;

        public Types.LdnNetworkInfo ToLdnNetworkInfo() {
            Types.LdnNetworkInfo netInfo = new Types.LdnNetworkInfo() {
                SecurityParameter = this.SecurityParameter,
                SecurityMode = this.SecurityMode,
                StationAcceptPolicy = this.StationAcceptPolicy,
                Unknown1 = this.Unknown1,
                Reserved1 = this.Reserved1,
                NodeCountMax = this.NodeCountMax,
                NodeCount = this.NodeCount,
                Nodes = new Types.NodeInfo[8],
                Reserved2 = this.Reserved2,
                AdvertiseDataSize = this.AdvertiseDataSize,
                AdvertiseData = this.AdvertiseData,
                Unknown2 = new byte[140],
                AuthenticationId = this.AuthenticationId
            };

            for (int i = 0; i < 8; i++)
            {
                netInfo.Nodes.SetValue(new Types.NodeInfo()
                {
                    Ipv4Address = this.Nodes[i].Ipv4Address,
                    MacAddress = this.Nodes[i].MacAddress,
                    NodeId = (byte)(i + 1),
                    IsConnected = this.Nodes[i].IsConnected,
                    UserName = this.Nodes[i].UserName,
                    Reserved1 = 0,
                    LocalCommunicationVersion = this.Nodes[i].LocalCommunicationVersion,
                    Reserved2 = new byte[16]
                }, i);
            }

            return netInfo;
        }
    }
}
