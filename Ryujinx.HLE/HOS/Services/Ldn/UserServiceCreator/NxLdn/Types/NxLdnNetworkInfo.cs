using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 1280)]
    internal struct NxLdnNetworkInfo
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

        public LdnNetworkInfo ToLdnNetworkInfo()
        {
            LdnNetworkInfo netInfo = new LdnNetworkInfo()
            {
                SecurityParameter = this.SecurityParameter,
                SecurityMode = this.SecurityMode,
                StationAcceptPolicy = this.StationAcceptPolicy,
                Unknown1 = this.Unknown1,
                Reserved1 = this.Reserved1,
                NodeCountMax = this.NodeCountMax,
                NodeCount = this.NodeCount,
                Nodes = new NodeInfo[this.Nodes.Length],
                Reserved2 = this.Reserved2,
                AdvertiseDataSize = this.AdvertiseDataSize,
                AdvertiseData = this.AdvertiseData,
                Unknown2 = new byte[140],
                AuthenticationId = this.AuthenticationId
            };

            for (int i = 0; i < this.Nodes.Length; i++)
            {
                netInfo.Nodes.SetValue(this.Nodes[i].ToNodeInfo((byte)(i + 1)), i);
            }

            return netInfo;
        }

        public static NxLdnNetworkInfo FromLdnNetworkInfo(LdnNetworkInfo netInfo)
        {
            NxLdnNetworkInfo nxNetInfo = new NxLdnNetworkInfo()
            {
                SecurityParameter = netInfo.SecurityParameter,
                SecurityMode = netInfo.SecurityMode,
                StationAcceptPolicy = netInfo.StationAcceptPolicy,
                Unknown1 = netInfo.Unknown1,
                Reserved1 = netInfo.Reserved1,
                NodeCountMax = netInfo.NodeCountMax,
                NodeCount = netInfo.NodeCount,
                Nodes = new NxNodeInfo[netInfo.Nodes.Length],
                Reserved2 = netInfo.Reserved2,
                AdvertiseDataSize = netInfo.AdvertiseDataSize,
                AdvertiseData = netInfo.AdvertiseData,
                Unknown2 = new byte[412],
                AuthenticationId = netInfo.AuthenticationId
            };

            for (int i = 0; i < netInfo.Nodes.Length; i++)
            {
                nxNetInfo.Nodes.SetValue(NxNodeInfo.FromNodeInfo(netInfo.Nodes[i]), i);
            }

            return nxNetInfo;
        }
    }
}
