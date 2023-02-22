using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.Types;
using SharpPcap.LibPcap;
using System;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn
{
    internal class NxLdnClient : INetworkClient, IDisposable
    {
        public IUserLocalCommunicationService _parent;
        private HLEConfiguration _config;

        private BaseAdapterHandler _adapterHandler;

        public ProxyConfig Config { get; private set; }

        public bool NeedsRealId => true;

        public event EventHandler<NetworkChangeEventArgs> NetworkChange;

        public NxLdnClient(IUserLocalCommunicationService parent, HLEConfiguration config)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "Init NxLdnClient...");

            EncryptionHelper.Initialize(config.VirtualFileSystem.KeySet);

            _parent = parent;
            _config = config;

            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"NxLdnClient MultiplayerLanInterfaceId: {_config.MultiplayerLanInterfaceId}");

            // TODO: What happens when __config.MultiplayerLanInterfaceId == 0 (meaning Default)?
            if (_config.MultiplayerLanInterfaceId == "0")
            {
                _adapterHandler = new DebugAdapterHandler(new CaptureFileReaderDevice("debug-readCap.pcap"));
            }
            else
            {
                foreach (LibPcapLiveDevice device in LibPcapLiveDeviceList.Instance)
                {
                    Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"NxLdnClient Looping through adapters: {device.Name} {device.Interface.Name}");

                    if (device.Name == _config.MultiplayerLanInterfaceId || device.Name.Contains(_config.MultiplayerLanInterfaceId))
                    {
                        Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"NxLdnClient Found matching adapter: {device.Name} {device.Interface.Name}");

                        _adapterHandler = new AdapterHandler(device, true);

                        break;
                    }
                }
            }

            if (_adapterHandler == null)
            {
                throw new Exception("Could not find the adapter.");
            }

            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient init done.");
        }

        public NetworkError Connect(ConnectRequest request)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient Connect");

            return _adapterHandler.Connect(request);
        }

        public NetworkError ConnectPrivate(ConnectPrivateRequest request)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient ConnectPrivate");

            return NetworkError.None;
        }

        public bool CreateNetwork(CreateAccessPointRequest request, byte[] advertiseData)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient CreateNetwork");

            if (_adapterHandler.CreateNetwork(request, out NetworkInfo networkInfo))
            {
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"NxLdnClient Network created: \n{JsonHelper.Serialize<object>(networkInfo, true)}");

                Config = new ProxyConfig
                {
                    ProxyIp         = networkInfo.Ldn.Nodes[0].Ipv4Address,
                    ProxySubnetMask = NetworkHelpers.ConvertIpv4Address("255.255.255.0")
                };

                NetworkChange?.Invoke(this, new NetworkChangeEventArgs(networkInfo, true));

                return true;
            }

            return false;
        }

        public bool CreateNetworkPrivate(CreateAccessPointPrivateRequest request, byte[] advertiseData)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient CreateNetworkPrivate");

            return true;
        }

        public void DisconnectAndStop()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient DisconnectAndStop");

            if (_adapterHandler != null)
            {
                _adapterHandler.DisconnectAndStop();
            }
        }

        public void DisconnectNetwork()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient DisconnectNetwork");

            _adapterHandler.DisconnectNetwork();
        }

        public ResultCode Reject(DisconnectReason disconnectReason, uint nodeId)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient Reject");

            return ResultCode.Success;
        }

        public NetworkInfo[] Scan(ushort channel, ScanFilter scanFilter)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient Scan");

            return _adapterHandler.Scan(channel);
        }

        public void SetAdvertiseData(byte[] data)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient SetAdvertiseData");

            if (_adapterHandler.SetAdvertiseData(data, out NetworkInfo networkInfo))
            {
                NetworkChange?.Invoke(this, new NetworkChangeEventArgs(networkInfo, true));
            }
        }

        public void SetGameVersion(byte[] versionString)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient SetGameVersion");

            _adapterHandler.SetGameVersion(versionString);
        }

        public void SetStationAcceptPolicy(AcceptPolicy acceptPolicy)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient SetStationAcceptPolicy");
        }

        public void Dispose()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient Dispose");

            _adapterHandler.Dispose();
        }

        public void HandleUpdateNodes(NetworkInfo info)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient HandleUpdateNodes");
        }

        public void HandleSyncNetwork(NetworkInfo info)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient HandleSyncNetwork");
        }

        public void HandleConnected(NetworkInfo info)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient HandleConnected");
        }

        public void HandleDisconnected(NetworkInfo info, DisconnectReason reason)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient HandleDisconnected");
        }

        public void HandleDisconnectNetwork(NetworkInfo info, DisconnectReason reason)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient HandleDisconnectNetwork");
        }
    }
}