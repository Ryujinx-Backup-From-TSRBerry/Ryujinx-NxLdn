using Ryujinx.Common.Utilities;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy
{
    internal class DefaultSocket : ISocket
    {
        private string _lanInterfaceId;

        public Socket BaseSocket { get; }

        public EndPoint RemoteEndPoint => BaseSocket.RemoteEndPoint;

        public EndPoint LocalEndPoint => BaseSocket.LocalEndPoint;

        public bool Connected => BaseSocket.Connected;

        public bool IsBound => BaseSocket.IsBound;

        public AddressFamily AddressFamily => BaseSocket.AddressFamily;

        public SocketType SocketType => BaseSocket.SocketType;

        public ProtocolType ProtocolType => BaseSocket.ProtocolType;

        public bool Blocking
        {
            get
            {
                return BaseSocket.Blocking;
            }
            set
            {
                BaseSocket.Blocking = value;
            }
        }

        public int Available => BaseSocket.Available;

        public DefaultSocket(Socket baseSocket, string lanInterfaceId)
        {
            _lanInterfaceId = lanInterfaceId;
            BaseSocket = baseSocket;
        }

        public DefaultSocket(AddressFamily domain, SocketType type, ProtocolType protocol, string lanInterfaceId)
        {
            _lanInterfaceId = lanInterfaceId;
            BaseSocket = new Socket(domain, type, protocol);
        }

        private void EnsureNetworkInterfaceBound()
        {
            if (_lanInterfaceId != "0" && !BaseSocket.IsBound)
            {
                UnicastIPAddressInformation ipInfo = NetworkHelpers.GetLocalInterface(_lanInterfaceId).Item2;
                BaseSocket.Bind(new IPEndPoint(ipInfo.Address, 0));
            }
        }

        public ISocket Accept()
        {
            return new DefaultSocket(BaseSocket.Accept(), _lanInterfaceId);
        }

        public void Bind(EndPoint localEP)
        {
            BaseSocket.Bind(localEP);
        }

        public void Close()
        {
            BaseSocket.Close();
        }

        public void Connect(EndPoint remoteEP)
        {
            EnsureNetworkInterfaceBound();
            BaseSocket.Connect(remoteEP);
        }

        public void Disconnect(bool reuseSocket)
        {
            BaseSocket.Disconnect(reuseSocket);
        }

        public void GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue)
        {
            BaseSocket.GetSocketOption(optionLevel, optionName, optionValue);
        }

        public void Listen(int backlog)
        {
            BaseSocket.Listen(backlog);
        }

        public int Receive(byte[] buffer)
        {
            EnsureNetworkInterfaceBound();
            return BaseSocket.Receive(buffer);
        }

        public int Receive(byte[] buffer, SocketFlags flags)
        {
            EnsureNetworkInterfaceBound();
            return BaseSocket.Receive(buffer, flags);
        }

        public int ReceiveFrom(byte[] buffer, int size, SocketFlags flags, ref EndPoint remoteEP)
        {
            EnsureNetworkInterfaceBound();
            return BaseSocket.ReceiveFrom(buffer, size, flags, ref remoteEP);
        }

        public int Send(byte[] buffer)
        {
            EnsureNetworkInterfaceBound();
            return BaseSocket.Send(buffer);
        }

        public int Send(byte[] buffer, SocketFlags flags)
        {
            EnsureNetworkInterfaceBound();
            return BaseSocket.Send(buffer, flags);
        }

        public int SendTo(byte[] buffer, int size, SocketFlags flags, EndPoint remoteEP)
        {
            EnsureNetworkInterfaceBound();
            return BaseSocket.SendTo(buffer, size, flags, remoteEP);
        }

        public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue)
        {
            BaseSocket.SetSocketOption(optionLevel, optionName, optionValue);
        }

        public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, object optionValue)
        {
            BaseSocket.SetSocketOption(optionLevel, optionName, optionValue);
        }

        public void Shutdown(SocketShutdown how)
        {
            BaseSocket.Shutdown(how);
        }
    }
}
