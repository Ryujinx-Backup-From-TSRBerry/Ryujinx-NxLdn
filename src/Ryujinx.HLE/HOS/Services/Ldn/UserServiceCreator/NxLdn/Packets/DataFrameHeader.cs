using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets
{
    [StructLayout(LayoutKind.Sequential, Size = 6, Pack = 1)]
    internal struct DataFrameHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Oui;

        public PacketType PacketType;

        private byte _pad1;
    }
}
