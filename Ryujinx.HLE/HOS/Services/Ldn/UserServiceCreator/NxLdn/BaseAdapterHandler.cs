using PacketDotNet;
using PacketDotNet.Ieee80211;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnRyu.Types;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn
{
    abstract class BaseAdapterHandler : IDisposable
    {
        internal bool _storeCapture = false;
        internal bool _debugMode = false;
        internal Random _random = new Random();

        internal CaptureFileWriterDevice _captureFileWriterDevice;

        protected ushort[] channels = { 1, 6, 11 };
        protected ushort currentChannel = 0;

        internal NetworkInfo _networkInfo;
        protected byte[] _gameVersion;

        protected List<NetworkInfo> _scanResults = new List<NetworkInfo>();
        protected readonly int _scanDwellTime = 110;

        // TODO: Remove debug stuff
        protected static void LogMsg(string msg, object obj = null)
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

        // NOTE: This should be part of: https://github.com/dotpcap/packetnet/blob/c4ba374674eeb1e7a7fd58ebcfe0b933505599f2/PacketDotNet/Ieee80211/RadioTapFields.cs
        // Maybe I should PR this?
        // public static ushort ChannelToFrequencyMHz(ushort channel)
        // {
        //     // NOTE: These will only include the channels the switch will use
        //     switch (channel)
        //     {
        //         case 1:
        //             return 2412;
        //         case 6:
        //             return 2437;
        //         case 11:
        //             return 2462;
        //         default:
        //             return 0;
        //     }
        // }

        protected static bool BuildNetworkInfo(ushort channel, ActionFrame action, AdvertisementFrame advertisement, out NetworkInfo networkInfo)
        {
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
            // TODO: Remove debug stuff
            string username = System.Text.Encoding.UTF8.GetString(networkInfo.Ldn.Nodes[0].UserName.AsSpan());
            LogMsg($"Node 0 - Username: {username}");

            // https://gchq.github.io/CyberChef/#recipe=From_Base64('A-Za-z0-9%2B/%3D',true)Swap_endianness('Raw',8,true)To_Hex('None',0)
            LogMsg($"LocalCommunicationId: ", BitConverter.GetBytes(networkInfo.NetworkId.IntentId.LocalCommunicationId));
            LogMsg($"SceneId: {networkInfo.NetworkId.IntentId.SceneId}");
            LogMsg($"AppVersion: {networkInfo.Ldn.Nodes[0].LocalCommunicationVersion}");

            return (Marshal.SizeOf(networkInfo) == Marshal.SizeOf<NetworkInfo>());
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
                currentChannel = (ushort)((ChannelRadioTapField)packet.Extract<RadioPacket>()[RadioTapType.Channel]).Channel;

            // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L710
            // RadioPacket HasPayloadPacket -> ActionFrame
            if (packet.HasPayloadPacket && packet.PayloadPacket is ActionFrame)
            {
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
                LogMsg($"OnScanPacketArrival: Got RadioPacket from: {action.SourceAddress.ToString()}");
                // ActionFrame HasPayloadData -> Action(?)
                // LogMsg($"OnScanPacketArrival: Action Frame: [{action.TotalPacketLength}] {action.ToString(StringOutputType.VerboseColored)}");
                // LogMsg($"Action Payload: {action.HasPayloadPacket} / Action Data: {action.HasPayloadData}");
                // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L719
                if (AdvertisementFrame.TryGetAdvertisementFrame(action, out AdvertisementFrame adFrame))
                {
                    LogMsg($"ActionPayloadData matches LDN header!");
                    // LogMsg("AdvertisementFrame: ", adFrame);

                    if (BuildNetworkInfo(currentChannel, action, adFrame, out NetworkInfo networkInfo))
                    {
                        LogMsg("Got networkInfo: ", networkInfo);
                        if (!_scanResults.Contains(networkInfo))
                        {
                            _scanResults.Add(networkInfo);
                            LogMsg("Added NetworkInfo to scanResults.");
                        }
                    }
                    else
                    {
                        LogMsg("Invalid NetworkInfo packet skipped.");
                    }
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

            _scanResults = new List<NetworkInfo>();
        }

        public abstract bool CreateNetwork(CreateAccessPointRequest request, Array384<byte> advertiseData, ushort advertiseDataLength, out NetworkInfo networkInfo);

        public virtual void SetGameVersion(byte[] versionString)
        {
            _gameVersion = versionString;
            LogMsg("GameVersion: ", versionString);
            if (_gameVersion.Length < 16)
            {
                Array.Resize<byte>(ref _gameVersion, 16);
            }
        }

        // public NetworkError Connect(ConnectRequest request)
        // {
        //     byte[] authKey = new byte[0x10];
        //     _random.NextBytes(authKey);
        //     AuthenticationRequest authRequest = new AuthenticationRequest()
        //     {
        //         UserName = request.UserConfig.UserName,
        //         AppVersion = BitConverter.ToUInt16(_gameVersion)
        //     };
        //     ChallengeRequestParameter challenge = new ChallengeRequest()
        //     {
        //         Token = request.NetworkInfo.Ldn.AuthenticationId,
        //         Nonce = (ulong)_random.NextInt64(0x100000000), // FIXME: This should probably be done in another way
        //         DeviceId = (ulong)_random.NextInt64(0x100000000) // FIXME: This should probably be done in another way
        //     }.Encode();
        //     // TODO: Figure out if this is the right packet type
        //     DataDataFrame data = new DataDataFrame()
        //     {
        //         SourceAddress = _adapter.MacAddress,
        //         DestinationAddress = new PhysicalAddress(request.NetworkInfo.Common.MacAddress),
        //         PayloadData = new AuthenticationFrame()
        //         {
        //             Version = 3, // FIXME: usually this will be 3 (with encryption), but there needs to be a way to check this
        //             StatusCode = AuthenticationStatusCode.Success,
        //             IsResponse = false,
        //             Header = request.NetworkInfo.NetworkId,
        //             NetworkKey = request.NetworkInfo.NetworkId.SessionId,
        //             AuthenticationKey = authKey, // FIXME: Secure RNG?
        //             Size = 64 + 0x300 + 0x24,
        //             Payload = authRequest,
        //             ChallengeRequest = challenge
        //         }.Encode(),
        //     };
        //     _adapter.SendPacket(data);
        //     return NetworkError.None;
        // }

        public abstract NetworkInfo[] Scan(ushort channel);

        public void SetAdvertiseData(byte[] data)
        {
            data.CopyTo(_networkInfo.Ldn.AdvertiseData.AsSpan());
            _networkInfo.Ldn.AdvertiseDataSize = (ushort)data.Length;
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