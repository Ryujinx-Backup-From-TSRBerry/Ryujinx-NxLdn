using PacketDotNet;
using PacketDotNet.Ieee80211;
using PacketDotNet.Utils;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Capabilities;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Frames;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnRyu;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnRyu.Types;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn
{
    /*
     * This implementation is based on: https://github.com/kinnay/LDN
     * This is only possible because of that project!
     * They really need to be credited for their incredible work!
     */

    class NewNxLdnClient : INetworkClient, IDisposable
    {
        public ProxyConfig Config { get; private set; }
        public bool NeedsRealId => true;

        public event EventHandler<NetworkChangeEventArgs> NetworkChange;

        private ushort[] _wifiChannels = { 1, 6, 11 };

        private LibPcapLiveDevice _wifiAdapter;

        private byte[] _gameVersion = new byte[0x10];
        private Dictionary<int, NetworkInfo> _scannedNetworks = new Dictionary<int, NetworkInfo>();

        public NewNxLdnClient(IUserLocalCommunicationService parent, HLEConfiguration config)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "Initialize NxLdnClient...");

            EncryptionHelper.Initialize(config.VirtualFileSystem.KeySet);

            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "Searching for WiFi Adapter...");

            foreach (LibPcapLiveDevice device in LibPcapLiveDeviceList.Instance)
            {
                if (device.Name == config.MultiplayerLanInterfaceId || device.Name.Contains(config.MultiplayerLanInterfaceId))
                {
                    Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"WiFi Adapter found: {device.Name} {device.Interface.Name}");

                    _wifiAdapter = device;

                    break;
                }
            }

            if (_wifiAdapter == null)
            {
                Logger.Error?.PrintMsg(LogClass.ServiceLdn, $"WiFi Adapter not found!");
            }

            Initialize();

            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient init done.");
        }

        public void Initialize()
        {
            // NOTE: If this wasn't executed in main it will fail here.
            //       But if it was then there is no need for that call (since the caps are already set correctly).
            if (OperatingSystem.IsLinux() && !Capabilities.Capabilities.InheritCapabilities())
            {
                throw new SystemException("Raising capabilities failed");
            }

            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "Openning WiFi Adapter...");

            // NOTE: Crashing here means the device is not ready or "operation not permitted".
            //       - Linux: CAP_NET_RAW,CAP_NET_ADMIN are required.
            //       - Windows: Npcap needs to be configured without admin-only access or Ryujinx needs to be started as Admin.
            _wifiAdapter.Open(new DeviceConfiguration()
            {
                Mode          = DeviceModes.MaxResponsiveness,
                Monitor       = MonitorMode.Active, // TODO: Test without monitor mode
                LinkLayerType = LinkLayers.Ieee80211
            });

            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "WiFi Adapter opened successfully!");

            // Register our handler function to the "packet arrival" event.
            _wifiAdapter.OnPacketArrival += new PacketArrivalEventHandler(OnPacketArrival);

            _wifiAdapter.StartCapture();
        }

        private ushort _currentChannel = 0;

        private void OnPacketArrival(object s, PacketCapture e)
        {
            RawCapture rawPacket = e.GetPacket();
            RadioPacket radioPacket = (RadioPacket)Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);

            if (radioPacket.HasPayloadPacket)
            {
                switch (radioPacket.PayloadPacket)
                {
                    case ActionFrame:
                        NetworkInfo networkInfo = new NewAdvertisementFrame(_currentChannel, radioPacket.PayloadPacket.PayloadData).GenerateNetworkInfo(radioPacket.Extract<ActionFrame>().SourceAddress.GetAddressBytes());

                        int hash = networkInfo.GetHashCode();
                        if (!_scannedNetworks.ContainsKey(hash))
                        {
                            ulong networkInfoSize = (ulong)Marshal.SizeOf(networkInfo);

                            _scannedNetworks[hash] = networkInfo;
                            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "Added NetworkInfo to scanResults.");
                        }

                        break;
                }
            }
        }

        public NetworkError Connect(ConnectRequest request)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient Connect");

            return NetworkError.None;
        }

        public NetworkError ConnectPrivate(ConnectPrivateRequest request)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient ConnectPrivate");

            return NetworkError.None;
        }

        public bool CreateNetwork(CreateAccessPointRequest request, byte[] advertiseData)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient CreateNetwork");

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
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "WiFi Adapter cleaning up...");

            if (_wifiAdapter.Started)
            {
                _wifiAdapter.StopCapture();
            }

            if (_wifiAdapter.Opened)
            {
                _wifiAdapter.Close();
            }
        }

        public void DisconnectNetwork()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient DisconnectNetwork");

            _scannedNetworks.Clear();
        }

        public ResultCode Reject(DisconnectReason disconnectReason, uint nodeId)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient Reject");

            return ResultCode.Success;
        }

        public NetworkInfo[] Scan(ushort channel, ScanFilter scanFilter)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient Scan");

            if (!_wifiChannels.Contains(channel))
            {
                _currentChannel = channel = _wifiChannels[new Random().Next(_wifiChannels.Length)];
            }

            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"Ldn Channel selected: {channel}");

            if (!WifiHelper.SetWifiAdapterChannel(_wifiAdapter.Name, channel))
            {
                Logger.Error?.PrintMsg(LogClass.ServiceLdn, $"Scan Error: Could not set adapter channel: {channel}");

                return Array.Empty<NetworkInfo>();
            }

            // NOTE: Using _adapter.StartCapture() and _adapter.StartCapture() in a small delay doesn't seems to be handled correctly under windows.

            if (_scannedNetworks.Count > 0)
            {
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"Returning Scanned Networks: {_scannedNetworks.Count}");
            }

            return _scannedNetworks.Values.ToArray();
        }

        public void SetAdvertiseData(byte[] data)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient SetAdvertiseData");
        }

        public void SetGameVersion(byte[] versionString)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient SetGameVersion");

            _gameVersion = versionString;

            if (_gameVersion.Length < 0x10)
            {
                Array.Resize(ref _gameVersion, 0x10);
            }
        }

        public void SetStationAcceptPolicy(AcceptPolicy acceptPolicy)
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient SetStationAcceptPolicy");
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

        public void Dispose()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "NxLdnClient Dispose");

            DisconnectAndStop();
        }
    }
}