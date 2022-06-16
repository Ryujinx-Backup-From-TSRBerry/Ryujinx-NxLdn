using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types;
using System;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn
{
    internal class DisabledLdnClient : INetworkClient, IDisposable
    {
        public ProxyConfig Config { get; }

        public event EventHandler<NetworkChangeEventArgs> NetworkChange;

        public NetworkError Connect(ConnectRequest request)
        {
            this.NetworkChange?.Invoke(this, new NetworkChangeEventArgs(default(NetworkInfo), connected: false));
            return NetworkError.None;
        }

        public NetworkError ConnectPrivate(ConnectPrivateRequest request)
        {
            this.NetworkChange?.Invoke(this, new NetworkChangeEventArgs(default(NetworkInfo), connected: false));
            return NetworkError.None;
        }

        public bool CreateNetwork(CreateAccessPointRequest request, byte[] advertiseData)
        {
            this.NetworkChange?.Invoke(this, new NetworkChangeEventArgs(default(NetworkInfo), connected: false));
            return true;
        }

        public bool CreateNetworkPrivate(CreateAccessPointPrivateRequest request, byte[] advertiseData)
        {
            this.NetworkChange?.Invoke(this, new NetworkChangeEventArgs(default(NetworkInfo), connected: false));
            return true;
        }

        public void DisconnectAndStop()
        {
        }

        public void DisconnectNetwork()
        {
        }

        public ResultCode Reject(DisconnectReason disconnectReason, uint nodeId)
        {
            return ResultCode.Success;
        }

        public NetworkInfo[] Scan(ushort channel, ScanFilter scanFilter)
        {
            return Array.Empty<NetworkInfo>();
        }

        public void SetAdvertiseData(byte[] data)
        {
        }

        public void SetGameVersion(byte[] versionString)
        {
        }

        public void SetStationAcceptPolicy(AcceptPolicy acceptPolicy)
        {
        }

        public void Dispose()
        {
        }
    }
}
