using PacketDotNet.Ieee80211;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnRyu.Types;
using SharpPcap;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Network
{
    internal abstract class BaseAccessPoint
    {
        protected static readonly PhysicalAddress broadcastAddr = new PhysicalAddress(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff });
        private BaseAdapterHandler _parent;
        protected PacketArrivalEventHandler _eventHandler;
        private Thread t;

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

        protected virtual void OnPacketArrival(object s, PacketCapture e)
        {

        }

        public virtual void BuildNewNetworkInfo(CreateAccessPointRequest request, Array384<byte> advertiseData)
        {
            Array33<byte> sessionId = new();
            _parent._random.NextBytes(sessionId.AsSpan()[..16]);
            int networkId = _parent._random.Next(1, 128);
            _parent._networkInfo = new NetworkInfo()
            {
                NetworkId = {
                    IntentId = request.NetworkConfig.IntentId
                },
                Common = {
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
                    Nodes = new Array8<NodeInfo>(),
                    Reserved1 = 0,
                    Reserved2 = 0,
                    SecurityMode = ((ushort)request.SecurityConfig.SecurityMode),
                    StationAcceptPolicy = 0,
                    Unknown1 = 0,
                    Unknown2 = new Array140<byte>(),
                }
            };

            sessionId.AsSpan()[..16].CopyTo(_parent._networkInfo.NetworkId.SessionId.AsSpan());
            _parent._networkInfo.Ldn.Nodes[0] = new NodeInfo()
            {
                Reserved1 = 0,
                Ipv4Address = NetworkHelpers.ConvertIpv4Address(IPAddress.Parse($"169.254.{networkId}.1")),
                IsConnected = 1,
                LocalCommunicationVersion = request.NetworkConfig.LocalCommunicationVersion,
                NodeId = 1,
                Reserved2 = new Array16<byte>(),
                UserName = request.UserConfig.UserName
            };

            request.SecurityConfig.Passphrase.AsSpan()[..16].CopyTo(_parent._networkInfo.Ldn.SecurityParameter.AsSpan());
        }

        protected virtual RadioPacket GetAdvertisementFrame()
        {
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

            return radioPacket;
        }

        protected abstract void SpamActionFrame();

        public BaseAccessPoint(BaseAdapterHandler handler)
        {
            _parent = handler;
            _eventHandler = new PacketArrivalEventHandler(OnPacketArrival);
            t = new Thread(new ThreadStart(SpamActionFrame));
        }

        public bool Start()
        {
            t.Start();
            return true;
        }
    }
}