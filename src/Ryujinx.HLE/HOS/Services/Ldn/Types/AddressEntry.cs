using Ryujinx.Common.Memory;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 0xC)]
    struct AddressEntry
    {
        public uint Ipv4Address;
        public Array6<byte> MacAddress;
        public ushort Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x60)]
    struct AddressList
    {
        public Array8<AddressEntry> Addresses;
    }
}
