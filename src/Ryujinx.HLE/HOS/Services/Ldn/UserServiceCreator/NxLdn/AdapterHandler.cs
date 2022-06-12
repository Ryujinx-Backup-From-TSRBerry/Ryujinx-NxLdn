using PacketDotNet;
using PacketDotNet.Ieee80211;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.NxLdn.Capabilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.Types;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using AuthenticationFrame = Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets.AuthenticationFrame;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn
{
    class AdapterHandler: IDisposable {
        internal LibPcapLiveDevice _adapter;
        internal bool _storeCapture = false;
        internal bool _debugMode = false;
        internal Random _random = new Random();

        internal CaptureFileWriterDevice _captureFileWriterDevice;

        private ushort[] channels = {1, 6, 11};
        private ushort currentChannel = 0;

        internal NetworkInfo _networkInfo;
        private byte[] _gameVersion;

        private List<NetworkInfo> _scanResults = new List<NetworkInfo>();
        private int _scanDwellTime = 110;

        private Network.AccessPoint _ap;

        // TODO: Remove debug stuff
        private static void LogMsg(string msg, object obj = null)
        {
            if (obj != null)
            {
                string jsonString = JsonHelper.Serialize<object>(obj, true);
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, msg + "\n" + jsonString);
            }
            else
            {
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, msg);
            }
        }

        private bool SetAdapterChannel(ushort channel) {
            if (_debugMode)
                return true;

            using (Process process = new Process())
            {
                process.StartInfo.CreateNoWindow = true;
                if (OperatingSystem.IsLinux()) {
                    process.StartInfo.FileName = "iw";
                    process.StartInfo.Arguments = $"dev {_adapter.Name} set channel {channel}";
                }
                else if (OperatingSystem.IsWindows()) {
                    process.StartInfo.FileName = $"{Environment.SystemDirectory}\\Npcap\\WlanHelper.exe";
                    process.StartInfo.Arguments = $"\"{_adapter.Name}\" channel {channel}";
                }
                else {
                    throw new NotImplementedException();
                }
                process.StartInfo.RedirectStandardError = true;
                // LogMsg($"AdapterHandler: Setting channel to {channel}...");
                process.Start();
                process.WaitForExit();
                // LogMsg($"AdapterHandler: process exited with code: {process.ExitCode} - Error Output: {process.StandardError.ReadToEnd()}");
                if (process.ExitCode == 0)
                {
                    currentChannel = channel;
                }

                return process.ExitCode == 0;
            }
        }

        private static bool BuildNetworkInfo(ushort channel, ActionFrame action, AdvertisementFrame advertisement, out NetworkInfo networkInfo) {
            Array33<byte> sessionId = new();

            advertisement.Header.SessionId.AsSpan().CopyTo(sessionId.AsSpan());


            LogMsg($"Header length: {Marshal.SizeOf(advertisement.Header)} / {Marshal.SizeOf<NetworkId>()} | Info length: {Marshal.SizeOf(advertisement.Info)} / {Marshal.SizeOf<LdnNetworkInfo>()}");
            networkInfo = new NetworkInfo()
            {
                NetworkId = advertisement.Header,
                Common = {
                    Ssid = {
                        Length = (byte)advertisement.Header.SessionId.Length,
                        Name = sessionId
                    },
                    Channel = channel,
                    LinkLevel = 3,
                    NetworkType = 2,
                    Reserved = (uint) 0
                },
                Ldn = advertisement.Info
            };

            action.SourceAddress.GetAddressBytes().CopyTo(networkInfo.Common.MacAddress.AsSpan());

            LogMsg($"NetworkInfo length: {Marshal.SizeOf(networkInfo)} / {Marshal.SizeOf<NetworkInfo>()}");

            // LogMsg($"Built NetworkInfo: ", networkInfo);

            // https://gchq.github.io/CyberChef/#recipe=From_Base64('A-Za-z0-9%2B/%3D',true)Swap_endianness('Raw',8,true)To_Hex('None',0)
            LogMsg($"LocalCommunicationId: ", BitConverter.GetBytes(networkInfo.NetworkId.IntentId.LocalCommunicationId));
            LogMsg($"SceneId: {networkInfo.NetworkId.IntentId.SceneId}");
            LogMsg($"AppVersion: {networkInfo.Ldn.Nodes[0].LocalCommunicationVersion}");

            return (Marshal.SizeOf(networkInfo) == Marshal.SizeOf<NetworkInfo>());
        }

        private void OnPacketArrival(object s, PacketCapture e) {
            var rawPacket = e.GetPacket();
            // LogMsg($"OnScanPacketArrival: [Len: {e.Data.Length}] {e.GetPacket()}");

            // LogMsg($"OnScanPacketArrival: {e.GetPacket().GetPacket().PrintHex()}");
            // string headerData = string.Join(" ", e.GetPacket().GetPacket().HeaderData.Select(x => string.Format("{0:X2}", x)));
            // LogMsg($"OnScanPacketArrival: {headerData}");
            // if (e.GetPacket().GetPacket().HasPayloadData) {
            //     string payloadData = string.Join(" ", e.GetPacket().GetPacket().PayloadData.Select(x => string.Format("{0:X2}", x)));
            //     LogMsg($"OnScanPacketArrival: {payloadData}");
            // }

            var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);

            if (_debugMode)
                currentChannel = (ushort) ((ChannelRadioTapField)packet.Extract<RadioPacket>()[RadioTapType.Channel]).Channel;

            // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L710
            // RadioPacket HasPayloadPacket -> ActionFrame
            if (packet.HasPayloadPacket && packet.PayloadPacket is ActionFrame) {
                if (_storeCapture)
                {
                    _captureFileWriterDevice.Write(rawPacket);
                    // LogMsg($"OnScanPacketArrival: Raw packet dumped to file.");
                }
                // LogMsg($"OnScanPacketArrival: Got Packet: {packet.ToString(StringOutputType.VerboseColored)}");
                LogMsg($"OnScanPacketArrival: RadioPacket: Header length: {packet.HeaderData.Length} / Payload length: {packet.PayloadPacket.TotalPacketLength}");
                // LogMsg($"OnScanPacketArrival: RadioPacket: {string.Join(" ", packet.PayloadPacket.HeaderData.Select(x => x.ToString("X2")))}");
                // LogMsg($"OnScanPacketArrival: RadioPacket: {packet.PrintHex()}");
                // LogMsg($"OnScanPacketArrival: Action Frame? {packet.HasPayloadPacket}");
                // ActionFrame
                LogMsg($"OnScanPacketArrival: RadioPacket PayloadPacket type: {packet.PayloadPacket.GetType()}");
                // This does not work - I have no idea why
                // {packet.PayloadPacket.ToString(StringOutputType.VerboseColored)}

                // ActionFrame action = (ActionFrame) packet.PayloadPacket;
                ActionFrame action = packet.Extract<ActionFrame>();
                // ActionFrame HasPayloadData -> Action(?)
                // LogMsg($"OnScanPacketArrival: Action Frame: [{action.TotalPacketLength}] {action.ToString(StringOutputType.VerboseColored)}");
                // LogMsg($"Action Payload: {action.HasPayloadPacket} / Action Data: {action.HasPayloadData}");
                // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L719
                if (AdvertisementFrame.TryGetAdvertisementFrame(action, out AdvertisementFrame adFrame)) {
                    LogMsg($"ActionPayloadData matches LDN header!");
                    // LogMsg("AdvertisementFrame: ", adFrame);

                    if (BuildNetworkInfo(currentChannel, action, adFrame, out NetworkInfo networkInfo)) {
                        if (!_scanResults.Contains(networkInfo))
                        {
                            _scanResults.Add(networkInfo);
                            LogMsg("Added NetworkInfo to scanResults.");
                        }
                    }
                    else {
                        LogMsg("Invalid NetworkInfo packet skipped.");
                    }
                }
            }
        }

        /*
        * Handles everything related to the WiFi adapter
        */
        public AdapterHandler(LibPcapLiveDevice device, bool storeCapture = false, bool debug = false) {
            AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) => Dispose();
            AppDomain.CurrentDomain.ProcessExit += (object sender, EventArgs e) => Dispose();
            // ILiveDevice doesn't work with pcap files
            debug = false;
            _adapter = device;
            _debugMode = debug;
            _storeCapture = storeCapture;

            if (!_debugMode) {
                // If this wasn't executed in main it will fail here
                // But if it was then there is no need for that call (since the caps are already set correctly)
                if (OperatingSystem.IsLinux() && !Capabilities.InheritCapabilities())
                {
                    throw new SystemException("Raising capabilities failed");
                }
            }
            else {
                LogMsg("AdapterHandler initializing in debug mode...");
                _storeCapture = false;
            }

            LogMsg("AdapterHandler trying to access the adapter now...");

            // Crashing here means the device is not ready or "operation not permitted"
            // Linux: CAP_NET_RAW,CAP_NET_ADMIN are required
            // Windows: Npcap needs to be configured without admin-only access or Ryujinx needs to be started as Admin
            if (!_debugMode) {
                // Configure and open wifi adapter
                _adapter.Open(new DeviceConfiguration() {
                    Monitor = MonitorMode.Active,
                    // TODO: Test without monitor mode
                    // Mode = DeviceModes.Promiscuous,
                    LinkLayerType = LinkLayers.Ieee80211
                });
            }
            else {
                _adapter.Open();
            }

            LogMsg("AdapterHandler opened the adapter successfully!");

            _scanResults = new List<NetworkInfo>();

            _adapter.OnPacketArrival += new PacketArrivalEventHandler(OnPacketArrival);

            if (_storeCapture) {
                LogMsg("AdapterHandler: Dumping raw packets to file...");
                _captureFileWriterDevice = new CaptureFileWriterDevice("debug-cap.pcap");
                _captureFileWriterDevice.Open(_adapter);
            }
        }

        public bool CreateNetwork(CreateAccessPointRequest request, Array384<byte> advertiseData, out NetworkInfo networkInfo) {
            _ap = new Network.AccessPoint(this);
            _ap.BuildNewNetworkInfo(request, advertiseData);
            networkInfo = _networkInfo;
            return _ap.Start();
        }

        public void SetGameVersion(byte[] versionString) {
            _gameVersion = versionString;
            LogMsg("GameVersion: ", versionString);
            if (_gameVersion.Length < 16) {
                Array.Resize<byte>(ref _gameVersion, 16);
            }
        }

        public NetworkError Connect(ConnectRequest request) {
            byte[] authKey = new byte[0x10];
            _random.NextBytes(authKey);
            AuthenticationRequest authRequest = new AuthenticationRequest() {
                AppVersion = BitConverter.ToUInt16(_gameVersion)
            };
            request.UserConfig.UserName.AsSpan()[..32].CopyTo(authRequest.UserName.AsSpan());
            ChallengeRequestParameter challenge = new ChallengeRequest() {
                Token = request.NetworkInfo.Ldn.AuthenticationId,
                Nonce = (ulong) _random.NextInt64(0x100000000), // FIXME: This should probably be done in another way
                DeviceId = (ulong) _random.NextInt64(0x100000000) // FIXME: This should probably be done in another way
            }.Encode();
            // TODO: Figure out if this is the right packet type
            DataDataFrame data = new DataDataFrame() {
                SourceAddress = _adapter.MacAddress,
                DestinationAddress = new PhysicalAddress(request.NetworkInfo.Common.MacAddress.AsSpan().ToArray()),
                PayloadData = new AuthenticationFrame() {
                    Version = 3, // FIXME: usually this will be 3 (with encryption), but there needs to be a way to check this
                    StatusCode = AuthenticationStatusCode.Success,
                    IsResponse = false,
                    Header = request.NetworkInfo.NetworkId,
                    NetworkKey = request.NetworkInfo.NetworkId.SessionId.AsSpan().ToArray(),
                    AuthenticationKey = authKey, // FIXME: Secure RNG?
                    Size = 64 + 0x300 + 0x24,
                    Payload = authRequest,
                    ChallengeRequest = challenge
                }.Encode(),
            };
            _adapter.SendPacket(data);
            return NetworkError.None;
        }

        public NetworkInfo[] Scan(ushort channel) {
            if (!channels.Contains(channel)) {
                // LogMsg($"Scan Warning: {channel} is not in channel list.");
                if (currentChannel != 0)
                {
                    int index = Array.IndexOf(channels, currentChannel);
                    if (index == channels.Length - 1) {
                        index = 0;
                    }
                    else {
                        index++;
                    }
                    channel = channels[index];
                }
                else {
                    channel = channels[0];
                }
            }
            if (currentChannel != channel) {
                if (!SetAdapterChannel(channel))
                {
                    LogMsg($"Scan Error: Could not set adapter channel: {channel}");
                    return new NetworkInfo[] { };
                }
            }

            _scanResults.Clear();

            _adapter.StartCapture();

            Thread.Sleep(_scanDwellTime);

            return _scanResults.ToArray();
        }

        public void SetAdvertiseData(byte[] data) {
            data.CopyTo(_networkInfo.Ldn.AdvertiseData.AsSpan());
            _networkInfo.Ldn.AdvertiseDataSize = (ushort) data.Length;
        }

        public void DisconnectAndStop() {
            LogMsg("AdapterHandler cleaning up...");
            if (_adapter.Opened)
                _adapter.Close();
        }

        public void DisconnectNetwork() {
            _adapter.StopCapture();
        }

        public void Dispose() {
            DisconnectAndStop();
            _adapter.Dispose();
        }
    }
}