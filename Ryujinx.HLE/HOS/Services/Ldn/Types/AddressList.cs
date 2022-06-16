using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 96)]
    internal struct AddressList
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public AddressEntry[] Addresses;
    }
}
