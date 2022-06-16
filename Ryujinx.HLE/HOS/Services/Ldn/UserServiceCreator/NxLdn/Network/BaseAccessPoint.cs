using PacketDotNet.Ieee80211;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types;
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

        public virtual void BuildNewNetworkInfo(CreateAccessPointRequest request, byte[] advertiseData)
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
                    AuthenticationId = (ulong) _parent._random.NextInt64(),
                    NodeCount = 1,
                    NodeCountMax = request.NetworkConfig.NodeCountMax,
                    Nodes = new NodeInfo[8] {
                        new NodeInfo() {
                            Reserved1 = 0,
                            Ipv4Address = NetworkHelpers.ConvertIpv4Address(IPAddress.Parse($"169.254.{networkId}.1")),
                            IsConnected = 1,
                            LocalCommunicationVersion = request.NetworkConfig.LocalCommunicationVersion,
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
                    Unknown2 = new byte[140]
                }
            };
        }

        protected abstract RadioPacket GetAdvertisementFrame();

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
