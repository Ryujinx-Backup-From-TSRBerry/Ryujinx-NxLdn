using PacketDotNet.Ieee80211;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnRyu.Types;
using SharpPcap;
using System;
using System.Net.NetworkInformation;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Network
{
    internal class DebugAccessPoint : BaseAccessPoint
    {
        private DebugAdapterHandler _parent;
        private PhysicalAddress _macAddress;

        public override void BuildNewNetworkInfo(CreateAccessPointRequest request)
        {
            base.BuildNewNetworkInfo(request);
            _macAddress.GetAddressBytes().CopyTo(_parent._networkInfo.Common.MacAddress.AsSpan());
            _macAddress.GetAddressBytes().CopyTo(_parent._networkInfo.Ldn.Nodes[0].MacAddress.AsSpan());
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "AP: New NetworkInfo created.");
        }

        protected override RadioPacket GetAdvertisementFrame()
        {
            byte[] nonce = BitConverter.GetBytes(_parent._random.Next(0x10000000));
            RadioPacket radioPacket = new RadioPacket();

            ActionFrame action = new ActionFrame(_macAddress, broadcastAddr, broadcastAddr);
            AdvertisementFrame advertisement = new AdvertisementFrame()
            {
                Header = _parent._networkInfo.NetworkId,
                Encryption = 2, // can be 1(plain) or 2(AES-CTR) -> https://github.com/kinnay/NintendoClients/wiki/LDN-Protocol#advertisement-payload
                Info = NxLdnNetworkInfo.FromLdnNetworkInfo(_parent._networkInfo.Ldn),
                Nonce = nonce,
                Version = 3 // can be 2(no auth token) or 3(with auth token) - https://github.com/kinnay/NintendoClients/wiki/LDN-Protocol#advertisement-data
            };
            advertisement.WriteHash();
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"AP: Created AdvertisementFrame: \n{JsonHelper.Serialize<object>(advertisement, true)}");
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"AdvertisementFrame correct hash: {advertisement.CheckHash()}");
            // advertisement.LogProps();
            action.PayloadData = advertisement.Encode();
            action.UpdateFrameCheckSequence();
            radioPacket.PayloadPacket = action;
            radioPacket.UpdateCalculatedValues();

            return radioPacket;
        }

        protected override void SpamActionFrame()
        {
            RadioPacket radioPacket = GetAdvertisementFrame();
            while (_parent._adapter.Opened)
            {
                if (_parent._storeCapture && _parent._captureFileWriterDevice.Opened)
                {
                    // Logger.Info?.PrintMsg(LogClass.ServiceLdn, "AP: Writing packet to file...");
                    _parent._captureFileWriterDevice.SendPacket(radioPacket);
                }
                // https://github.com/kinnay/NintendoClients/wiki/LDN-Protocol#overview
                Thread.Sleep(100);
            }
        }

        public DebugAccessPoint(DebugAdapterHandler handler) : base(handler)
        {
            _parent = handler;
            _parent._adapter.OnPacketArrival += _eventHandler;
            byte[] randomMac = new byte[6];
            _parent._random.NextBytes(randomMac);
            _macAddress = new PhysicalAddress(randomMac);
        }

        ~DebugAccessPoint()
        {
            _parent._adapter.OnPacketArrival -= _eventHandler;
        }
    }
}