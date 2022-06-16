using NetCoreServer;
using Open.Nat;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Proxy
{
    internal class P2pProxyServer : TcpServer, IDisposable
    {
        public const ushort PrivatePortBase = 39990;

        public const int PrivatePortRange = 10;

        private const ushort PublicPortBase = 39990;

        private const int PublicPortRange = 10;

        private const ushort PortLeaseLength = 60;

        private const ushort PortLeaseRenew = 50;

        private const ushort AuthWaitSeconds = 1;

        private ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private ushort _publicPort;

        private bool _disposed;

        private CancellationTokenSource _disposedCancellation = new CancellationTokenSource();

        private NatDevice _natDevice;

        private Mapping _portMapping;

        private ProxyConfig _config;

        private List<P2pProxySession> _players = new List<P2pProxySession>();

        private List<ExternalProxyToken> _waitingTokens = new List<ExternalProxyToken>();

        private AutoResetEvent _tokenEvent = new AutoResetEvent(initialState: false);

        private uint _broadcastAddress;

        private MasterServerClient _master;

        private RyuLdnProtocol _masterProtocol;

        private RyuLdnProtocol _protocol;

        public ushort PrivatePort { get; }

        public P2pProxyServer(MasterServerClient master, ushort port, RyuLdnProtocol masterProtocol)
            : base(IPAddress.Any, port)
        {
            if (ProxyHelpers.SupportsNoDelay())
            {
                base.OptionNoDelay = true;
            }
            PrivatePort = port;
            _master = master;
            _masterProtocol = masterProtocol;
            _masterProtocol.ExternalProxyState += HandleStateChange;
            _masterProtocol.ExternalProxyToken += HandleToken;
            _protocol = new RyuLdnProtocol();
        }

        private void HandleToken(LdnHeader header, ExternalProxyToken token)
        {
            _lock.EnterWriteLock();
            _waitingTokens.Add(token);
            _lock.ExitWriteLock();
            _tokenEvent.Set();
        }

        private void HandleStateChange(LdnHeader header, ExternalProxyConnectionState state)
        {
            if (state.Connected)
            {
                return;
            }
            _lock.EnterWriteLock();
            _waitingTokens.RemoveAll((ExternalProxyToken token) => token.VirtualIp == state.IpAddress);
            _players.RemoveAll(delegate (P2pProxySession player)
            {
                if (player.VirtualIpAddress == state.IpAddress)
                {
                    player.DisconnectAndStop();
                    return true;
                }
                return false;
            });
            _lock.ExitWriteLock();
        }

        public void Configure(ProxyConfig config)
        {
            _config = config;
            _broadcastAddress = config.ProxyIp | ~config.ProxySubnetMask;
        }

        public async Task<ushort> NatPunch()
        {
            NatDiscoverer discoverer = new NatDiscoverer();
            CancellationTokenSource cts = new CancellationTokenSource(1000);
            NatDevice device;
            try
            {
                device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);
            }
            catch (NatDeviceNotFoundException)
            {
                return 0;
            }
            _publicPort = 39990;
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    _portMapping = new Mapping(Protocol.Tcp, PrivatePort, _publicPort, 60, "Ryujinx Local Multiplayer");
                    await device.CreatePortMapAsync(_portMapping);
                }
                catch (MappingException)
                {
                    _publicPort++;
                    goto IL_0163;
                }
                catch (Exception)
                {
                    return 0;
                }
                break;
            IL_0163:
                if (i == 9)
                {
                    _publicPort = 0;
                }
            }
            if (_publicPort != 0)
            {
                _ = Task.Delay(50000, _disposedCancellation.Token).ContinueWith((Task task) => Task.Run(new Func<Task>(RefreshLease)));
            }
            _natDevice = device;
            return _publicPort;
        }

        private void RouteMessage(P2pProxySession sender, ref ProxyInfo info, Action<P2pProxySession> action)
        {
            if (info.SourceIpV4 == 0)
            {
                info.SourceIpV4 = sender.VirtualIpAddress;
            }
            else if (info.SourceIpV4 != sender.VirtualIpAddress)
            {
                return;
            }
            uint destIp = info.DestIpV4;
            if (destIp == 3232235775u)
            {
                destIp = _broadcastAddress;
            }
            bool num = destIp == _broadcastAddress;
            _lock.EnterReadLock();
            if (num)
            {
                _players.ForEach(delegate (P2pProxySession player)
                {
                    action(player);
                });
            }
            else
            {
                P2pProxySession target = _players.FirstOrDefault((P2pProxySession player) => player.VirtualIpAddress == destIp);
                if (target != null)
                {
                    action(target);
                }
            }
            _lock.ExitReadLock();
        }

        public void HandleProxyDisconnect(P2pProxySession sender, LdnHeader header, ProxyDisconnectMessage message)
        {
            RouteMessage(sender, ref message.Info, delegate (P2pProxySession target)
            {
                target.SendAsync(sender.Protocol.Encode(PacketId.ProxyDisconnect, message));
            });
        }

        public void HandleProxyData(P2pProxySession sender, LdnHeader header, ProxyDataHeader message, byte[] data)
        {
            RouteMessage(sender, ref message.Info, delegate (P2pProxySession target)
            {
                target.SendAsync(sender.Protocol.Encode(PacketId.ProxyData, message, data));
            });
        }

        public void HandleProxyConnectReply(P2pProxySession sender, LdnHeader header, ProxyConnectResponse message)
        {
            RouteMessage(sender, ref message.Info, delegate (P2pProxySession target)
            {
                target.SendAsync(sender.Protocol.Encode(PacketId.ProxyConnectReply, message));
            });
        }

        public void HandleProxyConnect(P2pProxySession sender, LdnHeader header, ProxyConnectRequest message)
        {
            RouteMessage(sender, ref message.Info, delegate (P2pProxySession target)
            {
                target.SendAsync(sender.Protocol.Encode(PacketId.ProxyConnect, message));
            });
        }

        private async Task RefreshLease()
        {
            if (!_disposed && _natDevice != null)
            {
                try
                {
                    await _natDevice.CreatePortMapAsync(_portMapping);
                }
                catch (Exception)
                {
                }
                _ = Task.Delay(50, _disposedCancellation.Token).ContinueWith((Task task) => Task.Run(new Func<Task>(RefreshLease)));
            }
        }

        public bool TryRegisterUser(P2pProxySession session, ExternalProxyConfig config)
        {
            _lock.EnterWriteLock();
            IPAddress address = (session.Socket.RemoteEndPoint as IPEndPoint).Address;
            byte[] addressBytes = ProxyHelpers.AddressTo16Byte(address);
            long endTime = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
            long time;
            do
            {
                for (int i = 0; i < _waitingTokens.Count; i++)
                {
                    ExternalProxyToken waitToken = _waitingTokens[i];
                    if ((waitToken.PhysicalIp.SequenceEqual(new byte[16]) || (waitToken.AddressFamily == address.AddressFamily && waitToken.PhysicalIp.SequenceEqual(addressBytes))) && waitToken.Token.SequenceEqual(config.Token))
                    {
                        _waitingTokens.RemoveAt(i);
                        session.SetIpv4(waitToken.VirtualIp);
                        ProxyConfig proxyConfig = default(ProxyConfig);
                        proxyConfig.ProxyIp = session.VirtualIpAddress;
                        proxyConfig.ProxySubnetMask = 4294901760u;
                        ProxyConfig pconfig = proxyConfig;
                        if (_players.Count == 0)
                        {
                            Configure(pconfig);
                        }
                        _players.Add(session);
                        session.SendAsync(_protocol.Encode(PacketId.ProxyConfig, pconfig));
                        _lock.ExitWriteLock();
                        return true;
                    }
                }
                _lock.ExitWriteLock();
                time = Stopwatch.GetTimestamp();
                int remainingMs = (int)((endTime - time) / (Stopwatch.Frequency / 1000));
                if (remainingMs < 0)
                {
                    remainingMs = 0;
                }
                _tokenEvent.WaitOne(remainingMs);
                _lock.EnterWriteLock();
            }
            while (time < endTime);
            _lock.ExitWriteLock();
            return false;
        }

        public void DisconnectProxyClient(P2pProxySession session)
        {
            _lock.EnterWriteLock();
            if (_players.Remove(session))
            {
                _master.SendAsync(_masterProtocol.Encode(PacketId.ExternalProxyState, new ExternalProxyConnectionState
                {
                    IpAddress = session.VirtualIpAddress,
                    Connected = false
                }));
            }
            _lock.ExitWriteLock();
        }

        public new void Dispose()
        {
            base.Dispose();
            _disposed = true;
            _disposedCancellation.Cancel();
            try
            {
                (_natDevice?.DeletePortMapAsync(new Mapping(Protocol.Tcp, PrivatePort, _publicPort, 60, "Ryujinx Local Multiplayer")))?.ContinueWith(delegate
                {
                });
            }
            catch (Exception)
            {
            }
        }

        protected override TcpSession CreateSession()
        {
            return new P2pProxySession(this);
        }

        protected override void OnError(SocketError error)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"Proxy TCP server caught an error with code {error}");
        }
    }
}
