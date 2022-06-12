using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets
{
    internal readonly struct AdvertisementFields
    {
        public static readonly int LdnHeaderLength = Marshal.SizeOf<ActionFrameHeader>();
        public static readonly int MessageHeaderPosition = LdnHeaderLength;
        public static readonly int MessageHeaderLength = 0x28;
        public static readonly int SessionInfoPosition = MessageHeaderPosition;
        public static readonly int SessionInfoLength = Marshal.SizeOf<NetworkId>();
        public static readonly int VersionPosition = SessionInfoPosition + SessionInfoLength;
        public static readonly int VersionLength = 0x1;
        public static readonly int EncryptionPosition = VersionPosition + VersionLength;
        public static readonly int EncryptionLength = 0x1;
        public static readonly int BodySizePosition = EncryptionPosition + EncryptionLength;
        public static readonly int BodySizeLength = 0x2;
        public static readonly int BodySizeValue = 0x500;
        public static readonly int NoncePosition = BodySizePosition + BodySizeLength;
        public static readonly int NonceLength = 0x4;
        public static readonly int BodyPosition = NoncePosition + NonceLength;
        public static readonly int HashLength = 0x20;
    }
}
