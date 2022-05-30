using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets {
    internal readonly struct AuthenticationFields {
        public static readonly int LdnHeaderLength = Marshal.SizeOf<DataFrameHeader>();
        public static readonly int VersionPosition = LdnHeaderLength;
        public static readonly int VersionLength = 0x1;
        public static readonly int SizeLowPosition = VersionPosition + VersionLength;
        public static readonly int SizeLowLength = 0x1;
        public static readonly int StatusCodePosition = SizeLowPosition + SizeLowLength;
        public static readonly int StatusCodeLength = 0x1;
        public static readonly int IsResponsePosition = StatusCodePosition + StatusCodeLength;
        public static readonly int IsResponseLength = 0x1;
        public static readonly int SizeHighPosition = IsResponsePosition + IsResponseLength;
        public static readonly int SizeHighLength = 0x1;
        public static readonly int SessionInfoPosition = SizeHighPosition + SizeHighLength + 0x3;
        public static readonly int SessionInfoLength = Marshal.SizeOf<NetworkId>();
        public static readonly int NetworkKeyPosition = SessionInfoPosition + SessionInfoLength;
        public static readonly int NetworkKeyLength = 0x10;
        public static readonly int AuthenticationKeyPosition = NetworkKeyPosition + NetworkKeyLength;
        public static readonly int AuthenticationKeyLength = 0x10;
        public static readonly int PayloadPosition = AuthenticationKeyPosition + AuthenticationKeyLength;
    }
}
