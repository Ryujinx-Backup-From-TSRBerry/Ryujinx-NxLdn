using LibHac.Crypto;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Frames
{
    public class NewAdvertisementFrame
    {
        internal static readonly byte[] OuiMagic = new byte[] { 0x00, 0x22, 0xAA };
        internal static readonly byte[] AdvertisementKey = { 0x19, 0x18, 0x84, 0x74, 0x3e, 0x24, 0xc7, 0x7d, 0x87, 0xc6, 0x9e, 0x42, 0x07, 0xd0, 0xc4, 0x38 };

        internal struct AdvertisementFrameHeader
        {
            public byte Category;
            public Array3<byte> Oui;
            public byte ProtocolId;
            public byte Padding1;
            public ushort PacketType;
            public ushort Unknown;
            public ushort Padding2;

            public AdvertisementFrameHeader()
            {
                Category = 127;
                Oui = new Array3<byte>();
                ProtocolId = 0x04;
                Padding1 = 0;
                PacketType = (ushort)Types.PacketType.Advertisement;
                Unknown = 0;
                Padding2 = 0;

                OuiMagic.CopyTo(Oui.AsSpan());
            }
        }

        internal struct SessionInfo
        {
            public ulong LocalCommunicationId;
            public ushort Padding1;
            public ushort GameMode;
            public uint Padding2;
            public Array16<byte> Ssid;
        }

        internal enum EncryptionType : byte
        {
            Invalid,
            Plaintext,
            Encrypted
        }

        internal struct AdvertisementFramePayload
        {
            public SessionInfo SessionInfo;
            public byte LdnVersion;
            public EncryptionType EncryptionType;
            public ushort AdvertisementDataSize;
            public uint Nonce;

            public void ReverseEndianness()
            {
                AdvertisementDataSize = BinaryPrimitives.ReverseEndianness(AdvertisementDataSize);
            }
        }

        internal struct Participant
        {
            public Array4<byte> IpAddress;
            public Array6<byte> MacAddress;
            public byte IsConnected;
            public byte Padding1;
            public Array32<byte> Username;
            public ushort ApplicationCommunicationVersion;
            public Array10<byte> Padding2;

            public void ReverseEndianness()
            {
                ApplicationCommunicationVersion = BinaryPrimitives.ReverseEndianness(ApplicationCommunicationVersion);
            }
        }

        internal struct AdvertisementData
        {
            public Array32<byte> Sha256Hash;
            public Array16<byte> NetworkKey;
            public ushort SecurityLevel;
            public byte StationAcceptPolicy;
            public Array3<byte> Padding1;
            public byte ParticipantCountMax;
            public byte ParticipantCount;
            public Array8<Participant> Participants;
            public ushort Padding2;
            public ushort ApplicationDataSize;
            private ApplicationDataArrayStruct _applicationData;
            private Padding3ArrayStruct _padding3;
            public ulong AuthenticationToken;

            [StructLayout(LayoutKind.Sequential, Size = 0x180)]
            private struct ApplicationDataArrayStruct { }

            public Span<byte> ApplicationData => SpanHelpers.AsSpan<ApplicationDataArrayStruct, byte>(ref _applicationData);

            [StructLayout(LayoutKind.Sequential, Size = 0x19C)]
            private struct Padding3ArrayStruct { }

            public Span<byte> Padding3 => SpanHelpers.AsSpan<Padding3ArrayStruct, byte>(ref _padding3);

            public void ReverseEndianness()
            {
                SecurityLevel = BinaryPrimitives.ReverseEndianness(SecurityLevel);
                ApplicationDataSize = BinaryPrimitives.ReverseEndianness(ApplicationDataSize);
                AuthenticationToken = BinaryPrimitives.ReverseEndianness(AuthenticationToken);

                for (int i = 0; i < Participants.Length; i++)
                {
                    Participants[i].ReverseEndianness();
                }
            }
        }

        private AdvertisementFrameHeader _header;
        private AdvertisementFramePayload _payload;
        private AdvertisementData _data;
        private ushort _channel;
        public NewAdvertisementFrame(ushort channel, byte[] frameData)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"AdvertisementFrame Found!");

            // Check if the data size is enough.
            if (frameData.Length > Marshal.SizeOf<AdvertisementFrameHeader>())
            {
                ref AdvertisementFrameHeader header = ref MemoryMarshal.Cast<byte, AdvertisementFrameHeader>(frameData.AsSpan()[..Marshal.SizeOf<AdvertisementFrameHeader>()])[0];
                _header = header;

                // Check if the AdvertisementFrameHeader is a LDN frame.
                if (header.Equals(new AdvertisementFrameHeader()))
                {
                    ref AdvertisementFramePayload payload = ref MemoryMarshal.Cast<byte, AdvertisementFramePayload>(frameData.AsSpan().Slice(Marshal.SizeOf<AdvertisementFrameHeader>(), Marshal.SizeOf<AdvertisementFramePayload>()))[0];
                    _payload = payload;
                    _payload.ReverseEndianness();

                    if (_payload.AdvertisementDataSize == 0x500)
                    {
                        int start = Marshal.SizeOf<AdvertisementFrameHeader>() + Marshal.SizeOf<AdvertisementFramePayload>();
                        Span<byte> encryptedData = frameData.AsSpan()[start..frameData.Length];
                        Span<byte> decryptedData = new byte[encryptedData.Length];

                        if (_payload.EncryptionType == EncryptionType.Encrypted)
                        {
                            byte[] key = EncryptionHelper.DeriveKey(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref _payload.SessionInfo, 1)).ToArray(), AdvertisementKey);
                            byte[] nonce = MemoryMarshal.Cast<uint, byte>(MemoryMarshal.CreateSpan(ref _payload.Nonce, 1)).ToArray();
                            Array.Resize(ref nonce, 0x10); // Align Nonce on 10 bytes.

                            Aes.DecryptCtr128(encryptedData, decryptedData, key, nonce);
                        }
                        else
                        {
                            encryptedData.CopyTo(decryptedData);
                        }

                        ref AdvertisementData data = ref MemoryMarshal.Cast<byte, AdvertisementData>(decryptedData)[0];
                        _data = data;
                        _data.ReverseEndianness();

                        // TODO: Check SHA256 Hash.

                        _channel = channel;
                    }
                }
            }
        }

        public NetworkInfo GenerateNetworkInfo(byte[] macAddress)
        {
            NetworkInfo networkInfo = new()
            {
                NetworkId = new()
                {
                    IntentId = new()
                    {
                        LocalCommunicationId = (long)_payload.SessionInfo.LocalCommunicationId,
                        SceneId = _payload.SessionInfo.GameMode
                    },
                    SessionId = _payload.SessionInfo.Ssid
                },
                Common = new()
                {
                    Ssid = new()
                    {
                        Length = (byte)_payload.SessionInfo.Ssid.Length,
                    },
                    Channel = _channel,
                    LinkLevel = 3,
                    NetworkType = 2,
                },
                Ldn = new()
                {
                    SecurityParameter = _data.NetworkKey,
                    SecurityMode = _data.SecurityLevel,
                    StationAcceptPolicy = _data.StationAcceptPolicy,
                    NodeCountMax = _data.ParticipantCountMax,
                    NodeCount = _data.ParticipantCount,
                    Nodes = new Array8<NodeInfo>(),
                    AdvertiseDataSize = _data.ApplicationDataSize,
                    Unknown2 = new Array140<byte>(),
                    AuthenticationId = _data.AuthenticationToken
                }
            };

            macAddress.CopyTo(networkInfo.Common.MacAddress.AsSpan());
            _payload.SessionInfo.Ssid.AsSpan().CopyTo(networkInfo.Common.Ssid.Name.AsSpan());
            _data.ApplicationData.CopyTo(networkInfo.Ldn.AdvertiseData.AsSpan());

            for (int i = 0; i < networkInfo.Ldn.Nodes.Length; i++)
            {
                networkInfo.Ldn.Nodes[i] = new NodeInfo();
                networkInfo.Ldn.Nodes[i].Ipv4Address = BitConverter.ToUInt32(_data.Participants[i].IpAddress.AsSpan());
                networkInfo.Ldn.Nodes[i].MacAddress = _data.Participants[i].MacAddress;
                networkInfo.Ldn.Nodes[i].NodeId = (byte)i;
                networkInfo.Ldn.Nodes[i].IsConnected = _data.Participants[i].IsConnected;
                _data.Participants[i].Username.AsSpan().CopyTo(networkInfo.Ldn.Nodes[i].UserName.AsSpan());
                networkInfo.Ldn.Nodes[i].LocalCommunicationVersion = _data.Participants[i].ApplicationCommunicationVersion;
                networkInfo.Ldn.Nodes[i].Reserved2 = new Array16<byte>();
            }

            return networkInfo;
        }
    }
}