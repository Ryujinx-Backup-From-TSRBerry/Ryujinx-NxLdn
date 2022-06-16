using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Proxy;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy;
using Ryujinx.HLE.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn
{
    internal class MasterServerClient : NetCoreServer.TcpClient, INetworkClient, IDisposable, IProxyClient
    {
        private static InitializeMessage InitializeMemory = new InitializeMessage
        {
            Id = new byte[16],
            MacAddress = new byte[6]
        };

        private const int InactiveTimeout = 6000;

        private const int FailureTimeout = 4000;

        private const int ScanTimeout = 1000;

        private bool _useP2pProxy;

        private NetworkError _lastError;

        private ManualResetEvent _connected = new ManualResetEvent(initialState: false);

        private ManualResetEvent _error = new ManualResetEvent(initialState: false);

        private ManualResetEvent _scan = new ManualResetEvent(initialState: false);

        private ManualResetEvent _reject = new ManualResetEvent(initialState: false);

        private AutoResetEvent _apConnected = new AutoResetEvent(initialState: false);

        private RyuLdnProtocol _protocol;

        private NetworkTimeout _timeout;

        private List<NetworkInfo> _availableGames = new List<NetworkInfo>();

        private DisconnectReason _disconnectReason;

        private P2pProxyServer _hostedProxy;

        private P2pProxyClient _connectedProxy;

        private bool _networkConnected;

        private string _passphrase;

        private byte[] _gameVersion = new byte[16];

        private HLEConfiguration _config;

        public ProxyConfig Config { get; private set; }

        public event EventHandler<NetworkChangeEventArgs> NetworkChange;

        public MasterServerClient(string address, int port, HLEConfiguration config)
            : base(address, port)
        {
            if (ProxyHelpers.SupportsNoDelay())
            {
                base.OptionNoDelay = true;
            }
            _protocol = new RyuLdnProtocol();
            _timeout = new NetworkTimeout(6000, TimeoutConnection);
            _protocol.Initialize += HandleInitialize;
            _protocol.Connected += HandleConnected;
            _protocol.Reject += HandleReject;
            _protocol.RejectReply += HandleRejectReply;
            _protocol.SyncNetwork += HandleSyncNetwork;
            _protocol.ProxyConfig += HandleProxyConfig;
            _protocol.Disconnected += HandleDisconnected;
            _protocol.ScanReply += HandleScanReply;
            _protocol.ScanReplyEnd += HandleScanReplyEnd;
            _protocol.ExternalProxy += HandleExternalProxy;
            _protocol.Ping += HandlePing;
            _protocol.NetworkError += HandleNetworkError;
            _config = config;
            _useP2pProxy = !config.MultiplayerDisableP2p;
        }

        private void TimeoutConnection()
        {
            _connected.Reset();
            DisconnectAsync();
            while (base.IsConnected)
            {
                Thread.Yield();
            }
        }

        private bool EnsureConnected()
        {
            if (base.IsConnected)
            {
                return true;
            }
            _error.Reset();
            ConnectAsync();
            int num = WaitHandle.WaitAny(new WaitHandle[2] { _connected, _error }, 4000);
            if (base.IsConnected)
            {
                SendAsync(_protocol.Encode(PacketId.Initialize, InitializeMemory));
            }
            if (num == 0)
            {
                return base.IsConnected;
            }
            return false;
        }

        private void UpdatePassphraseIfNeeded()
        {
            string passphrase = _config.MultiplayerLdnPassphrase ?? "";
            if (passphrase != _passphrase)
            {
                _passphrase = passphrase;
                SendAsync(_protocol.Encode(PacketId.Passphrase, StringUtils.GetFixedLengthBytes(passphrase, 128, Encoding.UTF8)));
            }
        }

        protected override void OnConnected()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"LDN TCP client connected a new session with Id {base.Id}");
            UpdatePassphraseIfNeeded();
            _connected.Set();
        }

        protected override void OnDisconnected()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"LDN TCP client disconnected a session with Id {base.Id}");
            _passphrase = null;
            _connected.Reset();
            if (_networkConnected)
            {
                DisconnectInternal();
            }
        }

        public void DisconnectAndStop()
        {
            _timeout.Dispose();
            DisconnectAsync();
            while (base.IsConnected)
            {
                Thread.Yield();
            }
            Dispose();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            _protocol.Read(buffer, (int)offset, (int)size);
        }

        protected override void OnError(SocketError error)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"LDN TCP client caught an error with code {error}");
            _error.Set();
        }

        private void HandleInitialize(LdnHeader header, InitializeMessage initialize)
        {
            InitializeMemory = initialize;
        }

        private void HandleExternalProxy(LdnHeader header, ExternalProxyConfig config)
        {
            int length = config.AddressFamily switch
            {
                AddressFamily.InterNetwork => 4,
                AddressFamily.InterNetworkV6 => 16,
                _ => 0,
            };
            if (length != 0 && !(_connectedProxy = new P2pProxyClient(new IPAddress(config.ProxyIp.Take(length).ToArray()).ToString(), config.ProxyPort)).PerformAuth(config))
            {
                DisconnectInternal();
            }
        }

        private void HandlePing(LdnHeader header, PingMessage ping)
        {
            if (ping.Requester == 0)
            {
                SendAsync(_protocol.Encode(PacketId.Ping, ping));
            }
        }

        private void HandleNetworkError(LdnHeader header, NetworkErrorMessage error)
        {
            if (error.Error == NetworkError.PortUnreachable)
            {
                _useP2pProxy = false;
            }
            else
            {
                _lastError = error.Error;
            }
        }

        private NetworkError ConsumeNetworkError()
        {
            NetworkError lastError = _lastError;
            _lastError = NetworkError.None;
            return lastError;
        }

        private void HandleSyncNetwork(LdnHeader header, NetworkInfo info)
        {
            this.NetworkChange?.Invoke(this, new NetworkChangeEventArgs(info, connected: true));
        }

        private void HandleConnected(LdnHeader header, NetworkInfo info)
        {
            _networkConnected = true;
            _disconnectReason = DisconnectReason.None;
            _apConnected.Set();
            this.NetworkChange?.Invoke(this, new NetworkChangeEventArgs(info, connected: true));
        }

        private void HandleDisconnected(LdnHeader header, DisconnectMessage message)
        {
            DisconnectInternal();
        }

        private void HandleReject(LdnHeader header, RejectRequest reject)
        {
            _disconnectReason = reject.DisconnectReason;
        }

        private void HandleRejectReply(LdnHeader header)
        {
            _reject.Set();
        }

        private void HandleScanReply(LdnHeader header, NetworkInfo info)
        {
            _availableGames.Add(info);
        }

        private void HandleScanReplyEnd(LdnHeader obj)
        {
            _scan.Set();
        }

        private void DisconnectInternal()
        {
            if (_networkConnected)
            {
                _networkConnected = false;
                _hostedProxy?.Dispose();
                _hostedProxy = null;
                _connectedProxy?.Dispose();
                _connectedProxy = null;
                _apConnected.Reset();
                this.NetworkChange?.Invoke(this, new NetworkChangeEventArgs(default(NetworkInfo), connected: false, _disconnectReason));
                if (base.IsConnected)
                {
                    _timeout.RefreshTimeout();
                }
            }
        }

        public void DisconnectNetwork()
        {
            if (_networkConnected)
            {
                SendAsync(_protocol.Encode(PacketId.Disconnect, default(DisconnectMessage)));
                DisconnectInternal();
            }
        }

        public ResultCode Reject(DisconnectReason disconnectReason, uint nodeId)
        {
            if (_networkConnected)
            {
                _reject.Reset();
                SendAsync(_protocol.Encode(PacketId.Reject, new RejectRequest(disconnectReason, nodeId)));
                if (WaitHandle.WaitAny(new WaitHandle[2] { _reject, _error }, 6000) == 0)
                {
                    if (ConsumeNetworkError() == NetworkError.None)
                    {
                        return ResultCode.Success;
                    }
                    return ResultCode.InvalidState;
                }
            }
            return ResultCode.InvalidState;
        }

        public void SetAdvertiseData(byte[] data)
        {
            if (_networkConnected)
            {
                SendAsync(_protocol.Encode(PacketId.SetAdvertiseData, data));
            }
        }

        public void SetGameVersion(byte[] versionString)
        {
            _gameVersion = versionString;
            if (_gameVersion.Length < 16)
            {
                Array.Resize(ref _gameVersion, 16);
            }
        }

        public void SetStationAcceptPolicy(AcceptPolicy acceptPolicy)
        {
            if (_networkConnected)
            {
                SendAsync(_protocol.Encode(PacketId.SetAcceptPolicy, new SetAcceptPolicyRequest
                {
                    StationAcceptPolicy = acceptPolicy
                }));
            }
        }

        private void DisposeProxy()
        {
            _hostedProxy?.Dispose();
            _hostedProxy = null;
        }

        private void ConfigureAccessPoint(ref RyuNetworkConfig request)
        {
            request.GameVersion = _gameVersion;
            if (!_useP2pProxy)
            {
                return;
            }
            int i;
            for (i = 0; i < 10; i++)
            {
                _hostedProxy = new P2pProxyServer(this, (ushort)(39990 + i), _protocol);
                try
                {
                    _hostedProxy.Start();
                }
                catch (SocketException ex)
                {
                    _hostedProxy.Dispose();
                    _hostedProxy = null;
                    if (ex.SocketErrorCode != SocketError.AddressAlreadyInUse)
                    {
                        i = 10;
                    }
                    continue;
                }
                break;
            }
            if (i < 10)
            {
                Task<ushort> natPunchResult = _hostedProxy.NatPunch();
                try
                {
                    if (natPunchResult.Result != 0)
                    {
                        request.ExternalProxyPort = natPunchResult.Result;
                    }
                }
                catch (Exception)
                {
                }
                if (request.ExternalProxyPort == 0)
                {
                    Logger.Warning?.Print(LogClass.ServiceLdn, "Failed to open a port with UPnP for P2P connection. Proxying through the master server instead. Expect higher latency.", "ConfigureAccessPoint");
                    _hostedProxy.Dispose();
                    return;
                }
                Logger.Info?.Print(LogClass.ServiceLdn, $"Created a wireless P2P network on port {request.ExternalProxyPort}.", "ConfigureAccessPoint");
                _hostedProxy.Start();
                UnicastIPAddressInformation unicastAddress = NetworkHelpers.GetLocalInterface().Item2;
                request.PrivateIp = ProxyHelpers.AddressTo16Byte(unicastAddress.Address);
                request.InternalProxyPort = _hostedProxy.PrivatePort;
                request.AddressFamily = unicastAddress.Address.AddressFamily;
            }
            else
            {
                Logger.Warning?.Print(LogClass.ServiceLdn, "Cannot create a P2P server. Proxying through the master server instead. Expect higher latency.", "ConfigureAccessPoint");
            }
        }

        private bool CreateNetworkCommon()
        {
            bool num = _apConnected.WaitOne(4000);
            if (!_useP2pProxy && _hostedProxy != null)
            {
                Logger.Warning?.Print(LogClass.ServiceLdn, "Locally hosted proxy server was not externally reachable. Proxying through the master server instead. Expect higher latency.", "CreateNetworkCommon");
                DisposeProxy();
            }
            if (num && _connectedProxy != null)
            {
                _connectedProxy.EnsureProxyReady();
                Config = _connectedProxy.ProxyConfig;
                return num;
            }
            DisposeProxy();
            return num;
        }

        public bool CreateNetwork(CreateAccessPointRequest request, byte[] advertiseData)
        {
            _timeout.DisableTimeout();
            ConfigureAccessPoint(ref request.RyuNetworkConfig);
            if (!EnsureConnected())
            {
                DisposeProxy();
                return false;
            }
            UpdatePassphraseIfNeeded();
            SendAsync(_protocol.Encode(PacketId.CreateAccessPoint, request, advertiseData));
            return CreateNetworkCommon();
        }

        public bool CreateNetworkPrivate(CreateAccessPointPrivateRequest request, byte[] advertiseData)
        {
            _timeout.DisableTimeout();
            ConfigureAccessPoint(ref request.RyuNetworkConfig);
            if (!EnsureConnected())
            {
                DisposeProxy();
                return false;
            }
            UpdatePassphraseIfNeeded();
            SendAsync(_protocol.Encode(PacketId.CreateAccessPointPrivate, request, advertiseData));
            return CreateNetworkCommon();
        }

        public NetworkInfo[] Scan(ushort channel, ScanFilter scanFilter)
        {
            if (!_networkConnected)
            {
                _timeout.RefreshTimeout();
            }
            _availableGames.Clear();
            int index = -1;
            if (EnsureConnected())
            {
                UpdatePassphraseIfNeeded();
                _scan.Reset();
                SendAsync(_protocol.Encode(PacketId.Scan, scanFilter));
                index = WaitHandle.WaitAny(new WaitHandle[2] { _scan, _error }, 1000);
            }
            if (index != 0)
            {
                return Array.Empty<NetworkInfo>();
            }
            return _availableGames.ToArray();
        }

        private NetworkError ConnectCommon()
        {
            bool signalled = _apConnected.WaitOne(4000);
            NetworkError error = ConsumeNetworkError();
            if (error != 0)
            {
                return error;
            }
            if (signalled && _connectedProxy != null)
            {
                _connectedProxy.EnsureProxyReady();
                Config = _connectedProxy.ProxyConfig;
            }
            if (!signalled)
            {
                return NetworkError.ConnectTimeout;
            }
            return NetworkError.None;
        }

        public NetworkError Connect(ConnectRequest request)
        {
            _timeout.DisableTimeout();
            if (!EnsureConnected())
            {
                return NetworkError.Unknown;
            }
            SendAsync(_protocol.Encode(PacketId.Connect, request));
            return ConnectCommon();
        }

        public NetworkError ConnectPrivate(ConnectPrivateRequest request)
        {
            _timeout.DisableTimeout();
            if (!EnsureConnected())
            {
                return NetworkError.Unknown;
            }
            SendAsync(_protocol.Encode(PacketId.ConnectPrivate, request));
            return ConnectCommon();
        }

        private void HandleProxyConfig(LdnHeader header, ProxyConfig config)
        {
            Config = config;
            SocketHelpers.RegisterProxy(new LdnProxy(config, this, _protocol));
        }
    }
}
