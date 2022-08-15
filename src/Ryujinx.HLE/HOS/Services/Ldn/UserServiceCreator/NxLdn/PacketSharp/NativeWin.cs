using System;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn
{
    public static class NativeWin
    {
        [DllImport(@"Npcap\Packet.dll", SetLastError = true)]
        public static extern IntPtr PacketGetVersion();
        [DllImport(@"Npcap\Packet.dll", SetLastError = true)]
        public static extern IntPtr PacketGetDriverVersion();

        [DllImport(@"Npcap\Packet.dll", SetLastError = true)]
        public static extern IntPtr PacketOpenAdapter([MarshalAs(UnmanagedType.LPStr)] string adapterName);

        [DllImport(@"Npcap\Packet.dll", SetLastError = true)]
        public static extern void PacketCloseAdapter(IntPtr pLpAdapter);

        [DllImport(@"Npcap\Packet.dll", SetLastError = true)]
        public static extern int PacketGetMonitorMode([MarshalAs(UnmanagedType.LPStr)] string adapterName);

        [DllImport(@"Npcap\Packet.dll", SetLastError = true)]
        public static extern bool PacketRequest(ref LPADAPTER lpAdapter, bool set, ref PacketOidData OidData);

        [StructLayout(LayoutKind.Sequential)]
        public struct LPADAPTER
        {
            public IntPtr File;
            [MarshalAs(UnmanagedType.LPStr, SizeConst = 64)]
            public string SymbolicLink; // NOTE: Seems to be ignored
            public int    NumWrites;
            public IntPtr ReadEvent;
            public uint   ReadTimeOut;
            [MarshalAs(UnmanagedType.LPStr, SizeConst = 256 + 12)]
            public string Name; // NOTE: Seems to be ignored
            public IntPtr WanAdapter;
            public uint   Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PacketOidData
        {
            public uint Oid;
            public uint Length;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DOT11_OPERATION_MODE_CAPABILITY
        {
            public uint Reserved;
            public uint MajorVersion;
            public uint MinorVersion;
            public uint NumOfTXBuffers;
            public uint NumOfRXBuffers;
            public uint OpModeCapability;
        }

        public const uint OID_DOT11_NDIS_START                = 0x0D010300;
        public const uint OID_DOT11_CURRENT_CHANNEL           = OID_DOT11_NDIS_START + 53;
        public const uint OID_DOT11_OPERATION_MODE_CAPABILITY = OID_DOT11_NDIS_START + 7;

        public const uint DOT11_OPERATION_MODE_EXTENSIBLE_AP      = 0x00000008;
        public const uint DOT11_OPERATION_MODE_EXTENSIBLE_STATION = 0x00000004;
        public const uint DOT11_OPERATION_MODE_NETWORK_MONITOR    = 0x80000000;
    }
}
