using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 56)]
    internal struct NxNodeInfo
    {
        public uint Ipv4Address;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] MacAddress;

        public byte IsConnected;

        private byte pad;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] UserName;

        // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L122
        public ushort LocalCommunicationVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public byte[] Reserved1;

        public NodeInfo ToNodeInfo(byte nodeId)
        {
            byte[] username = new byte[33];
            this.UserName.CopyTo(username, 0);
            return new NodeInfo()
            {
                Ipv4Address = this.Ipv4Address,
                MacAddress = this.MacAddress,
                NodeId = nodeId,
                IsConnected = this.IsConnected,
                UserName = username,
                Reserved1 = 0,
                LocalCommunicationVersion = this.LocalCommunicationVersion,
                Reserved2 = new byte[16]
            };
        }

        public static NxNodeInfo FromNodeInfo(NodeInfo info)
        {
            byte[] username = new byte[32];
            if (info.UserName != null)
            {
                Array.Copy(info.UserName, username, 32);
            }
            return new NxNodeInfo()
            {
                Ipv4Address = info.Ipv4Address,
                MacAddress = info.MacAddress,
                IsConnected = info.IsConnected,
                UserName = username,
                LocalCommunicationVersion = info.LocalCommunicationVersion,
                Reserved1 = new byte[10]
            };
        }
    }
}
