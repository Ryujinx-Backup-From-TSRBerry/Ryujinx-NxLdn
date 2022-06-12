using PacketDotNet.Ieee80211;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Network
{
    internal class AccessPoint
    {
        private static readonly PhysicalAddress broadcastAddr = new PhysicalAddress(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff });
        private AdapterHandler _parent;
        private PacketArrivalEventHandler _eventHandler;
        private Thread t;

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

        private void OnPacketArrival(object s, PacketCapture e)
        {

        }

        public void BuildNewNetworkInfo(CreateAccessPointRequest request, byte[] advertiseData)
        {
            byte[] sessionId = new byte[16];
            Array.Resize(ref advertiseData, 384);
            _parent._random.NextBytes(sessionId);
            Array.Resize(ref sessionId, 33);
            int networkId = _parent._random.Next(1, 128);
            _parent._networkInfo = new NetworkInfo()
            {
                NetworkId = {
                    IntentId = request.NetworkConfig.IntentId,
                    SessionId = sessionId
                },
                Common = {
                    MacAddress = _parent._adapter.MacAddress.GetAddressBytes(),
                    Ssid = {
                        Length = (byte) 16,
                        Name = sessionId
                    },
                    Channel = request.NetworkConfig.Channel == 0 ? (ushort) 1 : request.NetworkConfig.Channel,
                    LinkLevel = 3,
                    NetworkType = 2,
                    Reserved = (uint) 0
                },
                Ldn = {
                    AdvertiseData = advertiseData,
                    AdvertiseDataSize = (ushort) advertiseData.Length,
                    AuthenticationId = 0, // ?
                    NodeCount = 1,
                    NodeCountMax = request.NetworkConfig.NodeCountMax,
                    Nodes = new NodeInfo[8] {
                        new NodeInfo() {
                            Reserved1 = 0,
                            Ipv4Address = NetworkHelpers.ConvertIpv4Address(IPAddress.Parse($"169.254.{networkId}.1")),
                            IsConnected = 1,
                            LocalCommunicationVersion = request.NetworkConfig.LocalCommunicationVersion,
                            MacAddress = _parent._adapter.MacAddress.GetAddressBytes(),
                            NodeId = 1,
                            Reserved2 = new byte[16],
                            UserName = request.UserConfig.UserName
                        },
                        default, default, default, default, default, default, default
                    },
                    Reserved1 = 0,
                    Reserved2 = 0,
                    SecurityMode = ((ushort)request.SecurityConfig.SecurityMode),
                    SecurityParameter = request.SecurityConfig.Passphrase,
                    StationAcceptPolicy = 0,
                    Unknown1 = 0,
                    Unknown2 = new byte[140],
                }
            };
            LogMsg("AP: New NetworkInfo created.");
        }

        private RadioPacket GetAdvertisementFrame()
        {
            byte[] nonce = BitConverter.GetBytes(_parent._random.Next(0x10000000));

            RadioPacket radioPacket = new RadioPacket();
            radioPacket.Add(new TsftRadioTapField((ulong)new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds()));
            radioPacket.Add(new FlagsRadioTapField(RadioTapFlags.FcsIncludedInFrame));
            ChannelRadioTapField channel = new ChannelRadioTapField();
            channel.Channel = _parent._networkInfo.Common.Channel;
            radioPacket.Add(channel);
            radioPacket.Add(new DbmAntennaSignalRadioTapField(-50)); // -50 dBm as a default value for now
            radioPacket.Add(new AntennaRadioTapField(0));
            radioPacket.Add(new RxFlagsRadioTapField());
            // Mcs information Field missing

            ActionFrame action = new ActionFrame(_parent._adapter.MacAddress, broadcastAddr, broadcastAddr);
            AdvertisementFrame advertisement = new AdvertisementFrame()
            {
                Header = _parent._networkInfo.NetworkId,
                Encryption = 2, // can be 1(plain) or 2(AES-CTR) -> https://github.com/kinnay/NintendoClients/wiki/LDN-Protocol#advertisement-payload
                Info = _parent._networkInfo.Ldn,
                Nonce = nonce,
                Version = 3 // can be 2(no auth token) or 3(with auth token) - https://github.com/kinnay/NintendoClients/wiki/LDN-Protocol#advertisement-data
            };
            LogMsg("AP: Created AdvertisementFrame: ", advertisement);
            // advertisement.LogProps();
            action.PayloadData = advertisement.Encode();
            radioPacket.PayloadPacket = action;

            return radioPacket;
        }

        private void SpamActionFrame()
        {
            while (_parent._adapter.Opened)
            {
                RadioPacket radioPacket = GetAdvertisementFrame();
                _parent._adapter.SendPacket(radioPacket);
                if (_parent._storeCapture && _parent._captureFileWriterDevice.Opened)
                {
                    // LogMsg($"AP: Writing packet to file...");
                    _parent._captureFileWriterDevice.SendPacket(radioPacket);
                }
                // https://github.com/kinnay/NintendoClients/wiki/LDN-Protocol#overview
                Thread.Sleep(100);
            }
        }

        public AccessPoint(AdapterHandler handler)
        {
            _parent = handler;
            _eventHandler = new PacketArrivalEventHandler(OnPacketArrival);
            _parent._adapter.OnPacketArrival += _eventHandler;
            t = new Thread(new ThreadStart(SpamActionFrame));
        }

        ~AccessPoint()
        {
            _parent._adapter.OnPacketArrival -= _eventHandler;
        }

        public bool Start()
        {
            t.Start();
            return true;
        }
    }
}
