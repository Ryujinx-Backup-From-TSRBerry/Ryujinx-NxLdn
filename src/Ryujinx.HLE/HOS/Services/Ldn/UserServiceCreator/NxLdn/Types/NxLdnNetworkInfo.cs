using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 1280)]
    internal struct NxLdnNetworkInfo
    {
        public Array16<byte> SecurityParameter;

        public ushort SecurityMode;

        public byte StationAcceptPolicy;

        private byte _pad1;

        private ushort _pad2;

        public byte NodeCountMax;

        public byte NodeCount;

        public Array8<NxNodeInfo> Nodes;

        private ushort _pad3;

        public ushort AdvertiseDataSize;

        public Array384<byte> AdvertiseData;

        private Array412<byte> _pad4;

        public ulong AuthenticationToken;

        public LdnNetworkInfo ToLdnNetworkInfo()
        {
            LdnNetworkInfo netInfo = new LdnNetworkInfo()
            {
                SecurityParameter = this.SecurityParameter,
                SecurityMode = this.SecurityMode,
                StationAcceptPolicy = this.StationAcceptPolicy,
                Unknown1 = 0,
                Reserved1 = 0,
                NodeCountMax = this.NodeCountMax,
                NodeCount = this.NodeCount,
                Nodes = new Array8<NodeInfo>(),
                Reserved2 = 0,
                AdvertiseDataSize = this.AdvertiseDataSize,
                AdvertiseData = this.AdvertiseData,
                Unknown2 = new Array140<byte>(),
                AuthenticationId = this.AuthenticationToken
            };

            netInfo.Unknown2.AsSpan().Fill(0);

            for (int i = 0; i < this.Nodes.Length; i++)
            {
                netInfo.Nodes[i] = this.Nodes[i].ToNodeInfo((byte)(i));
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
                _pad1 = 0,
                _pad2 = 0,
                NodeCountMax = netInfo.NodeCountMax,
                NodeCount = netInfo.NodeCount,
                Nodes = new Array8<NxNodeInfo>(),
                _pad3 = 0,
                AdvertiseDataSize = netInfo.AdvertiseDataSize,
                AdvertiseData = netInfo.AdvertiseData,
                _pad4 = new Array412<byte>(),
                AuthenticationToken = netInfo.AuthenticationId
            };

            nxNetInfo._pad4.AsSpan().Fill(0);

            for (int i = 0; i < netInfo.Nodes.Length; i++)
            {
                nxNetInfo.Nodes[i] = NxNodeInfo.FromNodeInfo(netInfo.Nodes[i]);
            }

            return nxNetInfo;
        }
    }
}