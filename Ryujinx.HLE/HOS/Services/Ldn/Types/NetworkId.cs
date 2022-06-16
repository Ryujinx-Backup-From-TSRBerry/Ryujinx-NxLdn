using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    internal struct NetworkId
    {
        public IntentId IntentId;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] SessionId;
    }
}
