using PacketDotNet.Ieee80211;
using PacketDotNet.Utils;
using PacketDotNet.Utils.Converters;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types;
using System.Linq;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets
{
    // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L453
    // Length: 78 + ? (depends on size)
    internal sealed class AuthenticationFrame {
        // For PacketDotNet.Packet implementations this would usually be called Header
        private ByteArraySegment PacketHeader;

        // TODO: Remove debug stuff
        private static void LogMsg(string msg, object obj = null)
        {
            if (obj != null)
            {
                string jsonString = JsonHelper.Serialize<object>(obj, true);
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, "AuthenticationFrame: " + msg + "\n" + jsonString);
            }
            else
            {
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, "AuthenticationFrame: " + msg);
            }
        }

        public byte Version {
            get => PacketHeader.Skip(AuthenticationFields.VersionPosition).First();
            set => PacketHeader.Bytes[PacketHeader.Offset + AuthenticationFields.VersionPosition] = value;
        }

        private byte SizeLow {
            get => PacketHeader.Skip(AuthenticationFields.SizeLowPosition).First();
            set => PacketHeader.Bytes[PacketHeader.Offset + AuthenticationFields.SizeLowPosition] = value;
        }

        public AuthenticationStatusCode StatusCode {
            get => (AuthenticationStatusCode) PacketHeader.Skip(AuthenticationFields.StatusCodePosition).First();
            set => PacketHeader.Bytes[PacketHeader.Offset + AuthenticationFields.StatusCodePosition] = (byte) value;
        }

        public bool IsResponse {
            get => EndianBitConverter.Big.ToBoolean(PacketHeader.Bytes, AuthenticationFields.IsResponsePosition);
            set => EndianBitConverter.Big.CopyBytes(value, PacketHeader.Bytes, PacketHeader.Offset + AuthenticationFields.IsResponsePosition);
        }

        private byte SizeHigh {
            get => PacketHeader.Skip(AuthenticationFields.SizeHighPosition).First();
            set => PacketHeader.Bytes[PacketHeader.Offset + AuthenticationFields.SizeHighPosition] = value;
        }

        // This time the result should be in little endian
        // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L499
        public NetworkId Header
        {
            get => LdnHelper.FromBytes<NetworkId>(
                    PacketHeader.Skip(AuthenticationFields.SessionInfoPosition).Take(AuthenticationFields.SessionInfoLength).ToArray()
                );
            set => LdnHelper.StructureToByteArray(value).CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AuthenticationFields.SessionInfoPosition);
        }

        public byte[] NetworkKey {
            get => PacketHeader.Skip(AuthenticationFields.NetworkKeyPosition).Take(AuthenticationFields.NetworkKeyLength).ToArray();
            set => value.CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AuthenticationFields.NetworkKeyPosition);
        }

        public byte[] AuthenticationKey {
            get => PacketHeader.Skip(AuthenticationFields.AuthenticationKeyPosition).Take(AuthenticationFields.AuthenticationKeyLength).ToArray();
            set => value.CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AuthenticationFields.AuthenticationKeyLength);
        }

        public ushort Size {
            // TODO: Available bytes check: https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L504
            get => (byte) (SizeHigh << 8 | SizeLow);
            set
            {
                SizeLow = (byte) (value & 0xFF);
                SizeHigh = (byte) (value >> 8);
            }
        }

        private byte[] _payload {
            get => PacketHeader.Skip(AuthenticationFields.PayloadPosition).Take(Size).ToArray();
            set => value.CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AuthenticationFields.PayloadPosition);
        }

        public AuthenticationPayload Payload {
            get
            {
                if (!IsResponse)
                {
                    return LdnHelper.FromBytes<AuthenticationRequest>(_payload);
                }
                else
                {
                    if (Version >= 3)
                        return LdnHelper.FromBytes<AuthenticationResponse>(_payload);
                    return null;
                }
            }
            set
            {
                if (!IsResponse)
                {
                    LdnHelper.StructureToByteArray((AuthenticationRequest) value).CopyTo(_payload, 0);
                }
                else
                {
                    // TODO: Adjust exception type + message
                    if (Version < 3)
                        throw new System.Exception("Version < 3");
                    LdnHelper.StructureToByteArray((AuthenticationResponse)value).CopyTo(_payload, 0);
                }
            }
        }

        public ChallengeRequestParameter ChallengeRequest {
            get
            {
                if (!IsResponse) {
                    if (Version >= 3)
                    {
                        return LdnHelper.FromBytes<ChallengeRequestParameter>(_payload.Skip(AuthenticationFields.PayloadRequestChallengePosition).Take(AuthenticationFields.PayloadRequestChallengeLength).ToArray());
                    }
                    return default;
                }
                // TODO: Adjust exception type + message
                throw new System.Exception("IsResponse == true");
            }
            set
            {
                // TODO: Adjust exception type + message
                if (IsResponse)
                    throw new System.Exception("IsResponse == true");
                LdnHelper.StructureToByteArray(value).CopyTo(_payload, AuthenticationFields.PayloadRequestChallengePosition);
            }
        }

        public byte[] Encode()
        {
            return PacketHeader.ActualBytes();
        }

        public AuthenticationFrame()
        {
            PacketHeader = new ByteArraySegment(new byte[78 + 0]);
        }

        private AuthenticationFrame(ByteArraySegment byteArraySegment)
        {
            PacketHeader = byteArraySegment;
        }

        public static bool TryGetAuthenticationFrame(DataFrame data, out AuthenticationFrame authFrame)
        {
            if (data.PayloadDataSegment.Take(Marshal.SizeOf(HeaderFields.Authentication)).SequenceEqual(LdnHelper.StructureToByteArray(HeaderFields.Authentication)))
            {
                authFrame = new AuthenticationFrame(data.PayloadDataSegment);
                return true;
            }
            authFrame = null;
            return false;
        }
    }
}
