using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    internal struct PassphraseMessage
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] Passphrase;
    }
}
