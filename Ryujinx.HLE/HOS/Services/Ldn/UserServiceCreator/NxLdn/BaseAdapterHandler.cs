using PacketDotNet;
using PacketDotNet.Ieee80211;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnRyu.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.Types;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn
{
    abstract class BaseAdapterHandler : IDisposable
    {
        internal ushort[]      _channels = { 1, 6, 11 };
        protected readonly int _scanDwellTime = 110;

        internal ushort        _sequenceNumber = 0;
        internal bool          _storeCapture = false;
        internal bool          _debugMode = false;
        internal Random        _random = new Random();
        internal ushort        _currentChannel;
        internal NetworkInfo   _networkInfo;
        internal byte[]        _gameVersion;

        internal CaptureFileWriterDevice _captureFileWriterDevice;

        internal List<NetworkInfo> _scanResults = new List<NetworkInfo>();

        internal ushort GetRandomChannel() {
            return _channels[_random.Next(_channels.Length)];
        }

        private static NetworkInfo GetEmptyNetworkInfo()
        {
            NetworkInfo networkInfo = new NetworkInfo
            {
                NetworkId = {
                    IntentId = default,
                    SessionId = new Array16<byte>()
                },
                Common = {
                    MacAddress = new Array6<byte>(),
                    Ssid = {
                        Length = 0,
                        Name = new Array33<byte>()
                    }
                },
                Ldn = {
                    NodeCountMax = 8,
                    SecurityParameter = new Array16<byte>(),
                    Nodes = new Array8<NodeInfo>(),
                    AdvertiseData = new Array384<byte>(),
                    Unknown2 = new Array140<byte>()
                }
            };
            networkInfo.Common.Ssid.Name.AsSpan().Fill(0);
            networkInfo.NetworkId.SessionId.AsSpan().Fill(0);
            networkInfo.Ldn.SecurityParameter.AsSpan().Fill(0);
            for (int i = 0; i < 8; i++)
            {
                networkInfo.Ldn.Nodes[i] = new NodeInfo()
                {
                    MacAddress = new Array6<byte>(),
                    UserName = new Array33<byte>(),
                    Reserved2 = new Array16<byte>()
                };
                networkInfo.Ldn.Nodes[i].UserName.AsSpan().Fill(0);
                networkInfo.Ldn.Nodes[i].Reserved2.AsSpan().Fill(0);
            }
            networkInfo.Ldn.AdvertiseData.AsSpan().Fill(0);
            networkInfo.Ldn.Unknown2.AsSpan().Fill(0);

            return networkInfo;
        }

        protected static bool BuildNetworkInfo(ushort channel, ActionFrame action, AdvertisementFrame advertisement, out NetworkInfo networkInfo)
        {
            Array33<byte> sessionId = new();
            advertisement.Header.SessionId.AsSpan().CopyTo(sessionId.AsSpan());

            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"Header length: {Marshal.SizeOf(advertisement.Header)} / {Marshal.SizeOf<NetworkId>()} | Info length: {Marshal.SizeOf(advertisement.Info)} / {Marshal.SizeOf<LdnNetworkInfo>()}");
            networkInfo = GetEmptyNetworkInfo();
            networkInfo.NetworkId = advertisement.Header;
            networkInfo.Common.Ssid.Length = (byte)advertisement.Header.SessionId.Length;
            networkInfo.Common.Ssid.Name = sessionId;
            networkInfo.Common.Channel = channel;
            networkInfo.Common.LinkLevel = 3;
            networkInfo.Common.NetworkType = 2;
            networkInfo.Common.Reserved = 0;
            networkInfo.Ldn = advertisement.Info.ToLdnNetworkInfo();

            action.SourceAddress.GetAddressBytes().CopyTo(networkInfo.Common.MacAddress.AsSpan());

            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"NetworkInfo length: {Marshal.SizeOf(networkInfo)} / {Marshal.SizeOf<NetworkInfo>()}");

            // Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"Built NetworkInfo: \n{JsonHelper.Serialize<object>(networkInfo, true)}");
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"Node 0 - Username: {Encoding.UTF8.GetString(networkInfo.Ldn.Nodes[0].UserName.AsSpan())}");

            // https://gchq.github.io/CyberChef/#recipe=From_Base64('A-Za-z0-9%2B/%3D',true)Swap_endianness('Raw',8,true)To_Hex('None',0)
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"LocalCommunicationId: {networkInfo.NetworkId.IntentId.LocalCommunicationId:x16}");
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"SceneId: {networkInfo.NetworkId.IntentId.SceneId}");
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"AppVersion: {networkInfo.Ldn.Nodes[0].LocalCommunicationVersion}");

            return Marshal.SizeOf(networkInfo) == Marshal.SizeOf<NetworkInfo>();
        }

        protected void OnPacketArrival(object s, PacketCapture e)
        {
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
            {
                _currentChannel = (ushort)((ChannelRadioTapField)packet.Extract<RadioPacket>()[RadioTapType.Channel]).Channel;
            }

            // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L710
            // RadioPacket HasPayloadPacket -> ActionFrame
            if (packet.HasPayloadPacket)
            {
                if (_storeCapture)
                {
                    _captureFileWriterDevice.Write(rawPacket);
                    // LogMsg($"OnScanPacketArrival: Raw packet dumped to file.");
                }

                // LogMsg($"OnScanPacketArrival: Got Packet: {packet.ToString(StringOutputType.VerboseColored)}");
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"OnScanPacketArrival: RadioPacket: Header length: {packet.HeaderData.Length} / Payload length: {packet.PayloadPacket.TotalPacketLength}");
                // LogMsg($"OnScanPacketArrival: RadioPacket: {string.Join(" ", packet.PayloadPacket.HeaderData.Select(x => x.ToString("X2")))}");
                // LogMsg($"OnScanPacketArrival: RadioPacket: {packet.PrintHex()}");
                // LogMsg($"OnScanPacketArrival: Action Frame? {packet.HasPayloadPacket}");
                // ActionFrame
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"OnScanPacketArrival: RadioPacket PayloadPacket type: {packet.PayloadPacket.GetType()}");
                // This does not work - I have no idea why
                // {packet.PayloadPacket.ToString(StringOutputType.VerboseColored)}

                switch (packet.PayloadPacket)
                {
                    case ActionFrame:
                        ActionFrame action = packet.Extract<ActionFrame>();
                        Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"OnScanPacketArrival: Got ActionFrame from: {action.SourceAddress}");
                        // ActionFrame HasPayloadData -> Action(?)
                        // LogMsg($"OnScanPacketArrival: Action Frame: [{action.TotalPacketLength}] {action.ToString(StringOutputType.VerboseColored)}");
                        // LogMsg($"Action Payload: {action.HasPayloadPacket} / Action Data: {action.HasPayloadData}");
                        // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L719
                        if (AdvertisementFrame.TryGetAdvertisementFrame(action, out AdvertisementFrame adFrame))
                        {
                            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"ActionPayloadData matches LDN header!");
                            // LogMsg("AdvertisementFrame: ", adFrame);

                            if (BuildNetworkInfo(_currentChannel, action, adFrame, out NetworkInfo networkInfo))
                            {
                                // Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"Got networkInfo: \n{JsonHelper.Serialize<object>(networkInfo, true)}");
                                if (!_scanResults.Contains(networkInfo))
                                {
                                    _scanResults.Add(networkInfo);
                                    Logger.Info?.PrintMsg(LogClass.ServiceLdn, "Added NetworkInfo to scanResults.");
                                }
                            }
                            else
                            {
                                Logger.Info?.PrintMsg(LogClass.ServiceLdn, "Invalid NetworkInfo packet skipped.");
                            }
                        }
                        break;
                    case AuthenticationFrame:
                        AuthenticationFrame authFrame = packet.Extract<AuthenticationFrame>();
                        if (NxAuthenticationFrame.TryGetNxAuthenticationFrame(authFrame, out NxAuthenticationFrame nxAuthFrame))
                        {
                            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "OnPacketArrival: Authentication packet header matches!");
                            // Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"OnPacketArrival: AuthenticationFrame: \n{JsonHelper.Serialize<object>(nxAuthFrame, true)}");
                        }
                        break;
                }
            }
        }

        /*
        * Handles everything related to the WiFi adapter
        */
        public BaseAdapterHandler(bool storeCapture = false, bool debugMode = false)
        {
            AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) => Dispose();
            AppDomain.CurrentDomain.ProcessExit += (object sender, EventArgs e) => Dispose();
            _storeCapture = storeCapture;
            _debugMode = debugMode;

            // FIXME: Temp workaround
            _networkInfo = GetEmptyNetworkInfo();
            _scanResults = new List<NetworkInfo>();
        }

        public abstract bool CreateNetwork(CreateAccessPointRequest request, out NetworkInfo networkInfo);

        public virtual void SetGameVersion(byte[] versionString)
        {
            _gameVersion = versionString;
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"GameVersion: {Encoding.UTF8.GetString(versionString)}");
            if (_gameVersion.Length < 16)
            {
                Array.Resize(ref _gameVersion, 16);
            }
        }

        protected NxAuthenticationFrame BuildAuthenticationRequest(ConnectRequest request)
        {
            byte[] authKey = new byte[0x10];
            _random.NextBytes(authKey);
            AuthenticationRequest authRequest = new AuthenticationRequest()
            {
                AppVersion = BitConverter.ToUInt16(_gameVersion)
            };
            request.UserConfig.UserName.AsSpan().CopyTo(authRequest.UserName.AsSpan());

            ChallengeRequestParameter challenge = new ChallengeRequest()
            {
                Token = request.NetworkInfo.Ldn.AuthenticationId,
                Nonce = (ulong)_random.NextInt64(0x100000000), // FIXME: This should probably be done in another way
                DeviceId = (ulong)_random.NextInt64(0x100000000) // FIXME: This should probably be done in another way
            }.Encode();
            NxAuthenticationFrame authFrame = new NxAuthenticationFrame()
            {
                Version = 3, // FIXME: usually this will be 3 (with encryption), but there needs to be a way to check this
                StatusCode = AuthenticationStatusCode.Success,
                IsResponse = false,
                Header = request.NetworkInfo.NetworkId,
                AuthenticationKey = authKey, // FIXME: Secure RNG?
                Size = (ushort)(Marshal.SizeOf(authRequest) + NxAuthenticationFields.PayloadRequestChallengePadding + Marshal.SizeOf(challenge)),
                Payload = authRequest,
                ChallengeRequest = challenge
            };

            request.NetworkInfo.NetworkId.SessionId.AsSpan().CopyTo(authFrame.NetworkKey);

            return authFrame;
        }

        public abstract NetworkError Connect(ConnectRequest request);

        public abstract NetworkInfo[] Scan(ushort channel);

        public bool SetAdvertiseData(byte[] data, out NetworkInfo networkInfo)
        {
            data.CopyTo(_networkInfo.Ldn.AdvertiseData.AsSpan());
            _networkInfo.Ldn.AdvertiseDataSize = (ushort)data.Length;

            networkInfo = _networkInfo;

            return _networkInfo.Ldn.Nodes[0].IsConnected == 1;
        }

        public abstract void DisconnectAndStop();

        public abstract void DisconnectNetwork();

        public virtual void Dispose()
        {
            DisconnectAndStop();
            if (_captureFileWriterDevice.Opened)
                _captureFileWriterDevice.Close();
            _captureFileWriterDevice.Dispose();
        }
    }
}