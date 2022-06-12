using PacketDotNet.Ieee80211;
using PacketDotNet.Utils;
using PacketDotNet.Utils.Converters;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types;
using System;
using System.Buffers.Binary;

using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets
{
    // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L181
    // Length: 1364 | in Ryujinx: 1368
    internal sealed class AdvertisementFrame
    {

        private static readonly byte[] AdvertisementKeySource = { 0x19, 0x18, 0x84, 0x74, 0x3e, 0x24, 0xc7, 0x7d, 0x87, 0xc6, 0x9e, 0x42, 0x07, 0xd0, 0xc4, 0x38 };

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
        private byte[] MessageHeader
        {
            get => PacketHeader.Skip(AdvertisementFields.MessageHeaderPosition).Take(AdvertisementFields.MessageHeaderLength).ToArray();
            set
            {
                if (value != null && value.Length > 0 && value.Length <= AdvertisementFields.MessageHeaderLength)
                {
                    Array.Resize(ref value, AdvertisementFields.MessageHeaderLength);
                    value.CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.MessageHeaderPosition);
                }
                else if (value == null)
                {
                    byte[] fillArr = new byte[AdvertisementFields.MessageHeaderLength];
                    Array.Fill<byte>(fillArr, 0);
                    fillArr.CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.MessageHeaderPosition);
                }
                else
                {
                    throw new OverflowException();
                }
            }
        }

        private NetworkId _header
        {
            get => LdnHelper.FromBytes<NetworkId>(
                    PacketHeader.Skip(AdvertisementFields.SessionInfoPosition).Take(AdvertisementFields.SessionInfoLength).ToArray()
                );
            set => LdnHelper.StructureToByteArray(value).CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.SessionInfoPosition);
        }

        public NetworkId Header
        {
            get
            {
                NetworkId netId = _header;
                netId.IntentId.LocalCommunicationId = BinaryPrimitives.ReverseEndianness(netId.IntentId.LocalCommunicationId);
                return netId;
            }
            set
            {
                // TODO: Does this affect the passed in value?
                value.IntentId.LocalCommunicationId = BinaryPrimitives.ReverseEndianness(value.IntentId.LocalCommunicationId);
                _header = value;
            }
        }

        public byte Version
        {
            get => PacketHeader.Skip(AdvertisementFields.VersionPosition).First();
            set
            {
                // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L253
                if (new byte[] { 2, 3 }.Contains(value))
                {
                    PacketHeader.Bytes[PacketHeader.Offset + AdvertisementFields.VersionPosition] = value;
                }
                else
                {
                    throw new ArgumentException();
                }
            }
        }

        public byte Encryption
        {
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

        private ushort BodySize
        {
            get => EndianBitConverter.Big.ToUInt16(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.BodySizePosition);
            set
            {
                // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L262
                if (value == AdvertisementFields.BodySizeValue)
                {
                    EndianBitConverter.Big.CopyBytes(value, PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.BodySizePosition);
                }
                else
                {
                    throw new ArgumentException();
                }
            }
        }

        public byte[] Nonce
        {
            get => PacketHeader.Skip(AdvertisementFields.NoncePosition).Take(AdvertisementFields.NonceLength).ToArray();
            set
            {
                if (value != null && value.Length > 0 && value.Length <= AdvertisementFields.NonceLength)
                {
                    Array.Resize<byte>(ref value, AdvertisementFields.NonceLength);
                    value.CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.NoncePosition);
                }
                else if (value == null)
                {
                    byte[] fillArr = new byte[AdvertisementFields.NonceLength];
                    Array.Fill<byte>(fillArr, 0);
                    fillArr.CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.NoncePosition);
                }
                else
                {
                    throw new OverflowException();
                }
            }
        }

        private byte[] Body
        {
            get => Decrypt(PacketHeader.Skip(AdvertisementFields.BodyPosition).Take(AdvertisementFields.HashLength + BodySize).ToArray());
            set
            {
                if (value != null && value.Length > 0 && value.Length <= AdvertisementFields.HashLength + BodySize)
                {
                    Array.Resize<byte>(ref value, AdvertisementFields.HashLength + BodySize);
                    Encrypt(value).CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.BodyPosition);
                }
                else if (value == null)
                {
                    byte[] fillArr = new byte[AdvertisementFields.HashLength + BodySize];
                    Array.Fill<byte>(fillArr, 0);
                    Encrypt(fillArr).CopyTo(PacketHeader.Bytes, PacketHeader.Offset + AdvertisementFields.BodyPosition);
                }
                else
                {
                    throw new OverflowException();
                }
            }
        }

        // Sha
        private byte[] Hash
        {
            get => Body.Take(AdvertisementFields.HashLength).ToArray();
            set
            {
                if (value != null && value.Length > 0 && value.Length <= AdvertisementFields.HashLength)
                {
                    Array.Resize<byte>(ref value, AdvertisementFields.HashLength);
                    byte[] body = Body;
                    value.CopyTo(body, 0);
                    Body = body;
                }
                else if (value == null)
                {
                    byte[] fillArr = new byte[AdvertisementFields.HashLength];
                    Array.Fill<byte>(fillArr, 0);
                    byte[] body = Body;
                    fillArr.CopyTo(body, 0);
                    Body = body;
                }
                else
                {
                    throw new OverflowException();
                }
            }
        }


        // Info
        private byte[] Payload
        {
            get => Body.Skip(AdvertisementFields.HashLength).ToArray();
            set
            {
                if (value != null && value.Length > 0 && value.Length <= BodySize)
                {
                    Array.Resize<byte>(ref value, BodySize);
                    byte[] body = Body;
                    value.CopyTo(body, AdvertisementFields.HashLength);
                    Body = body;
                }
                else if (value == null)
                {
                    byte[] fillArr = new byte[BodySize];
                    Array.Fill<byte>(fillArr, 0);
                    byte[] body = Body;
                    fillArr.CopyTo(body, AdvertisementFields.HashLength);
                    Body = body;
                }
                else
                {
                    throw new OverflowException();
                }
            }
        }

        public LdnNetworkInfo Info
        {
            get
            {
                NxLdnNetworkInfo ldnInfo = LdnHelper.FromBytes<NxLdnNetworkInfo>(Payload);
                ldnInfo.SecurityMode = BinaryPrimitives.ReverseEndianness(ldnInfo.SecurityMode);
                for (int i = 0; i < ldnInfo.Nodes.Length; i++)
                {
                    ldnInfo.Nodes[i].Ipv4Address = BinaryPrimitives.ReverseEndianness(ldnInfo.Nodes[i].Ipv4Address);
                    ldnInfo.Nodes[i].LocalCommunicationVersion = BinaryPrimitives.ReverseEndianness(ldnInfo.Nodes[i].LocalCommunicationVersion);
                }
                ldnInfo.AdvertiseDataSize = BinaryPrimitives.ReverseEndianness(ldnInfo.AdvertiseDataSize);
                ldnInfo.AuthenticationId = BinaryPrimitives.ReverseEndianness(ldnInfo.AuthenticationId);
                return ldnInfo.ToLdnNetworkInfo();
            }
            set
            {
                // TODO: Does this affect the passed in value?
                value.SecurityMode = BinaryPrimitives.ReverseEndianness(value.SecurityMode);
                for (int i = 0; i < value.Nodes.Length; i++)
                {
                    value.Nodes[i].Ipv4Address = BinaryPrimitives.ReverseEndianness(value.Nodes[i].Ipv4Address);
                    value.Nodes[i].LocalCommunicationVersion = BinaryPrimitives.ReverseEndianness(value.Nodes[i].LocalCommunicationVersion);
                }
                value.AdvertiseDataSize = BinaryPrimitives.ReverseEndianness(value.AdvertiseDataSize);
                value.AuthenticationId = BinaryPrimitives.ReverseEndianness(value.AuthenticationId);
                BodySize = (ushort)Marshal.SizeOf<NxLdnNetworkInfo>();
                Payload = LdnHelper.StructureToByteArray(NxLdnNetworkInfo.FromLdnNetworkInfo(value));
            }
        }

        private byte[] Encrypt(byte[] data)
        {
            if (Encryption == 1)
                return data;

            byte[] key = EncryptionHelper.DeriveKey(LdnHelper.StructureToByteArray(_header), AdvertisementKeySource);
            // LogMsg($"Encrypt: Data length: {data.Length}");
            Span<byte> output = new Span<byte>(new byte[data.Length]);
            LibHac.Crypto.Aes.EncryptCtr128(data, output, key, Nonce);
            return output.ToArray();
        }

        private byte[] Decrypt(byte[] data)
        {
            if (Encryption == 1)
                return data;

            // LogMsg($"Decrypt: Data length: {data.Length}");
            byte[] key = EncryptionHelper.DeriveKey(LdnHelper.StructureToByteArray(_header), AdvertisementKeySource);
            // LogMsg($"Key: ", key);
            Span<byte> output = new Span<byte>(new byte[data.Length]);
            LibHac.Crypto.Aes.DecryptCtr128(data, output, key, Nonce);
            return output.ToArray();
        }

        public byte[] Encode()
        {
            return PacketHeader.ActualBytes();
        }

        public void WriteHash()
        {
            Hash = CalcHash().ToArray();
        }

        private Span<byte> CalcHash()
        {
            List<byte> message = new List<byte>();
            message.AddRange(MessageHeader);
            message.AddRange(new byte[0x20]);
            message.AddRange(Payload);
            LogMsg("Message: ", message.ToArray());
            Span<byte> output = new Span<byte>(new byte[AdvertisementFields.HashLength]);
            LibHac.Crypto.Sha256.GenerateSha256Hash(message.ToArray(), output);
            return output;
        }

        public bool CheckHash()
        {
            Span<byte> actualHash = CalcHash();
            LogMsg("Message Hash: ", actualHash.ToArray());
            return LibHac.Common.Utilities.ArraysEqual(actualHash.ToArray(), Hash);
        }

        public void LogProps()
        {
            LogMsg($"MessageHeader[{MessageHeader.Length}]: ", MessageHeader);
            // LogMsg($"HeaderData[{AdvertisementFields.SessionInfoLength}]: ", PacketHeader.Skip(AdvertisementFields.SessionInfoPosition).Take(AdvertisementFields.SessionInfoLength).ToArray());
            // LogMsg($"Header[{Marshal.SizeOf<SessionInfo>()}]: ", Header);
            LogMsg($"Version: ", Version);
            LogMsg($"Encryption: ", Encryption);
            LogMsg($"BodySize: {BodySize}");
            LogMsg($"Nonce: [{Nonce.Length}]", Nonce);
            // LogMsg($"Body data: ", PacketHeader.Skip(AdvertisementFields.BodyPosition).Take(AdvertisementFields.HashLength + BodySize).ToArray());
            // LogMsg($"Body[{Body.Length}]: ", Body);
            // LogMsg($"Hash[{Hash.Length}]: ", Hash);
            // LogMsg($"Payload[{Payload.Length}]: ", Payload);
            // LogMsg($"Message[{Message.Length}]: ", Message);
            // LogMsg($"Info[{Marshal.SizeOf<LdnNetworkInfo>()}]: ", Info);
        }

        public AdvertisementFrame()
        {
            PacketHeader = new ByteArraySegment(new byte[1368]);
            LdnHelper.StructureToByteArray(HeaderFields.Action).CopyTo(PacketHeader.Bytes, PacketHeader.Offset);
        }

        private AdvertisementFrame(ByteArraySegment byteArraySegment)
        {
            PacketHeader = byteArraySegment;

            // LogMsg($"Data[{byteArraySegment.Length}]: ", byteArraySegment.ActualBytes());
            LogProps();
        }

        public static bool TryGetAdvertisementFrame(ActionFrame action, out AdvertisementFrame adFrame)
        {
            if (action.PayloadDataSegment.Take(Marshal.SizeOf(HeaderFields.Action)).SequenceEqual(LdnHelper.StructureToByteArray(HeaderFields.Action)))
            {
                adFrame = new AdvertisementFrame(action.PayloadDataSegment);
                return adFrame.CheckHash();
            }
            else
            {
                LogMsg("Incorrect header:", action.PayloadDataSegment.Take(Marshal.SizeOf(HeaderFields.Action)).ToArray());
                LogMsg("Expected header:", LdnHelper.StructureToByteArray(HeaderFields.Action));
            }
            adFrame = null;
            return false;
        }
    }
}
