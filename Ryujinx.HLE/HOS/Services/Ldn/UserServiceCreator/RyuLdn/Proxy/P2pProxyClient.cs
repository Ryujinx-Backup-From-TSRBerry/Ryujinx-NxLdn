using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy;
using System.Net.Sockets;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Proxy
{
    internal class P2pProxyClient : NetCoreServer.TcpClient, IProxyClient
    {
        private const int FailureTimeout = 4000;

        private RyuLdnProtocol _protocol;

        private ManualResetEvent _connected = new ManualResetEvent(initialState: false);

        private ManualResetEvent _ready = new ManualResetEvent(initialState: false);

        private AutoResetEvent _error = new AutoResetEvent(initialState: false);

        public ProxyConfig ProxyConfig { get; private set; }

        public P2pProxyClient(string address, int port)
            : base(address, port)
        {
            if (ProxyHelpers.SupportsNoDelay())
            {
                base.OptionNoDelay = true;
            }
            _protocol = new RyuLdnProtocol();
            _protocol.ProxyConfig += HandleProxyConfig;
            ConnectAsync();
        }

        protected override void OnConnected()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"Proxy TCP client connected a new session with Id {base.Id}");
            _connected.Set();
        }

        protected override void OnDisconnected()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"Proxy TCP client disconnected a session with Id {base.Id}");
            SocketHelpers.UnregisterProxy();
            _connected.Reset();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            _protocol.Read(buffer, (int)offset, (int)size);
        }

        protected override void OnError(SocketError error)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"Proxy TCP client caught an error with code {error}");
            _error.Set();
        }

        private void HandleProxyConfig(LdnHeader header, ProxyConfig config)
        {
            ProxyConfig = config;
            SocketHelpers.RegisterProxy(new LdnProxy(config, this, _protocol));
            _ready.Set();
        }

        public bool EnsureProxyReady()
        {
            return _ready.WaitOne(4000);
        }

        public bool PerformAuth(ExternalProxyConfig config)
        {
            if (!_connected.WaitOne(4000))
            {
                return false;
            }
            SendAsync(_protocol.Encode(PacketId.ExternalProxy, config));
            return true;
        }
    }
}
