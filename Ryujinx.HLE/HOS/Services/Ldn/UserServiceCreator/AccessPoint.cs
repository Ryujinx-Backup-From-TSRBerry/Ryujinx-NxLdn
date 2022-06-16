using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types;
using System;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator
{
    internal class AccessPoint : IDisposable
    {
        private byte[] _advertiseData;

        private IUserLocalCommunicationService _parent;

        public NetworkInfo NetworkInfo;

        public NodeLatestUpdate[] LatestUpdates = new NodeLatestUpdate[8];

        public bool Connected { get; private set; }

        public ProxyConfig Config => _parent.NetworkClient.Config;

        public AccessPoint(IUserLocalCommunicationService parent)
        {
            _parent = parent;
            _parent.NetworkClient.NetworkChange += NetworkChanged;
        }

        public void Dispose()
        {
            _parent.NetworkClient.DisconnectNetwork();
            _parent.NetworkClient.NetworkChange -= NetworkChanged;
        }

        private void NetworkChanged(object sender, NetworkChangeEventArgs e)
        {
            LatestUpdates.CalculateLatestUpdate(NetworkInfo.Ldn.Nodes, e.Info.Ldn.Nodes);
            NetworkInfo = e.Info;
            if (Connected != e.Connected)
            {
                Connected = e.Connected;
                if (Connected)
                {
                    _parent.SetState(NetworkState.AccessPointCreated);
                }
                else
                {
                    _parent.SetDisconnectReason(e.DisconnectReasonOrDefault(DisconnectReason.DestroyedBySystem));
                }
            }
            else
            {
                _parent.SetState();
            }
        }

        public ResultCode SetAdvertiseData(byte[] advertiseData)
        {
            _advertiseData = advertiseData;
            _parent.NetworkClient.SetAdvertiseData(_advertiseData);
            return ResultCode.Success;
        }

        public ResultCode SetStationAcceptPolicy(AcceptPolicy acceptPolicy)
        {
            _parent.NetworkClient.SetStationAcceptPolicy(acceptPolicy);
            return ResultCode.Success;
        }

        public ResultCode CreateNetwork(SecurityConfig securityConfig, UserConfig userConfig, NetworkConfig networkConfig)
        {
            CreateAccessPointRequest createAccessPointRequest = default(CreateAccessPointRequest);
            createAccessPointRequest.SecurityConfig = securityConfig;
            createAccessPointRequest.UserConfig = userConfig;
            createAccessPointRequest.NetworkConfig = networkConfig;
            CreateAccessPointRequest request = createAccessPointRequest;
            if (!_parent.NetworkClient.CreateNetwork(request, _advertiseData ?? new byte[0]))
            {
                return ResultCode.InvalidState;
            }
            return ResultCode.Success;
        }

        public ResultCode CreateNetworkPrivate(SecurityConfig securityConfig, SecurityParameter securityParameter, UserConfig userConfig, NetworkConfig networkConfig, AddressList addressList)
        {
            CreateAccessPointPrivateRequest createAccessPointPrivateRequest = default(CreateAccessPointPrivateRequest);
            createAccessPointPrivateRequest.SecurityConfig = securityConfig;
            createAccessPointPrivateRequest.SecurityParameter = securityParameter;
            createAccessPointPrivateRequest.UserConfig = userConfig;
            createAccessPointPrivateRequest.NetworkConfig = networkConfig;
            createAccessPointPrivateRequest.AddressList = addressList;
            CreateAccessPointPrivateRequest request = createAccessPointPrivateRequest;
            if (!_parent.NetworkClient.CreateNetworkPrivate(request, _advertiseData))
            {
                return ResultCode.InvalidState;
            }
            return ResultCode.Success;
        }
    }
}
