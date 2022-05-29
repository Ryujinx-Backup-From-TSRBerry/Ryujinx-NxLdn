using PacketDotNet.Ieee80211;
using PacketDotNet.Utils;
using PacketDotNet.Utils.Converters;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.temp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets
{
    // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L181
    // Length: 1388
    public sealed class AdvertisementFrame {
        // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L704
        // Vendor-specific, Nintendo OUI, LDN, Advertisement
        private static readonly byte[] LdnHeader = { 0x7F, 0x00, 0x22, 0xAA, 0x04, 0x00, 0x01, 0x01 };

        private static readonly byte[] AdvertisementKeySource = {0x19, 0x18, 0x84, 0x74, 0x3e, 0x24, 0xc7, 0x7d, 0x87, 0xc6, 0x9e, 0x42, 0x07, 0xd0, 0xc4, 0x38 };

        // For PacketDotNet.Packet implementations this would usually be called Header
        private ByteArraySegment PacketHeader;

        // TODO: Remove debug stuff
        private static void LogMsg(string msg, object obj = null)
        {
            if (obj != null)
            {
                string jsonString = JsonHelper.Serialize<object>(obj, true);
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, "AdvertisementFrame: " + msg + "\n" + jsonString);
            }
            else
            {
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, "AdvertisementFrame: " + msg);
            }
        }

        // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L245
        private byte[] MessageHeader {
            get => PacketHeader.Skip(AdvertisementFields.MessageHeaderPosition).Take(AdvertisementFields.MessageHeaderLength).ToArray();
            set {
                if (value != null && value.Length > 0 && value.Length <= AdvertisementFields.MessageHeaderLength) {
                    Array.Resize(ref value, AdvertisementFields.MessageHeaderLength);
                    value.CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.MessageHeaderPosition);
                }
                else if (value == null) {
                    byte[] fillArr = new byte[AdvertisementFields.MessageHeaderLength];
                    Array.Fill<byte>(fillArr, 0);
                    fillArr.CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.MessageHeaderPosition);
                }
                else {
                    throw new OverflowException();
                }
            }
        }
        public NetworkId Header {
            get => StructConvHelper.BytesToStruct<NetworkId>(
                    Endianness.BigEndian,
                    PacketHeader.Skip(AdvertisementFields.SessionInfoPosition).Take(AdvertisementFields.SessionInfoLength).ToArray()
                );
            set => StructConvHelper.StructToBytes<NetworkId>(Endianness.BigEndian, value).CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.SessionInfoPosition);
        }

        public byte Version {
            get => PacketHeader.Skip((byte)AdvertisementFields.VersionPosition).First();
            set
            {
                // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L253
                if (new byte[] { 2, 3 }.Contains(value)) {
                    PacketHeader.Bytes[PacketHeader.Offset + AdvertisementFields.VersionPosition] = value;
                }
                else {
                    throw new ArgumentException();
                }
            }
        }

        public byte Encryption {
            get => PacketHeader.Skip(AdvertisementFields.EncryptionPosition).First();
            set
            {
                // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L258
                if (new byte[] { 1, 2 }.Contains(value))
                {
                    PacketHeader.Bytes[PacketHeader.Offset + AdvertisementFields.EncryptionPosition] = value;
                }
                else
                {
                    throw new ArgumentException();
                }
            }
        }

        private ushort BodySize {
            get => EndianBitConverter.Big.ToUInt16(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.BodySizePosition);
            set
            {
                // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L262
                if (value == AdvertisementFields.BodySizeValue) {
                    EndianBitConverter.Big.CopyBytes(value, PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.BodySizePosition);
                }
                else {
                    throw new ArgumentException();
                }
            }
        }

        public byte[] Nonce {
            get => PacketHeader.Skip(AdvertisementFields.NoncePosition).Take(AdvertisementFields.NonceLength).ToArray();
            set
            {
                if (value != null && value.Length > 0 && value.Length <= AdvertisementFields.NonceLength) {
                    Array.Resize<byte>(ref value, AdvertisementFields.NonceLength);
                    value.CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.NoncePosition);
                }
                else if (value == null) {
                    byte[] fillArr = new byte[AdvertisementFields.NonceLength];
                    Array.Fill<byte>(fillArr, 0);
                    fillArr.CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.NoncePosition);
                }
                else {
                    throw new OverflowException();
                }
            }
        }

        private byte[] Body {
            get => Decrypt(PacketHeader.Skip(AdvertisementFields.BodyPosition).Take(AdvertisementFields.HashLength + BodySize).ToArray());
            set
            {
                if (value != null && value.Length > 0 && value.Length <= AdvertisementFields.HashLength + BodySize) {
                    Array.Resize<byte>(ref value, AdvertisementFields.HashLength + BodySize);
                    Encrypt(value).CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.BodyPosition);
                }
                else if (value == null) {
                    byte[] fillArr = new byte[AdvertisementFields.HashLength + BodySize];
                    Array.Fill<byte>(fillArr, 0);
                    Encrypt(fillArr).CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.BodyPosition);
                }
                else {
                    throw new OverflowException();
                }
            }
        }

        // Sha
        private byte[] Hash {
            get => Body.Take(AdvertisementFields.HashLength).ToArray();
            set {
                if (value != null && value.Length > 0 && value.Length <= AdvertisementFields.HashLength)
                {
                    Array.Resize<byte>(ref value, AdvertisementFields.HashLength);
                    value.CopyTo(Body, 0);
                }
                else if (value == null)
                {
                    byte[] fillArr = new byte[AdvertisementFields.HashLength];
                    Array.Fill<byte>(fillArr, 0);
                    fillArr.CopyTo(Body, 0);
                }
                else
                {
                    throw new OverflowException();
                }
            }
        }


        // Info
        private byte[] Payload {
            get => Body.Skip(0x20).ToArray();
            set
            {
                if (value != null && value.Length > 0 && value.Length <= BodySize)
                {
                    Array.Resize<byte>(ref value, BodySize);
                    value.CopyTo(Body, AdvertisementFields.HashLength);
                }
                else if (value == null)
                {
                    byte[] fillArr = new byte[BodySize];
                    Array.Fill<byte>(fillArr, 0);
                    fillArr.CopyTo(Body, AdvertisementFields.HashLength);
                }
                else
                {
                    throw new OverflowException();
                }
            }
        }

        private byte[] Message {
            get
            {
                List<byte> message = new List<byte>();
                message.AddRange(MessageHeader);
                message.AddRange(new byte[0x20]);
                message.AddRange(Payload);
                Span<byte> output = new Span<byte>(new byte[0x20]);
                LibHac.Crypto.Sha256.GenerateSha256Hash(message.ToArray(), output);
                // LogMsg("Message: ", message.ToArray());
                // LogMsg("Message Hash: ", output.ToArray());
                if (LibHac.Common.Utilities.ArraysEqual(output.ToArray(), Hash)) {
                    return message.ToArray();
                }
                else {
                    LogMsg("Generated message hash: ", output.ToArray());
                    LogMsg("Expected message hash: ", Hash);
                    throw new Exception();
                }
            }
            // set
            // {
            //     if (value != null && value.Length == AdvertisementFields.MessageHeaderLength + (0x20 * 2)) {
            //         MessageHeader = value.Take(AdvertisementFields.MessageHeaderLength).ToArray();
            //         Payload = value.Skip(AdvertisementFields.MessageHeaderLength + 0x20).ToArray();
            //         if ()
            //     }
            //     else {
            //         throw new ArgumentException();
            //     }
            // }
        }

        public LdnNetworkInfo Info {
            get => LdnHelper.FromBytes<LdnNetworkInfo>(Payload);
            set => Payload = LdnHelper.StructureToByteArray(value);
        }

        private byte[] Encrypt(byte[] data) {
            if (Encryption == 1)
                return data;

            byte[] key = EncryptionHelper.DeriveKey(StructConvHelper.StructToBytes<NetworkId>(Endianness.BigEndian, Header), AdvertisementKeySource);
            // LogMsg($"Encrypt: Data length: {data.Length}");
            Span<byte> output = new Span<byte>(new byte[data.Length]);
            LibHac.Crypto.Aes.EncryptCtr128(data, output, key, Nonce);
            return output.ToArray();
        }

        private byte[] Decrypt(byte[] data) {
            if (Encryption == 1)
                return data;

            // LogMsg($"Decrypt: Data length: {data.Length}");
            byte[] key = EncryptionHelper.DeriveKey(StructConvHelper.StructToBytes<NetworkId>(Endianness.BigEndian, Header), AdvertisementKeySource);
            // LogMsg($"Key: ", key);
            Span<byte> output = new Span<byte>(new byte[data.Length]);
            LibHac.Crypto.Aes.DecryptCtr128(data, output, key, Nonce);
            return output.ToArray();
        }

        public byte[] Encode() {
            return PacketHeader.ActualBytes();
        }

        public AdvertisementFrame() {
            PacketHeader = new ByteArraySegment(new byte[1388]);
        }

        private AdvertisementFrame(ByteArraySegment byteArraySegment) {
            PacketHeader = byteArraySegment;

            // LogMsg($"Data[{byteArraySegment.Length}]: ", byteArraySegment.ActualBytes());

            // Log all Properties
            // LogMsg($"MessageHeader[{MessageHeader.Length}]: ", MessageHeader);
            LogMsg($"HeaderData[{AdvertisementFields.SessionInfoLength}]: ", PacketHeader.Skip(AdvertisementFields.SessionInfoPosition).Take(AdvertisementFields.SessionInfoLength).ToArray());
            // LogMsg($"Header[{Marshal.SizeOf<SessionInfo>()}]: ", Header);
            // LogMsg($"Version: ", Version);
            // LogMsg($"Encryption: ", Encryption);
            LogMsg($"BodySize: {BodySize}");
            // LogMsg($"Nonce: [{Nonce.Length}]", Nonce);
            // LogMsg($"Body data: ", PacketHeader.Skip(AdvertisementFields.BodyPosition).Take(AdvertisementFields.HashLength + BodySize).ToArray());
            // LogMsg($"Body[{Body.Length}]: ", Body);
            // LogMsg($"Hash[{Hash.Length}]: ", Hash);
            // LogMsg($"Payload[{Payload.Length}]: ", Payload);
            // LogMsg($"Message[{Message.Length}]: ", Message);
            // LogMsg($"Info[{Marshal.SizeOf<LdnNetworkInfo>()}]: ", Info);
        }

        public static bool TryGetAdvertisementFrame(ActionFrame action, out AdvertisementFrame adFrame) {
            if (action.PayloadDataSegment.Take(LdnHeader.Length).SequenceEqual(LdnHeader)) {
                adFrame = new AdvertisementFrame(action.PayloadDataSegment);
                return true;
            }
            adFrame = null;
            return false;
        }
    }
}
