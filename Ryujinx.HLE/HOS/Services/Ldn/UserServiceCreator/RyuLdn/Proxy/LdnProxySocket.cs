using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Proxy
{
    internal class LdnProxySocket : Sockets.Bsd.Proxy.ISocket
    {
        private LdnProxy _proxy;

        private bool _isListening;

        private List<LdnProxySocket> _listenSockets = new List<LdnProxySocket>();

        private Queue<ProxyConnectRequest> _connectRequests = new Queue<ProxyConnectRequest>();

        private AutoResetEvent _acceptEvent = new AutoResetEvent(initialState: false);

        private int _acceptTimeout = -1;

        private Queue<int> _errors = new Queue<int>();

        private AutoResetEvent _connectEvent = new AutoResetEvent(initialState: false);

        private ProxyConnectResponse _connectResponse;

        private int _receiveTimeout = -1;

        private AutoResetEvent _receiveEvent = new AutoResetEvent(initialState: false);

        private Queue<ProxyDataPacket> _receiveQueue = new Queue<ProxyDataPacket>();

        private int _sendTimeout = -1;

        private bool _connecting;

        private bool _broadcast;

        private bool _readShutdown;

        private bool _writeShutdown;

        private bool _closed;

        private Dictionary<SocketOptionName, int> _socketOptions = new Dictionary<SocketOptionName, int>
        {
            {
                SocketOptionName.Broadcast,
                0
            },
            {
                SocketOptionName.DontLinger,
                0
            },
            {
                SocketOptionName.Debug,
                0
            },
            {
                SocketOptionName.Error,
                0
            },
            {
                SocketOptionName.KeepAlive,
                0
            },
            {
                SocketOptionName.OutOfBandInline,
                0
            },
            {
                SocketOptionName.ReceiveBuffer,
                131072
            },
            {
                SocketOptionName.ReceiveTimeout,
                -1
            },
            {
                SocketOptionName.SendBuffer,
                131072
            },
            {
                SocketOptionName.SendTimeout,
                -1
            },
            {
                SocketOptionName.Type,
                0
            },
            {
                SocketOptionName.ReuseAddress,
                0
            }
        };

        public EndPoint RemoteEndPoint { get; private set; }

        public EndPoint LocalEndPoint { get; private set; }

        public bool Connected { get; private set; }

        public bool IsBound { get; private set; }

        public AddressFamily AddressFamily { get; }

        public SocketType SocketType { get; }

        public ProtocolType ProtocolType { get; }

        public bool Blocking { get; set; }

        public int Available
        {
            get
            {
                int result = 0;
                lock (_receiveQueue)
                {
                    foreach (ProxyDataPacket data in _receiveQueue)
                    {
                        result += data.Data.Length;
                    }
                    return result;
                }
            }
        }

        public bool Readable
        {
            get
            {
                if (_isListening)
                {
                    lock (_connectRequests)
                    {
                        return _connectRequests.Count > 0;
                    }
                }
                if (_readShutdown)
                {
                    return true;
                }
                lock (_receiveQueue)
                {
                    return _receiveQueue.Count > 0;
                }
            }
        }

        public bool Writable
        {
            get
            {
                if (!Connected)
                {
                    return ProtocolType == ProtocolType.Udp;
                }
                return true;
            }
        }

        public bool Error => false;

        public LdnProxySocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, LdnProxy proxy)
        {
            AddressFamily = addressFamily;
            SocketType = socketType;
            ProtocolType = protocolType;
            _proxy = proxy;
            _socketOptions[SocketOptionName.Type] = (int)socketType;
            proxy.RegisterSocket(this);
        }

        private IPEndPoint EnsureLocalEndpoint(bool replace)
        {
            if (LocalEndPoint != null)
            {
                if (!replace)
                {
                    return (IPEndPoint)LocalEndPoint;
                }
                _proxy.ReturnEphemeralPort(ProtocolType, (ushort)((IPEndPoint)LocalEndPoint).Port);
            }
            return (IPEndPoint)(LocalEndPoint = new IPEndPoint(_proxy.LocalAddress, _proxy.GetEphemeralPort(ProtocolType)));
        }

        public LdnProxySocket AsAccepted(IPEndPoint remoteEp)
        {
            Connected = true;
            RemoteEndPoint = remoteEp;
            IPEndPoint localEp = EnsureLocalEndpoint(replace: true);
            _proxy.SignalConnected(localEp, remoteEp, ProtocolType);
            return this;
        }

        private void SignalError(WsaError error)
        {
            lock (_errors)
            {
                _errors.Enqueue((int)error);
            }
        }

        private IPEndPoint GetEndpoint(uint ipv4, ushort port)
        {
            byte[] bytes = BitConverter.GetBytes(ipv4);
            Array.Reverse(bytes);
            return new IPEndPoint(new IPAddress(bytes), port);
        }

        public void IncomingData(ProxyDataPacket packet)
        {
            bool isBroadcast = _proxy.IsBroadcast(packet.Header.Info.DestIpV4);
            if (!_closed && (_broadcast || !isBroadcast))
            {
                lock (_receiveQueue)
                {
                    _receiveQueue.Enqueue(packet);
                }
            }
        }

        public Sockets.Bsd.Proxy.ISocket Accept()
        {
            if (!_isListening)
            {
                throw new InvalidOperationException();
            }
            lock (_connectRequests)
            {
                if (!Blocking && _connectRequests.Count == 0)
                {
                    throw new SocketException(10035);
                }
            }
            while (true)
            {
                _acceptEvent.WaitOne(_acceptTimeout);
                lock (_connectRequests)
                {
                    while (_connectRequests.Count > 0)
                    {
                        ProxyConnectRequest request = _connectRequests.Dequeue();
                        if (_connectRequests.Count > 0)
                        {
                            _acceptEvent.Set();
                        }
                        if (object.Equals(GetEndpoint(request.Info.DestIpV4, request.Info.DestPort), LocalEndPoint))
                        {
                            IPEndPoint remoteEndpoint = GetEndpoint(request.Info.SourceIpV4, request.Info.SourcePort);
                            LdnProxySocket socket = new LdnProxySocket(AddressFamily, SocketType, ProtocolType, _proxy).AsAccepted(remoteEndpoint);
                            lock (_listenSockets)
                            {
                                _listenSockets.Add(socket);
                            }
                            return socket;
                        }
                    }
                }
            }
        }

        public void Bind(EndPoint localEP)
        {
            if (localEP == null)
            {
                throw new ArgumentNullException();
            }
            if (LocalEndPoint != null)
            {
                _proxy.ReturnEphemeralPort(ProtocolType, (ushort)((IPEndPoint)LocalEndPoint).Port);
            }
            LocalEndPoint = (IPEndPoint)localEP;
            IsBound = true;
        }

        public void Close()
        {
            _closed = true;
            _proxy.UnregisterSocket(this);
            if (Connected)
            {
                Disconnect(reuseSocket: false);
            }
            lock (_listenSockets)
            {
                foreach (LdnProxySocket listenSocket in _listenSockets)
                {
                    listenSocket.Close();
                }
            }
            _isListening = false;
        }

        public void Connect(EndPoint remoteEP)
        {
            if (_isListening || !IsBound)
            {
                throw new InvalidOperationException();
            }
            if (!(remoteEP is IPEndPoint))
            {
                throw new NotSupportedException();
            }
            IPEndPoint localEp = EnsureLocalEndpoint(replace: true);
            _connecting = true;
            _proxy.RequestConnection(localEp, (IPEndPoint)remoteEP, ProtocolType);
            if (!Blocking && ProtocolType == ProtocolType.Tcp)
            {
                throw new SocketException(10035);
            }
            _connectEvent.WaitOne();
            if (_connectResponse.Info.SourceIpV4 == 0)
            {
                throw new SocketException(10061);
            }
            _connectResponse = default(ProxyConnectResponse);
        }

        public void HandleConnectResponse(ProxyConnectResponse obj)
        {
            if (_connecting)
            {
                _connecting = false;
                if (_connectResponse.Info.SourceIpV4 != 0)
                {
                    IPEndPoint remoteEp = (IPEndPoint)(RemoteEndPoint = GetEndpoint(obj.Info.SourceIpV4, obj.Info.SourcePort));
                    Connected = true;
                }
                else
                {
                    SignalError(WsaError.WSAECONNREFUSED);
                }
            }
        }

        public void Disconnect(bool reuseSocket)
        {
            if (Connected)
            {
                ConnectionEnded();
                _proxy.EndConnection(LocalEndPoint as IPEndPoint, RemoteEndPoint as IPEndPoint, ProtocolType);
            }
        }

        private void ConnectionEnded()
        {
            if (Connected)
            {
                RemoteEndPoint = null;
                Connected = false;
            }
        }

        public void GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue)
        {
            if (optionLevel != SocketOptionLevel.Socket)
            {
                throw new NotImplementedException();
            }
            if (_socketOptions.TryGetValue(optionName, out var result))
            {
                byte[] data = BitConverter.GetBytes(result);
                Array.Copy(data, 0, optionValue, 0, Math.Min(data.Length, optionValue.Length));
                return;
            }
            throw new NotImplementedException();
        }

        public void Listen(int backlog)
        {
            if (!IsBound)
            {
                throw new SocketException();
            }
            _isListening = true;
        }

        public void HandleConnectRequest(ProxyConnectRequest obj)
        {
            lock (_connectRequests)
            {
                _connectRequests.Enqueue(obj);
            }
            _connectEvent.Set();
        }

        public void HandleDisconnect(ProxyDisconnectMessage message)
        {
            Disconnect(reuseSocket: false);
        }

        public int Receive(byte[] buffer)
        {
            EndPoint dummy = new IPEndPoint(IPAddress.Any, 0);
            return ReceiveFrom(buffer, buffer.Length, SocketFlags.None, ref dummy);
        }

        public int Receive(byte[] buffer, SocketFlags flags)
        {
            EndPoint dummy = new IPEndPoint(IPAddress.Any, 0);
            return ReceiveFrom(buffer, buffer.Length, flags, ref dummy);
        }

        public int ReceiveFrom(byte[] buffer, int size, SocketFlags flags, ref EndPoint remoteEp)
        {
            if (!Connected && ProtocolType == ProtocolType.Tcp)
            {
                throw new SocketException(10054);
            }
            lock (_receiveQueue)
            {
                if (_receiveQueue.Count > 0)
                {
                    return ReceiveFromQueue(buffer, size, flags, ref remoteEp);
                }
                if (_readShutdown)
                {
                    return 0;
                }
                if (!Blocking)
                {
                    throw new SocketException(10035);
                }
            }
            int timeout = _receiveTimeout;
            _receiveEvent.WaitOne((timeout == 0) ? (-1) : timeout);
            if (!Connected && ProtocolType == ProtocolType.Tcp)
            {
                throw new SocketException(10054);
            }
            lock (_receiveQueue)
            {
                if (_receiveQueue.Count > 0)
                {
                    return ReceiveFromQueue(buffer, size, flags, ref remoteEp);
                }
                if (_readShutdown)
                {
                    return 0;
                }
                throw new SocketException(10060);
            }
        }

        private int ReceiveFromQueue(byte[] buffer, int size, SocketFlags flags, ref EndPoint remoteEp)
        {
            ProxyDataPacket packet = _receiveQueue.Peek();
            remoteEp = GetEndpoint(packet.Header.Info.SourceIpV4, packet.Header.Info.SourcePort);
            bool peek = (flags & SocketFlags.Peek) != 0;
            int read;
            if (packet.Data.Length > size)
            {
                read = size;
                Array.Copy(packet.Data, buffer, size);
                if (ProtocolType == ProtocolType.Udp)
                {
                    if (!peek)
                    {
                        _receiveQueue.Dequeue();
                    }
                    throw new SocketException(10040);
                }
                if (ProtocolType == ProtocolType.Tcp)
                {
                    byte[] newData = new byte[packet.Data.Length - size];
                    Array.Copy(packet.Data, size, newData, 0, newData.Length);
                    packet.Data = newData;
                }
            }
            else
            {
                read = packet.Data.Length;
                Array.Copy(packet.Data, buffer, packet.Data.Length);
                if (!peek)
                {
                    _receiveQueue.Dequeue();
                }
            }
            return read;
        }

        public int Send(byte[] buffer)
        {
            if (!Connected)
            {
                throw new SocketException();
            }
            return SendTo(buffer, buffer.Length, SocketFlags.None, RemoteEndPoint);
        }

        public int Send(byte[] buffer, SocketFlags flags)
        {
            if (!Connected)
            {
                throw new SocketException();
            }
            return SendTo(buffer, buffer.Length, flags, RemoteEndPoint);
        }

        public int SendTo(byte[] buffer, int size, SocketFlags flags, EndPoint remoteEP)
        {
            if (!Connected && ProtocolType == ProtocolType.Tcp)
            {
                throw new SocketException(10054);
            }
            IPEndPoint localEp = EnsureLocalEndpoint(replace: false);
            if (!(remoteEP is IPEndPoint))
            {
                throw new NotSupportedException();
            }
            return _proxy.SendTo(buffer, size, flags, localEp, (IPEndPoint)remoteEP, ProtocolType);
        }

        public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue)
        {
            if (optionLevel != SocketOptionLevel.Socket)
            {
                throw new NotImplementedException();
            }
            switch (optionName)
            {
                case SocketOptionName.SendTimeout:
                    _sendTimeout = optionValue;
                    break;
                case SocketOptionName.ReceiveTimeout:
                    _receiveTimeout = optionValue;
                    break;
                case SocketOptionName.Broadcast:
                    _broadcast = optionValue != 0;
                    break;
            }
            lock (_socketOptions)
            {
                _socketOptions[optionName] = optionValue;
            }
        }

        public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, object optionValue)
        {
        }

        public void Shutdown(SocketShutdown how)
        {
            switch (how)
            {
                case SocketShutdown.Both:
                    _readShutdown = true;
                    _writeShutdown = true;
                    break;
                case SocketShutdown.Receive:
                    _readShutdown = true;
                    break;
                case SocketShutdown.Send:
                    _writeShutdown = true;
                    break;
            }
        }

        public void ProxyDestroyed()
        {
        }
    }
}
