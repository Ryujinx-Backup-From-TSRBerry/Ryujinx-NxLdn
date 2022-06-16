using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 1072)]
    internal struct LdnNetworkInfo
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
        public NodeInfo[] Nodes;

        public ushort Reserved2;

        public ushort AdvertiseDataSize;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 384)]
        public byte[] AdvertiseData;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 140)]
        public byte[] Unknown2;

        public ulong AuthenticationId;
    }
}
