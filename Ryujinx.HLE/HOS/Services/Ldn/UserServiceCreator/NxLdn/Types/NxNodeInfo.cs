using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 56)]
    public struct NxNodeInfo
    {
        public uint Ipv4Address;

        public Array6<byte> MacAddress;

        public byte IsConnected;

        private byte pad;

        public Array32<byte> UserName;

        // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L122
        public ushort LocalCommunicationVersion;

        public Array10<byte> Reserved1;

        public NodeInfo ToNodeInfo(byte nodeId)
        {
            Array33<byte> username = new();
            this.UserName.AsSpan().CopyTo(username.AsSpan());

            return new NodeInfo()
            {
                Ipv4Address = this.Ipv4Address,
                MacAddress = this.MacAddress,
                NodeId = nodeId,
                IsConnected = this.IsConnected,
                UserName = username,
                Reserved1 = 0,
                LocalCommunicationVersion = this.LocalCommunicationVersion,
                Reserved2 = new Array16<byte>()
            };
        }

        public static NxNodeInfo FromNodeInfo(NodeInfo info)
        {
            Array32<byte> username = new();
            info.UserName.AsSpan()[..^1].CopyTo(username.AsSpan());

            return new NxNodeInfo()
            {
                Ipv4Address = info.Ipv4Address,
                MacAddress = info.MacAddress,
                IsConnected = info.IsConnected,
                UserName = username,
                LocalCommunicationVersion = info.LocalCommunicationVersion,
                Reserved1 = new Array10<byte>()
            };
        }
    }
}