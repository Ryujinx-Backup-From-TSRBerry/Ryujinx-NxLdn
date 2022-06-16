using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Proxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy
{
    internal static class SocketHelpers
    {
        private static LdnProxy _proxy;

        public static void Select(List<ISocket> readEvents, List<ISocket> writeEvents, List<ISocket> errorEvents, int timeout)
        {
            List<Socket> readDefault = (from x in readEvents
                                        select (x as DefaultSocket)?.BaseSocket into x
                                        where x != null
                                        select x).ToList();
            List<Socket> writeDefault = (from x in writeEvents
                                         select (x as DefaultSocket)?.BaseSocket into x
                                         where x != null
                                         select x).ToList();
            List<Socket> errorDefault = (from x in errorEvents
                                         select (x as DefaultSocket)?.BaseSocket into x
                                         where x != null
                                         select x).ToList();
            Socket.Select(readDefault, writeDefault, errorDefault, timeout);
            FilterSockets(readEvents, readDefault, (LdnProxySocket socket) => socket.Readable);
            FilterSockets(writeEvents, writeDefault, (LdnProxySocket socket) => socket.Writable);
            FilterSockets(errorEvents, errorDefault, (LdnProxySocket socket) => socket.Error);
            static void FilterSockets(List<ISocket> removeFrom, List<Socket> selectedSockets, Func<LdnProxySocket, bool> ldnCheck)
            {
                removeFrom.RemoveAll(delegate (ISocket socket)
                {
                    DefaultSocket defaultSocket = socket as DefaultSocket;
                    if (defaultSocket != null)
                    {
                        return !selectedSockets.Contains(defaultSocket.BaseSocket);
                    }
                    LdnProxySocket ldnProxySocket = socket as LdnProxySocket;
                    if (ldnProxySocket != null)
                    {
                        return !ldnCheck(ldnProxySocket);
                    }
                    throw new NotImplementedException();
                });
            }
        }

        public static void RegisterProxy(LdnProxy proxy)
        {
            if (_proxy != null)
            {
                UnregisterProxy();
            }
            _proxy = proxy;
        }

        public static void UnregisterProxy()
        {
            _proxy?.Dispose();
            _proxy = null;
        }

        public static ISocket CreateSocket(AddressFamily domain, SocketType type, ProtocolType protocol, string lanInterfaceId)
        {
            if (_proxy != null && _proxy.Supported(domain, type, protocol))
            {
                return new LdnProxySocket(domain, type, protocol, _proxy);
            }
            return new DefaultSocket(domain, type, protocol, lanInterfaceId);
        }
    }
}
