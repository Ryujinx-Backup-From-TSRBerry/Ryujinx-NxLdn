using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    internal struct NetworkConfig
    {
        public IntentId IntentId;

        public ushort Channel;

        public byte NodeCountMax;

        public byte Reserved1;

        public ushort LocalCommunicationVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public byte[] Reserved2;
    }
}
