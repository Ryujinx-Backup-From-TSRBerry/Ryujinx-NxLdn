using PacketDotNet.Ieee80211;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnRyu.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.Types;
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

        public virtual void BuildNewNetworkInfo(CreateAccessPointRequest request)
        {
            Array33<byte> sessionId = new();
            Array33<byte> ssid = new();
            _parent._random.NextBytes(sessionId.AsSpan()[..16]);
            _parent._random.NextBytes(ssid.AsSpan()[..32]);
            int networkId = _parent._random.Next(1, 128);

            _parent._networkInfo.NetworkId = new NetworkId
            {
                IntentId = request.NetworkConfig.IntentId
            };

            sessionId.AsSpan()[..16].CopyTo(_parent._networkInfo.NetworkId.SessionId.AsSpan());

            _parent._networkInfo.Common.Ssid.Length = (byte)32;
            _parent._networkInfo.Common.Ssid.Name = ssid;
            _parent._networkInfo.Common.Channel = request.NetworkConfig.Channel == 0 ? (ushort)1 : request.NetworkConfig.Channel;
            _parent._networkInfo.Common.LinkLevel = 3;
            _parent._networkInfo.Common.NetworkType = 2;
            // _parent._networkInfo.Ldn.AuthenticationId = (ulong)_parent._random.NextInt64();
            _parent._networkInfo.Ldn.NodeCount = 1;
            _parent._networkInfo.Ldn.NodeCountMax = request.NetworkConfig.NodeCountMax;
            _parent._networkInfo.Ldn.Nodes[0].Ipv4Address = NetworkHelpers.ConvertIpv4Address(IPAddress.Parse($"169.254.{networkId}.1"));
            _parent._networkInfo.Ldn.Nodes[0].IsConnected = 1;
            _parent._networkInfo.Ldn.Nodes[0].LocalCommunicationVersion = request.NetworkConfig.LocalCommunicationVersion;
            _parent._networkInfo.Ldn.Nodes[0].UserName = request.UserConfig.UserName;
            _parent._networkInfo.Ldn.SecurityMode = ((ushort)request.SecurityConfig.SecurityMode);

            request.SecurityConfig.Passphrase.AsSpan()[..16].CopyTo(_parent._networkInfo.Ldn.SecurityParameter.AsSpan());

            for (byte i = 0; i < _parent._networkInfo.Ldn.Nodes.Length; i++)
            {
                _parent._networkInfo.Ldn.Nodes[i].NodeId = i;
            }
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