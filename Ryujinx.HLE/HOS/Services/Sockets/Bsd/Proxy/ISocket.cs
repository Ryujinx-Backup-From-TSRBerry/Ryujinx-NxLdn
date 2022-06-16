using System.Net;
using System.Net.Sockets;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy
{
    internal interface ISocket
    {
        EndPoint RemoteEndPoint { get; }

        EndPoint LocalEndPoint { get; }

        bool Connected { get; }

        bool IsBound { get; }

        AddressFamily AddressFamily { get; }

        SocketType SocketType { get; }

        ProtocolType ProtocolType { get; }

        bool Blocking { get; set; }

        int Available { get; }

        int Receive(byte[] buffer);

        int Receive(byte[] buffer, SocketFlags flags);

        int ReceiveFrom(byte[] buffer, int size, SocketFlags flags, ref EndPoint remoteEP);

        int Send(byte[] buffer);

        int Send(byte[] buffer, SocketFlags flags);

        int SendTo(byte[] buffer, int size, SocketFlags flags, EndPoint remoteEP);

        ISocket Accept();

        void Bind(EndPoint localEP);

        void Connect(EndPoint remoteEP);

        void Listen(int backlog);

        void GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue);

        void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue);

        void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, object optionValue);

        void Shutdown(SocketShutdown how);

        void Disconnect(bool reuseSocket);

        void Close();
    }
}
