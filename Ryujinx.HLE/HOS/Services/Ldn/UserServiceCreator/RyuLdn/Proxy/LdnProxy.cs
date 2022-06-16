using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Proxy
{
    internal class LdnProxy : IDisposable
    {
        private List<LdnProxySocket> _sockets = new List<LdnProxySocket>();

        private Dictionary<ProtocolType, EphemeralPortPool> _ephemeralPorts = new Dictionary<ProtocolType, EphemeralPortPool>();

        private IProxyClient _parent;

        private RyuLdnProtocol _protocol;

        private uint _subnetMask;

        private uint _localIp;

        private uint _broadcast;

        public EndPoint LocalEndpoint { get; }

        public IPAddress LocalAddress { get; }

        public LdnProxy(ProxyConfig config, IProxyClient client, RyuLdnProtocol protocol)
        {
            _parent = client;
            _protocol = protocol;
            _ephemeralPorts[ProtocolType.Udp] = new EphemeralPortPool();
            _ephemeralPorts[ProtocolType.Tcp] = new EphemeralPortPool();
            byte[] address = BitConverter.GetBytes(config.ProxyIp);
            Array.Reverse(address);
            LocalAddress = new IPAddress(address);
            _subnetMask = config.ProxySubnetMask;
            _localIp = config.ProxyIp;
            _broadcast = _localIp | ~_subnetMask;
            RegisterHandlers(protocol);
        }

        public bool Supported(AddressFamily domain, SocketType type, ProtocolType protocol)
        {
            if (protocol == ProtocolType.Tcp)
            {
                Logger.Error?.PrintMsg(LogClass.ServiceLdn, "Tcp proxy networking is untested. Please report this game so that it can be tested.");
            }
            if (domain == AddressFamily.InterNetwork)
            {
                if (protocol != ProtocolType.Tcp)
                {
                    return protocol == ProtocolType.Udp;
                }
                return true;
            }
            return false;
        }

        private void RegisterHandlers(RyuLdnProtocol protocol)
        {
            protocol.ProxyConnect += HandleConnectionRequest;
            protocol.ProxyConnectReply += HandleConnectionResponse;
            protocol.ProxyData += HandleData;
            protocol.ProxyDisconnect += HandleDisconnect;
            _protocol = protocol;
        }

        public void UnregisterHandlers(RyuLdnProtocol protocol)
        {
            protocol.ProxyConnect -= HandleConnectionRequest;
            protocol.ProxyConnectReply -= HandleConnectionResponse;
            protocol.ProxyData -= HandleData;
            protocol.ProxyDisconnect -= HandleDisconnect;
        }

        public ushort GetEphemeralPort(ProtocolType type)
        {
            return _ephemeralPorts[type].Get();
        }

        public void ReturnEphemeralPort(ProtocolType type, ushort port)
        {
            _ephemeralPorts[type].Return(port);
        }

        public void RegisterSocket(LdnProxySocket socket)
        {
            lock (_sockets)
            {
                _sockets.Add(socket);
            }
        }

        public void UnregisterSocket(LdnProxySocket socket)
        {
            lock (_sockets)
            {
                _sockets.Remove(socket);
            }
        }

        private void ForRoutedSockets(ProxyInfo info, Action<LdnProxySocket> action)
        {
            lock (_sockets)
            {
                foreach (LdnProxySocket socket in _sockets)
                {
                    if (socket.ProtocolType == info.Protocol)
                    {
                        IPEndPoint endpoint = socket.LocalEndPoint as IPEndPoint;
                        if (endpoint != null && endpoint.Port == info.DestPort)
                        {
                            action(socket);
                        }
                    }
                }
            }
        }

        public void HandleConnectionRequest(LdnHeader header, ProxyConnectRequest request)
        {
            ForRoutedSockets(request.Info, delegate (LdnProxySocket socket)
            {
                socket.HandleConnectRequest(request);
            });
        }

        public void HandleConnectionResponse(LdnHeader header, ProxyConnectResponse response)
        {
            ForRoutedSockets(response.Info, delegate (LdnProxySocket socket)
            {
                socket.HandleConnectResponse(response);
            });
        }

        private string IPToString(uint ip)
        {
            return $"{(byte)(ip >> 24)}.{(byte)(ip >> 16)}.{(byte)(ip >> 8)}.{(byte)ip}";
        }

        public void HandleData(LdnHeader header, ProxyDataHeader proxyHeader, byte[] data)
        {
            ProxyDataPacket packet = new ProxyDataPacket
            {
                Header = proxyHeader,
                Data = data
            };
            ForRoutedSockets(proxyHeader.Info, delegate (LdnProxySocket socket)
            {
                socket.IncomingData(packet);
            });
        }

        public void HandleDisconnect(LdnHeader header, ProxyDisconnectMessage disconnect)
        {
            ForRoutedSockets(disconnect.Info, delegate (LdnProxySocket socket)
            {
                socket.HandleDisconnect(disconnect);
            });
        }

        private uint GetIpV4(IPEndPoint endpoint)
        {
            if (endpoint.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new NotSupportedException();
            }
            byte[] addressBytes = endpoint.Address.GetAddressBytes();
            Array.Reverse(addressBytes);
            return BitConverter.ToUInt32(addressBytes);
        }

        private ProxyInfo MakeInfo(IPEndPoint localEp, IPEndPoint remoteEP, ProtocolType type)
        {
            return new ProxyInfo
            {
                SourceIpV4 = GetIpV4(localEp),
                SourcePort = (ushort)localEp.Port,
                DestIpV4 = GetIpV4(remoteEP),
                DestPort = (ushort)remoteEP.Port,
                Protocol = type
            };
        }

        public void RequestConnection(IPEndPoint localEp, IPEndPoint remoteEp, ProtocolType type)
        {
            ProxyConnectRequest proxyConnectRequest = default(ProxyConnectRequest);
            proxyConnectRequest.Info = MakeInfo(localEp, remoteEp, type);
            ProxyConnectRequest request = proxyConnectRequest;
            _parent.SendAsync(_protocol.Encode(PacketId.ProxyConnect, request));
        }

        public void SignalConnected(IPEndPoint localEp, IPEndPoint remoteEp, ProtocolType type)
        {
            ProxyConnectResponse proxyConnectResponse = default(ProxyConnectResponse);
            proxyConnectResponse.Info = MakeInfo(localEp, remoteEp, type);
            ProxyConnectResponse request = proxyConnectResponse;
            _parent.SendAsync(_protocol.Encode(PacketId.ProxyConnectReply, request));
        }

        public void EndConnection(IPEndPoint localEp, IPEndPoint remoteEp, ProtocolType type)
        {
            ProxyDisconnectMessage proxyDisconnectMessage = default(ProxyDisconnectMessage);
            proxyDisconnectMessage.Info = MakeInfo(localEp, remoteEp, type);
            proxyDisconnectMessage.DisconnectReason = 0;
            ProxyDisconnectMessage request = proxyDisconnectMessage;
            _parent.SendAsync(_protocol.Encode(PacketId.ProxyDisconnect, request));
        }

        public int SendTo(byte[] buffer, int size, SocketFlags flags, IPEndPoint localEp, IPEndPoint remoteEp, ProtocolType type)
        {
            ProxyDataHeader proxyDataHeader = default(ProxyDataHeader);
            proxyDataHeader.Info = MakeInfo(localEp, remoteEp, type);
            proxyDataHeader.DataLength = (uint)size;
            ProxyDataHeader request = proxyDataHeader;
            if (size != buffer.Length)
            {
                byte[] newData = new byte[size];
                Array.Copy(buffer, newData, size);
                buffer = newData;
            }
            _parent.SendAsync(_protocol.Encode(PacketId.ProxyData, request, buffer));
            return size;
        }

        public bool IsBroadcast(uint ip)
        {
            return ip == _broadcast;
        }

        public bool IsMyself(uint ip)
        {
            return ip == _localIp;
        }

        public void Dispose()
        {
            UnregisterHandlers(_protocol);
            lock (_sockets)
            {
                foreach (LdnProxySocket socket in _sockets)
                {
                    socket.ProxyDestroyed();
                }
            }
        }
    }
}
