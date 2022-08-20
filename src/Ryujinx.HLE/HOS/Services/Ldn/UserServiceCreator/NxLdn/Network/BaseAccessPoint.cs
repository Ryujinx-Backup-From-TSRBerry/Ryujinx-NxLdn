using PacketDotNet.Ieee80211;
using Ryujinx.Common.Memory;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnRyu.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.Types;
using SharpPcap;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Network
{
    internal abstract class BaseAccessPoint
    {
        protected static readonly SortedList<ushort, ushort> channelMap = new SortedList<ushort, ushort>() {
            {1, 2412},
            {6, 2437},
            {11, 2462},
            {36, 5180},
            {40, 5200},
            {44, 5220},
            {48, 5240}
        };
        protected static readonly PhysicalAddress broadcastAddr = new PhysicalAddress(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff });
        private BaseAdapterHandler _parent;
        protected PacketArrivalEventHandler _eventHandler;
        private Thread t;
        protected ushort SequenceNumber = 0;

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

            _parent._networkInfo.Common.Ssid.Length = 16;
            _parent._networkInfo.Common.Ssid.Name = ssid;
            _parent._networkInfo.Common.Channel = request.NetworkConfig.Channel == 0 ? _parent.GetRandomChannel() : request.NetworkConfig.Channel;
            _parent._networkInfo.Common.LinkLevel = 3;
            _parent._networkInfo.Common.NetworkType = 2;
            Array16<byte> securityParam = new();
            _parent._random.NextBytes(securityParam.AsSpan());
            _parent._networkInfo.Ldn.SecurityParameter = securityParam;
            _parent._networkInfo.Ldn.AuthenticationId = (ulong)_parent._random.NextInt64();
            _parent._networkInfo.Ldn.NodeCount = 1;
            _parent._networkInfo.Ldn.NodeCountMax = request.NetworkConfig.NodeCountMax;
            _parent._networkInfo.Ldn.Nodes[0].Ipv4Address = NetworkHelpers.ConvertIpv4Address(IPAddress.Parse($"169.254.{networkId}.1"));
            _parent._networkInfo.Ldn.Nodes[0].IsConnected = 1;
            _parent._networkInfo.Ldn.Nodes[0].LocalCommunicationVersion = request.NetworkConfig.LocalCommunicationVersion;
            _parent._networkInfo.Ldn.Nodes[0].UserName = request.UserConfig.UserName;
            _parent._networkInfo.Ldn.SecurityMode = ((ushort)request.SecurityConfig.SecurityMode);

            for (byte i = 0; i < _parent._networkInfo.Ldn.Nodes.Length; i++)
            {
                _parent._networkInfo.Ldn.Nodes[i].NodeId = i;
            }
        }

        protected abstract BeaconFrame GetBeaconFrame();

        protected abstract ActionFrame GetAdvertisementFrame();

        protected abstract void SendAdvertisementFrames();

        public BaseAccessPoint(BaseAdapterHandler handler)
        {
            _parent = handler;
            _eventHandler = new PacketArrivalEventHandler(OnPacketArrival);
            t = new Thread(new ThreadStart(SendAdvertisementFrames));
        }

        public bool Start()
        {
            t.Start();
            return true;
        }
    }
}