using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets {

    [StructLayout(LayoutKind.Sequential, Size = 12)]
    internal struct ActionFrameHeader {
        public byte Category;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Oui;

        public byte ProtocolId;

        private byte _pad1;

        public PacketType PacketType;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        private byte[] _pad2;
    }
}
