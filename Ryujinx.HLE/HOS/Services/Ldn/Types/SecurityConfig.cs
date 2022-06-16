using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 68)]
    internal struct SecurityConfig
    {
        public SecurityMode SecurityMode;

        public ushort PassphraseSize;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] Passphrase;
    }
}
