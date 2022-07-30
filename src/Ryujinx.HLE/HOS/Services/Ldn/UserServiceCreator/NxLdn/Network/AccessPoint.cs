using PacketDotNet.Ieee80211;
using Ryujinx.Common.Memory;
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
            LogMsg("AP: New NetworkInfo created.");
        }

        protected override RadioPacket GetAdvertisementFrame()
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
            LogMsg($"AdvertisementFrame correct hash: {advertisement.CheckHash()}");
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
            byte[] ssid = new byte[32];
            Array.Fill<byte>(ssid, 0);
            InformationElementList infoList = new InformationElementList();
            infoList.Add(new InformationElement(InformationElement.ElementId.ServiceSetIdentity, ssid));
            // Supported Rates: 1B, 2B, 5.5B, 11B, 18Mbit, 24Mbit, 36Mbit, 54Mbit
            infoList.Add(new InformationElement(InformationElement.ElementId.SupportedRates, new byte[] { 0x82, 0x84, 0x8b, 0x96, 0x24, 0x30, 0x48, 0x6c }));
            // DS Parameter Set: Current Channel -> value
            infoList.Add(new InformationElement(InformationElement.ElementId.DsParameterSet, new byte[] { (byte)_parent._networkInfo.Common.Channel }));
            // TIM: DTIM count: 0, DITM period: 3, Bitmap control: 0x00, Partial virtual bitmap: 0x00
            // infoList.Add(new InformationElement(InformationElement.ElementId.TrafficIndicationMap, new byte[] { 0x00, 0x03, 0x00, 0x00 }));
            // Country info: Code: JP, Env: Any, First channel: 1, Number of channels: 13, Max transmit power level: 20dBm
            infoList.Add(new InformationElement(InformationElement.ElementId.Country, new byte[] { 0x4a, 0x50, 0x20, 0x01, 0x0d, 0x14 }));
            // Local power constraint: 0
            // infoList.Add(new InformationElement(InformationElement.ElementId.PowerConstraint, new byte[] { 0x00 }));
            // TPC report: Transmit power: 17, Link margin: 0
            // infoList.Add(new InformationElement(InformationElement.ElementId.TransmitPowerControlReport, new byte[] { 0x11, 0x00 }));
            // ERP: Bitmap: 0x00
            // infoList.Add(new InformationElement(InformationElement.ElementId.ErpInformation, new byte[] { 0x00 }));
            // Extended Supported Rates: 6, 9, 12, 48 [Mbit/s]
            infoList.Add(new InformationElement(InformationElement.ElementId.ExtendedSupportedRates, new byte[] { 0x0c, 0x12, 0x18, 0x60 }));
            // RSN: Version: 1, Group cipher suite: (OUI: 00:0f:ac, type: AES (CCM)), Pairwise cipher suite: (OUI: 00:0f:ac, type: AES (CCM)), Auth key management: (OUI: 00:0f:ac, type: PSK), Caps Bitmask: 0x000c
            // infoList.Add(new InformationElement(InformationElement.ElementId.RobustSecurityNetwork, new byte[] { 0x01, 0x00, 0x00, 0x0f, 0xac, 0x04, 0x01, 0x00, 0x00, 0x0f, 0xac, 0x04, 0x01, 0x00, 0x0f, 0xac, 0x02, 0x0c, 0x00 }));
            // HT Caps: Info Bitmask: 0x0020, A-MPDU params: 0x17, Rx MCS Set: 0xff 0x00*15, HT extended caps: 0x0000, TxBF caps: 0x00000000, Antenna selection caps: 0x00
            // infoList.Add(new InformationElement(InformationElement.ElementId.HighThroughputCapabilities, new byte[] { 0x20, 0x00, 0x17, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }));
            // HT Info: Info Subsets: 0x0800000000, Rx Basic MCS Set: 0x00*16
            // infoList.Add(new InformationElement(InformationElement.ElementId.HighThroughputInformation, new byte[] { 0x01, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }));
            // NOTE: missing extended caps
            BeaconFrame beaconFrame = new BeaconFrame(_parent._adapter.MacAddress, _parent._adapter.MacAddress, infoList);
            beaconFrame.BeaconInterval = 100;
            beaconFrame.CapabilityInformation = new CapabilityInformationField
            {
                IsEss = true,
                Privacy = true,
                ShortTimeSlot = true
                // NOTE: missing Spectrum management
            };
            beaconFrame.UpdateFrameCheckSequence();
            RadioPacket beaconPacket = new RadioPacket();
            beaconPacket.PayloadPacket = beaconFrame;
            while (_parent._adapter.Opened)
            {
                beaconPacket.UpdateCalculatedValues();
                _parent._adapter.SendPacket(beaconPacket);
                radioPacket.UpdateCalculatedValues();
                _parent._adapter.SendPacket(radioPacket);
                if (_parent._storeCapture && _parent._captureFileWriterDevice.Opened)
                {
                    // LogMsg($"AP: Writing packet to file...");
                    _parent._captureFileWriterDevice.SendPacket(beaconPacket);
                    _parent._captureFileWriterDevice.SendPacket(radioPacket);
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