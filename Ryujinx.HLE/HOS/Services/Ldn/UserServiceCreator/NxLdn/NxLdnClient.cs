using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types;
using SharpPcap;
using SharpPcap.LibPcap;
using System;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn
{
    /*
     * This implementation is based on: https://github.com/kinnay/LDN
     * This is only possible because of that project!
     * They really need to be credited for their incredible work!
     */
    internal class NxLdnClient : INetworkClient, IDisposable
    {
        public IUserLocalCommunicationService _commService;
        private HLEConfiguration _config;
        public ProxyConfig Config { get; }

        private AdapterHandler _adapterHandler;

        public event EventHandler<NetworkChangeEventArgs> NetworkChange;

        // TODO: Remove debug stuff
        private void LogMsg(string msg)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, msg);
        }

        // Linux: Get wifi adapter ready
        // I created a small helper script for that in temp/
        // nmcli device set <device name> managed false
        // nmcli radio wifi off
        // sudo rfkill unblock wifi
        // sudo iw dev <device name> set monitor none
        // sudo ip link set <device name> up
        public NxLdnClient(IUserLocalCommunicationService parent, HLEConfiguration config)
        {
            LogMsg("Init NxLdnClient...");

            _commService = parent;
            _config = config;

            LogMsg($"NxLdnClient MultiplayerLanInterfaceId: {_config.MultiplayerLanInterfaceId}");

            // TODO: What happens when __config.MultiplayerLanInterfaceId == 0 (meaning Default)?
            // TODO: maybe filter for wifi devices? - eh, probably not (don't want to interfere with other cool projects)
            foreach (LibPcapLiveDevice device in LibPcapLiveDeviceList.Instance)
            {
                if (device.Name == _config.MultiplayerLanInterfaceId)
                {
                    _adapterHandler = new AdapterHandler(device, true);
                    break;
                }
            }

            if (_adapterHandler == null)
            {
                throw new Exception("Could not find the adapter.");
            }

            LogMsg("NxLdnClient init done.");
        }

        public NetworkError Connect(ConnectRequest request)
        {
            LogMsg("NxLdnClient Connect");

            return _adapterHandler.Connect(request);
        }

        public NetworkError ConnectPrivate(ConnectPrivateRequest request)
        {
            LogMsg("NxLdnClient ConnectPrivate");

            return NetworkError.None;
        }

        public bool CreateNetwork(CreateAccessPointRequest request, byte[] advertiseData)
        {
            LogMsg("NxLdnClient CreateNetwork");

            return true;
        }

        public bool CreateNetworkPrivate(CreateAccessPointPrivateRequest request, byte[] advertiseData)
        {
            LogMsg("NxLdnClient CreateNetworkPrivate");

            return true;
        }

        public void DisconnectAndStop()
        {
            LogMsg("NxLdnClient DisconnectAndStop");

            if (_adapterHandler != null) {
                _adapterHandler.DisconnectAndStop();
            }
        }

        public void DisconnectNetwork()
        {
            LogMsg("NxLdnClient DisconnectNetwork");

            _adapterHandler.DisconnectNetwork();
        }

        public ResultCode Reject(DisconnectReason disconnectReason, uint nodeId)
        {
            LogMsg("NxLdnClient Reject");

            return ResultCode.Success;
        }

        public NetworkInfo[] Scan(ushort channel, ScanFilter scanFilter)
        {
            LogMsg("NxLdnClient Scan");

            return _adapterHandler.Scan(channel);
        }

        public void SetAdvertiseData(byte[] data)
        {
            LogMsg("NxLdnClient SetAdvertiseData");
            _adapterHandler.SetAdvertiseData(data);
        }

        public void SetGameVersion(byte[] versionString)
        {
            LogMsg("NxLdnClient SetGameVersion");
            _adapterHandler.SetGameVersion(versionString);
        }

        public void SetStationAcceptPolicy(AcceptPolicy acceptPolicy)
        {
            LogMsg("NxLdnClient SetStationAcceptPolicy");
        }

        public void Dispose()
        {
            LogMsg("NxLdnClient Dispose");
        }

        public void HandleCreateNetwork(NetworkInfo info)
        {
            LogMsg("NxLdnClient HandleCreateNetwork");
        }

        public void HandleUpdateNodes(NetworkInfo info)
        {
            LogMsg("NxLdnClient HandleUpdateNodes");
        }

        public void HandleSyncNetwork(NetworkInfo info)
        {
            LogMsg("NxLdnClient HandleSyncNetwork");
        }

        public void HandleConnected(NetworkInfo info)
        {
            LogMsg("NxLdnClient HandleConnected");
        }

        public void HandleDisconnected(NetworkInfo info, DisconnectReason reason)
        {
            LogMsg("NxLdnClient HandleDisconnected");
        }

        public void HandleDisconnectNetwork(NetworkInfo info, DisconnectReason reason)
        {
            LogMsg("NxLdnClient HandleDisconnectNetwork");
        }
    }
}
