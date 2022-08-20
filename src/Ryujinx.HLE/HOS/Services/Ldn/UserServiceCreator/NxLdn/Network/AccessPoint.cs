using PacketDotNet.Ieee80211;
using Ryujinx.Common.Memory;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.Types;
using SharpPcap;
using System;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Network
{
    internal class AccessPoint : BaseAccessPoint
    {
        private AdapterHandler _parent;

        public override void BuildNewNetworkInfo(CreateAccessPointRequest request)
        {
            base.BuildNewNetworkInfo(request);
            _parent._adapter.MacAddress.GetAddressBytes().CopyTo(_parent._networkInfo.Common.MacAddress.AsSpan());
            _parent._adapter.MacAddress.GetAddressBytes().CopyTo(_parent._networkInfo.Ldn.Nodes[0].MacAddress.AsSpan());
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "AP: New NetworkInfo created.");
        }

        protected override BeaconFrame GetBeaconFrame()
        {
            InformationElementList infoList = new InformationElementList();
            // optional: Supported Rates: 1B, 2B, 5.5B, 11B, 18Mbit, 24Mbit, 36Mbit, 54Mbit
            // infoList.Add(new InformationElement(InformationElement.ElementId.SupportedRates, new byte[] { 0x82, 0x84, 0x8b, 0x96, 0x24, 0x30, 0x48, 0x6c }));
            // optional: DS Parameter Set: Current Channel -> value
            // infoList.Add(new InformationElement(InformationElement.ElementId.DsParameterSet, new byte[] { (byte)_parent._networkInfo.Common.Channel }));
            // optional: Country info: Code: JP, Env: Any, First channel: 1, Number of channels: 13, Max transmit power level: 20dBm
            // infoList.Add(new InformationElement(InformationElement.ElementId.Country, new byte[] { 0x4a, 0x50, 0x20, 0x01, 0x0d, 0x14 }));
            // optional: Extended Supported Rates: 6, 9, 12, 48 [Mbit/s]
            // infoList.Add(new InformationElement(InformationElement.ElementId.ExtendedSupportedRates, new byte[] { 0x0c, 0x12, 0x18, 0x60 }));
            // optional: Traffic Indication Map: DTIM 1 of 3
            // infoList.Add(new InformationElement(InformationElement.ElementId.TrafficIndicationMap, new byte[] { 0x00, 0x03, 0x00, 0x00 }));
            // NOTE: (optional) missing extended caps
            BeaconFrame beaconFrame = new BeaconFrame(_parent._adapter.MacAddress, _parent._adapter.MacAddress, infoList);

            beaconFrame.BeaconInterval = 100;
            beaconFrame.CapabilityInformation = new CapabilityInformationField
            {
                IsEss = true,
                Privacy = true,
                ShortTimeSlot = true
                // NOTE: (optional) missing Spectrum management
            };
            beaconFrame.UpdateFrameCheckSequence();

            return beaconFrame;
        }

        protected override ActionFrame GetAdvertisementFrame()
        {
            byte[] nonce = BitConverter.GetBytes(_parent._random.Next(0x10000000));
            RadioPacket radioPacket = new RadioPacket();

            ActionFrame action = new ActionFrame(_parent._adapter.MacAddress, broadcastAddr, broadcastAddr);
            AdvertisementFrame advertisement = new AdvertisementFrame()
            {
                Header = _parent._networkInfo.NetworkId,
                Encryption = 2, // can be 1(plain) or 2(AES-CTR) -> https://github.com/kinnay/NintendoClients/wiki/LDN-Protocol#advertisement-payload
                Info = NxLdnNetworkInfo.FromLdnNetworkInfo(_parent._networkInfo.Ldn),
                Nonce = nonce,
                Version = 3 // can be 2(no auth token) or 3(with auth token) - https://github.com/kinnay/NintendoClients/wiki/LDN-Protocol#advertisement-data
            };
            advertisement.WriteHash();
            // LogMsg("AP: Created AdvertisementFrame: ", advertisement);
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"AdvertisementFrame correct hash: {advertisement.CheckHash()}");
            // advertisement.LogProps();
            action.PayloadData = advertisement.Encode();
            action.UpdateFrameCheckSequence();

            return action;
        }

        protected override void SendAdvertisementFrames()
        {
            RadioPacket beaconPacket = new RadioPacket();
            RadioPacket actionPacket = new RadioPacket();
            ChannelRadioTapField channelField = new ChannelRadioTapField(channelMap[_parent._networkInfo.Common.Channel], RadioTapChannelFlags.Channel2Ghz | RadioTapChannelFlags.Cck);
            beaconPacket.Add(channelField);
            actionPacket.Add(channelField);

            ActionFrame action = GetAdvertisementFrame();
            BeaconFrame beacon = GetBeaconFrame();

            while (_parent._adapter.Opened)
            {
                beacon.SequenceControl = new SequenceControlField((ushort)(++_parent._sequenceNumber << 4));
                action.SequenceControl = new SequenceControlField((ushort)(++_parent._sequenceNumber << 4));
                beacon.Timestamp = ((ulong)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds);
                beaconPacket.PayloadPacket = beacon;
                actionPacket.PayloadPacket = action;
                beaconPacket.UpdateCalculatedValues();
                actionPacket.UpdateCalculatedValues();

                _parent._adapter.SendPacket(beaconPacket);
                // Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"beaconPacket Sent");
                _parent._adapter.SendPacket(actionPacket);
                // Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"radioPacket Sent");

                if (_parent._storeCapture && _parent._captureFileWriterDevice.Opened)
                {
                    // LogMsg($"AP: Writing packet to file...");
                    _parent._captureFileWriterDevice.SendPacket(beaconPacket);
                    _parent._captureFileWriterDevice.SendPacket(actionPacket);
                }
                // https://github.com/kinnay/NintendoClients/wiki/LDN-Protocol#overview

                Thread.Sleep(100);
            }
        }

        public AccessPoint(AdapterHandler handler) : base(handler)
        {
            _parent = handler;
            _parent._adapter.OnPacketArrival += _eventHandler;
        }

        ~AccessPoint()
        {
            _parent._adapter.OnPacketArrival -= _eventHandler;
        }
    }
}