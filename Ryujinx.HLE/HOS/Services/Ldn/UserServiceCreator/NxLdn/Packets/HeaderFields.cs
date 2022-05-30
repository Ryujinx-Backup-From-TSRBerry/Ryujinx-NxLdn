using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets
{
    // Common network header fields used for LDN
    internal readonly struct HeaderFields
    {
        // Nintendo OUI
        internal static readonly byte[] Oui = new byte[] { 0x00, 0x22, 0xAA };

        // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L704
        // https://github.com/kinnay/NintendoClients/wiki/LDN-Protocol#advertisement-frame
        public static readonly ActionFrameHeader Action = new ActionFrameHeader() {
            Category = 0x7F,
            Oui = Oui,
            ProtocolId = 0x04,
            PacketType = PacketType.Advertisement
        };

        // https://github.com/kinnay/NintendoClients/wiki/LDN-Protocol#authentication-frame
        public static readonly DataFrameHeader Authentication = new DataFrameHeader {
            Oui = Oui,
            PacketType = PacketType.Authentication
        };
    }
}
