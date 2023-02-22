using PacketDotNet.Ieee80211;
using PacketDotNet.Utils;
using PacketDotNet.Utils.Converters;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets
{
    // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L453
    // Length: 6 + 72 + ? (depends on size)
    internal sealed class NxAuthenticationFrame
    {
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

        private DataFrameHeader AuthHeader
        {
            get => MemoryMarshal.Read<DataFrameHeader>(PacketHeader.Take(NxAuthenticationFields.LdnHeaderLength).ToArray());
            set => MemoryMarshal.Write(PacketHeader.Bytes.AsSpan(PacketHeader.Offset), ref value);
        }

        public byte Version
        {
            get => PacketHeader.Skip(NxAuthenticationFields.VersionPosition).First();
            set => PacketHeader.Bytes[PacketHeader.Offset + NxAuthenticationFields.VersionPosition] = value;
        }

        private byte SizeLow
        {
            get => PacketHeader.Skip(NxAuthenticationFields.SizeLowPosition).First();
            set => PacketHeader.Bytes[PacketHeader.Offset + NxAuthenticationFields.SizeLowPosition] = value;
        }

        public AuthenticationStatusCode StatusCode
        {
            get => (AuthenticationStatusCode)PacketHeader.Skip(NxAuthenticationFields.StatusCodePosition).First();
            set => PacketHeader.Bytes[PacketHeader.Offset + NxAuthenticationFields.StatusCodePosition] = (byte)value;
        }

        public bool IsResponse
        {
            get => EndianBitConverter.Big.ToBoolean(PacketHeader.Bytes, NxAuthenticationFields.IsResponsePosition);
            set => EndianBitConverter.Big.CopyBytes(value, PacketHeader.Bytes, PacketHeader.Offset + NxAuthenticationFields.IsResponsePosition);
        }

        private byte SizeHigh
        {
            get => PacketHeader.Skip(NxAuthenticationFields.SizeHighPosition).First();
            set => PacketHeader.Bytes[PacketHeader.Offset + NxAuthenticationFields.SizeHighPosition] = value;
        }

        // This time the result should be in little endian
        // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L499
        public NetworkId Header
        {
            get => MemoryMarshal.Read<NetworkId>(
                    PacketHeader.Skip(NxAuthenticationFields.SessionInfoPosition).Take(NxAuthenticationFields.SessionInfoLength).ToArray()
                );
            set => MemoryMarshal.Write(PacketHeader.Bytes.AsSpan(PacketHeader.Offset + NxAuthenticationFields.SessionInfoPosition), ref value);
        }

        public byte[] NetworkKey
        {
            get => PacketHeader.Skip(NxAuthenticationFields.NetworkKeyPosition).Take(NxAuthenticationFields.NetworkKeyLength).ToArray();
            set => value.CopyTo(PacketHeader.Bytes, PacketHeader.Offset + NxAuthenticationFields.NetworkKeyPosition);
        }

        public byte[] AuthenticationKey
        {
            get => PacketHeader.Skip(NxAuthenticationFields.AuthenticationKeyPosition).Take(NxAuthenticationFields.AuthenticationKeyLength).ToArray();
            set => value.CopyTo(PacketHeader.Bytes, PacketHeader.Offset + NxAuthenticationFields.AuthenticationKeyPosition);
        }

        public ushort Size
        {
            // TODO: Available bytes check: https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L504
            get => (ushort)(SizeLow | SizeHigh << 8);
            set
            {
                SizeLow = (byte)(value & 0xFF);
                SizeHigh = (byte)(value >> 8);
            }
        }

        private byte[] _payload
        {
            get => PacketHeader.Skip(NxAuthenticationFields.PayloadPosition).Take(Size).ToArray();
            set => value.CopyTo(PacketHeader.Bytes, PacketHeader.Offset + NxAuthenticationFields.PayloadPosition);
        }

        public AuthenticationPayload Payload
        {
            get
            {
                if (!IsResponse)
                {
                    return MemoryMarshal.Read<AuthenticationRequest>(_payload);
                }
                else
                {
                    if (Version >= 3)
                        return MemoryMarshal.Read<AuthenticationResponse>(_payload);
                    return null;
                }
            }
            set
            {
                byte[] payload = _payload;
                if (!IsResponse)
                {
                    // LdnHelper.StructureToByteArray((AuthenticationRequest)value).CopyTo(payload, 0);
                }
                else
                {
                    // TODO: Adjust exception type + message
                    if (Version < 3)
                        throw new System.Exception("Version < 3");
                    // LdnHelper.StructureToByteArray((AuthenticationResponse)value).CopyTo(payload, 0);
                }
                _payload = payload;
            }
        }

        public ChallengeRequestParameter ChallengeRequest
        {
            get
            {
                if (!IsResponse)
                {
                    if (Version >= 3)
                    {
                        return MemoryMarshal.Read<ChallengeRequestParameter>(_payload.Skip(NxAuthenticationFields.PayloadRequestChallengePosition).Take(NxAuthenticationFields.PayloadRequestChallengeLength).ToArray());
                    }
                    return default;
                }
                // TODO: Adjust exception type + message
                throw new System.Exception("IsResponse == true");
            }
            set
            {
                byte[] payload = _payload;
                // TODO: Adjust exception type + message
                if (IsResponse)
                    throw new System.Exception("IsResponse == true");
                MemoryMarshal.Write(payload.AsSpan(NxAuthenticationFields.PayloadRequestChallengePosition), ref value);
                _payload = payload;
            }
        }

        public byte[] Encode()
        {
            return PacketHeader.ActualBytes();
        }

        public void LogProps()
        {
            LogMsg($"0 > AuthHeader[{Marshal.SizeOf(AuthHeader)}/{Marshal.SizeOf<DataFrameHeader>()}]: ", AuthHeader);
            LogMsg($"{NxAuthenticationFields.VersionPosition} > Version: {Version}");
            LogMsg($"{NxAuthenticationFields.SizeLowPosition} > SizeLow: {SizeLow}");
            LogMsg($"{NxAuthenticationFields.StatusCodePosition} > StatusCode: {StatusCode}");
            LogMsg($"{NxAuthenticationFields.IsResponsePosition} > IsResponse: {IsResponse}");
            LogMsg($"{NxAuthenticationFields.SizeHighPosition} > SizeHigh: {SizeHigh}");
            LogMsg($"{NxAuthenticationFields.SessionInfoPosition} > Header: ", Header);
            LogMsg($"{NxAuthenticationFields.NetworkKeyPosition} > NetworkKey: ", NetworkKey);
            LogMsg($"{NxAuthenticationFields.AuthenticationKeyPosition} > AuthenticationKey: ", AuthenticationKey);
            LogMsg($"{NxAuthenticationFields.PayloadPosition} > Payload(Array): ", _payload);
            LogMsg($"Size: {Size}");
            LogMsg($"Payload: ", Payload);
            LogMsg($"ChallengeRequest: ", ChallengeRequest);
        }

        public NxAuthenticationFrame()
        {
            // Size: 0x3b2
            PacketHeader = new ByteArraySegment(new byte[78 + NxAuthenticationFields.PayloadRequestChallengePosition + NxAuthenticationFields.PayloadRequestChallengeLength]);
            AuthHeader = HeaderFields.Authentication;
        }

        private NxAuthenticationFrame(ByteArraySegment byteArraySegment)
        {
            PacketHeader = byteArraySegment;
        }

        public static bool TryGetNxAuthenticationFrame(AuthenticationFrame data, out NxAuthenticationFrame authFrame)
        {
            byte[] header = new byte[Marshal.SizeOf<DataFrameHeader>()];
            DataFrameHeader headerStruct = HeaderFields.Authentication;
            MemoryMarshal.Write(header, ref headerStruct);
            if (data.PayloadDataSegment.Take(Marshal.SizeOf(HeaderFields.Authentication)).SequenceEqual(header))
            {
                authFrame = new NxAuthenticationFrame(data.PayloadDataSegment);
                return true;
            }
            authFrame = null;
            return false;
        }
    }
}