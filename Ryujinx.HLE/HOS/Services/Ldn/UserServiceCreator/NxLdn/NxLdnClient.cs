using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnRyu;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnRyu.Types;
using SharpPcap.LibPcap;
using System;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn
{
    internal class NxLdnClient : INetworkClient, IDisposable
    {
        public IUserLocalCommunicationService _commService;
        private HLEConfiguration _config;
        public ProxyConfig Config { get; }
        public bool NeedsRealId => true;

        private BaseAdapterHandler _adapterHandler;

        public event EventHandler<NetworkChangeEventArgs> NetworkChange;

        // TODO: Remove debug stuff
        private void LogMsg(string msg)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, msg);
        }

        public NxLdnClient(IUserLocalCommunicationService parent, HLEConfiguration config)
        {
            LogMsg("Init NxLdnClient...");

            EncryptionHelper.Initialize(config.VirtualFileSystem.KeySet);

            _commService = parent;
            _config = config;

            LogMsg($"NxLdnClient MultiplayerLanInterfaceId: {_config.MultiplayerLanInterfaceId}");

            // TODO: What happens when __config.MultiplayerLanInterfaceId == 0 (meaning Default)?
            if (_config.MultiplayerLanInterfaceId == "0")
            {
                _adapterHandler = new DebugAdapterHandler(new CaptureFileReaderDevice("debug-readCap.pcap"));
            }
            else
            {
                // TODO: maybe filter for wifi devices? - eh, probably not (don't want to interfere with other cool projects)
                foreach (LibPcapLiveDevice device in LibPcapLiveDeviceList.Instance)
                {
                    if (device.Name == _config.MultiplayerLanInterfaceId)
                    {
                        _adapterHandler = new AdapterHandler(device, true);
                        break;
                    }
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

            // return _adapterHandler.Connect(request);
            return NetworkError.None;
        }

        public NetworkError ConnectPrivate(ConnectPrivateRequest request)
        {
            LogMsg("NxLdnClient ConnectPrivate");

            return NetworkError.None;
        }

        public bool CreateNetwork(CreateAccessPointRequest request, byte[] advertiseData)
        {
            LogMsg("NxLdnClient CreateNetwork");

            Array384<byte> adData = new();
            advertiseData.CopyTo(adData.AsSpan());

            if (_adapterHandler.CreateNetwork(request, adData, out NetworkInfo networkInfo))
            {
                NetworkChange.Invoke(this, new NetworkChangeEventArgs(networkInfo, true));
                return true;
            }

            return false;
        }

        public bool CreateNetworkPrivate(CreateAccessPointPrivateRequest request, byte[] advertiseData)
        {
            LogMsg("NxLdnClient CreateNetworkPrivate");

            return true;
        }

        public void DisconnectAndStop()
        {
            LogMsg("NxLdnClient DisconnectAndStop");

            if (_adapterHandler != null)
            {
                _adapterHandler.DisconnectAndStop();
            }
        }

        public void DisconnectNetwork()
        {
            // LogMsg("NxLdnClient DisconnectNetwork");

            _adapterHandler.DisconnectNetwork();
        }

        public ResultCode Reject(DisconnectReason disconnectReason, uint nodeId)
        {
            LogMsg("NxLdnClient Reject");

            return ResultCode.Success;
        }

        public NetworkInfo[] Scan(ushort channel, ScanFilter scanFilter)
        {
            // LogMsg("NxLdnClient Scan");

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