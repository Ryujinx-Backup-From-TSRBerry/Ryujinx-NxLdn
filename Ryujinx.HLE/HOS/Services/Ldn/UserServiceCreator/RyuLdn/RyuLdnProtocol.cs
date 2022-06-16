using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn
{
    internal class RyuLdnProtocol
    {
        private const byte CurrentProtocolVersion = 1;

        private const int Magic = 1313098834;

        private const int MaxPacketSize = 131072;

        private readonly int _headerSize = Marshal.SizeOf<LdnHeader>();

        private byte[] _buffer = new byte[131072];

        private int _bufferEnd;

        public event Action<LdnHeader, InitializeMessage> Initialize;

        public event Action<LdnHeader, PassphraseMessage> Passphrase;

        public event Action<LdnHeader, NetworkInfo> Connected;

        public event Action<LdnHeader, NetworkInfo> SyncNetwork;

        public event Action<LdnHeader, NetworkInfo> ScanReply;

        public event Action<LdnHeader> ScanReplyEnd;

        public event Action<LdnHeader, DisconnectMessage> Disconnected;

        public event Action<LdnHeader, ExternalProxyConfig> ExternalProxy;

        public event Action<LdnHeader, ExternalProxyConnectionState> ExternalProxyState;

        public event Action<LdnHeader, ExternalProxyToken> ExternalProxyToken;

        public event Action<LdnHeader, CreateAccessPointRequest, byte[]> CreateAccessPoint;

        public event Action<LdnHeader, CreateAccessPointPrivateRequest, byte[]> CreateAccessPointPrivate;

        public event Action<LdnHeader, RejectRequest> Reject;

        public event Action<LdnHeader> RejectReply;

        public event Action<LdnHeader, SetAcceptPolicyRequest> SetAcceptPolicy;

        public event Action<LdnHeader, byte[]> SetAdvertiseData;

        public event Action<LdnHeader, ConnectRequest> Connect;

        public event Action<LdnHeader, ConnectPrivateRequest> ConnectPrivate;

        public event Action<LdnHeader, ScanFilter> Scan;

        public event Action<LdnHeader, ProxyConfig> ProxyConfig;

        public event Action<LdnHeader, ProxyConnectRequest> ProxyConnect;

        public event Action<LdnHeader, ProxyConnectResponse> ProxyConnectReply;

        public event Action<LdnHeader, ProxyDataHeader, byte[]> ProxyData;

        public event Action<LdnHeader, ProxyDisconnectMessage> ProxyDisconnect;

        public event Action<LdnHeader, NetworkErrorMessage> NetworkError;

        public event Action<LdnHeader, PingMessage> Ping;

        public void Reset()
        {
            _bufferEnd = 0;
        }

        public void Read(byte[] data, int offset, int size)
        {
            int index = 0;
            while (index < size)
            {
                if (_bufferEnd < _headerSize)
                {
                    int copyable2 = Math.Min(size - index, Math.Min(size, _headerSize - _bufferEnd));
                    Array.Copy(data, index + offset, _buffer, _bufferEnd, copyable2);
                    index += copyable2;
                    _bufferEnd += copyable2;
                }
                if (_bufferEnd >= _headerSize)
                {
                    LdnHeader ldnHeader = LdnHelper.FromBytes<LdnHeader>(_buffer);
                    if (ldnHeader.Magic != 1313098834)
                    {
                        throw new InvalidOperationException("Invalid magic number in received packet.");
                    }
                    if (ldnHeader.Version != 1)
                    {
                        throw new InvalidOperationException($"Protocol version mismatch. Expected ${(byte)1}, was ${ldnHeader.Version}.");
                    }
                    int finalSize = _headerSize + ldnHeader.DataSize;
                    if (finalSize >= 131072)
                    {
                        throw new InvalidOperationException($"Max packet size {131072} exceeded.");
                    }
                    int copyable = Math.Min(size - index, Math.Min(size, finalSize - _bufferEnd));
                    Array.Copy(data, index + offset, _buffer, _bufferEnd, copyable);
                    index += copyable;
                    _bufferEnd += copyable;
                    if (finalSize == _bufferEnd)
                    {
                        byte[] ldnData = new byte[ldnHeader.DataSize];
                        Array.Copy(_buffer, _headerSize, ldnData, 0, ldnData.Length);
                        DecodeAndHandle(ldnHeader, ldnData);
                        Reset();
                    }
                }
            }
        }

        private T ParseDefault<T>(byte[] data)
        {
            return LdnHelper.FromBytes<T>(data);
        }

        private (T, byte[]) ParseWithData<T>(byte[] data)
        {
            int size = Marshal.SizeOf(default(T));
            byte[] remainder = new byte[data.Length - size];
            if (remainder.Length != 0)
            {
                Array.Copy(data, size, remainder, 0, remainder.Length);
            }
            return (LdnHelper.FromBytes<T>(data), remainder);
        }

        private void DecodeAndHandle(LdnHeader header, byte[] data)
        {
            switch (header.Type)
            {
                case 0:
                    this.Initialize?.Invoke(header, ParseDefault<InitializeMessage>(data));
                    break;
                case 1:
                    this.Passphrase?.Invoke(header, ParseDefault<PassphraseMessage>(data));
                    break;
                case 15:
                    this.Connected?.Invoke(header, ParseDefault<NetworkInfo>(data));
                    break;
                case 7:
                    this.SyncNetwork?.Invoke(header, ParseDefault<NetworkInfo>(data));
                    break;
                case 11:
                    this.ScanReply?.Invoke(header, ParseDefault<NetworkInfo>(data));
                    break;
                case 12:
                    this.ScanReplyEnd?.Invoke(header);
                    break;
                case 16:
                    this.Disconnected?.Invoke(header, ParseDefault<DisconnectMessage>(data));
                    break;
                case 4:
                    this.ExternalProxy?.Invoke(header, ParseDefault<ExternalProxyConfig>(data));
                    break;
                case 6:
                    this.ExternalProxyState?.Invoke(header, ParseDefault<ExternalProxyConnectionState>(data));
                    break;
                case 5:
                    this.ExternalProxyToken?.Invoke(header, ParseDefault<ExternalProxyToken>(data));
                    break;
                case 2:
                    var (packet, extraData) = ParseWithData<CreateAccessPointRequest>(data);
                    this.CreateAccessPoint?.Invoke(header, packet, extraData);
                    break;
                case 3:
                    var (packet2, extraData2) = ParseWithData<CreateAccessPointPrivateRequest>(data);
                    this.CreateAccessPointPrivate?.Invoke(header, packet2, extraData2);
                    break;
                case 8:
                    this.Reject?.Invoke(header, ParseDefault<RejectRequest>(data));
                    break;
                case 9:
                    this.RejectReply?.Invoke(header);
                    break;
                case 22:
                    this.SetAcceptPolicy?.Invoke(header, ParseDefault<SetAcceptPolicyRequest>(data));
                    break;
                case 23:
                    this.SetAdvertiseData?.Invoke(header, data);
                    break;
                case 13:
                    this.Connect?.Invoke(header, ParseDefault<ConnectRequest>(data));
                    break;
                case 14:
                    this.ConnectPrivate?.Invoke(header, ParseDefault<ConnectPrivateRequest>(data));
                    break;
                case 10:
                    this.Scan?.Invoke(header, ParseDefault<ScanFilter>(data));
                    break;
                case 17:
                    this.ProxyConfig?.Invoke(header, ParseDefault<ProxyConfig>(data));
                    break;
                case 18:
                    this.ProxyConnect?.Invoke(header, ParseDefault<ProxyConnectRequest>(data));
                    break;
                case 19:
                    this.ProxyConnectReply?.Invoke(header, ParseDefault<ProxyConnectResponse>(data));
                    break;
                case 20:
                    var (packet3, extraData3) = ParseWithData<ProxyDataHeader>(data);
                    this.ProxyData?.Invoke(header, packet3, extraData3);
                    break;
                case 21:
                    this.ProxyDisconnect?.Invoke(header, ParseDefault<ProxyDisconnectMessage>(data));
                    break;
                case 254:
                    this.Ping?.Invoke(header, ParseDefault<PingMessage>(data));
                    break;
                case byte.MaxValue:
                    this.NetworkError?.Invoke(header, ParseDefault<NetworkErrorMessage>(data));
                    break;
            }
        }

        private LdnHeader GetHeader(PacketId type, int dataSize)
        {
            LdnHeader result = default(LdnHeader);
            result.Magic = 1313098834u;
            result.Version = 1;
            result.Type = (byte)type;
            result.DataSize = dataSize;
            return result;
        }

        public byte[] Encode(PacketId type)
        {
            return LdnHelper.StructureToByteArray(GetHeader(type, 0));
        }

        public byte[] Encode(PacketId type, byte[] data)
        {
            byte[] result = LdnHelper.StructureToByteArray(GetHeader(type, data.Length), data.Length);
            Array.Copy(data, 0, result, Marshal.SizeOf<LdnHeader>(), data.Length);
            return result;
        }

        public byte[] Encode<T>(PacketId type, T packet)
        {
            byte[] packetData = LdnHelper.StructureToByteArray(packet);
            byte[] result = LdnHelper.StructureToByteArray(GetHeader(type, packetData.Length), packetData.Length);
            Array.Copy(packetData, 0, result, Marshal.SizeOf<LdnHeader>(), packetData.Length);
            return result;
        }

        public byte[] Encode<T>(PacketId type, T packet, byte[] data)
        {
            byte[] packetData = LdnHelper.StructureToByteArray(packet);
            byte[] result = LdnHelper.StructureToByteArray(GetHeader(type, packetData.Length + data.Length), packetData.Length + data.Length);
            Array.Copy(packetData, 0, result, Marshal.SizeOf<LdnHeader>(), packetData.Length);
            Array.Copy(data, 0, result, Marshal.SizeOf<LdnHeader>() + packetData.Length, data.Length);
            return result;
        }
    }
}
