using Ryujinx.Common.Memory;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.Types;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Linq;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn
{
    class DebugAdapterHandler : BaseAdapterHandler
    {
        internal CaptureFileReaderDevice _adapter;
        private Network.DebugAccessPoint _accessPoint;

        public DebugAdapterHandler(CaptureFileReaderDevice device) : base(true, true)
        {
            _adapter = device;

            _adapter.OnPacketArrival += new PacketArrivalEventHandler(OnPacketArrival);

            _adapter.Open();

            if (_storeCapture)
            {
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, "AdapterHandler: Dumping raw packets to file...");
                _captureFileWriterDevice = new CaptureFileWriterDevice("debug-cap.pcap");
                _captureFileWriterDevice.Open(_adapter);
            }
        }

        public override bool CreateNetwork(CreateAccessPointRequest request, out NetworkInfo networkInfo)
        {
            _accessPoint = new Network.DebugAccessPoint(this);
            _accessPoint.BuildNewNetworkInfo(request);

            networkInfo = _networkInfo;

            return _accessPoint.Start();
        }

        public override NetworkError Connect(ConnectRequest request)
        {
            NxAuthenticationFrame authRequest = BuildAuthenticationRequest(request);

            return NetworkError.None;
        }

        public override NetworkInfo[] Scan(ushort channel)
        {
            if (!_channels.Contains(channel))
            {
                // LogMsg($"Scan Warning: {channel} is not in channel list.");
                if (_currentChannel != 0)
                {
                    int index = Array.IndexOf(_channels, _currentChannel);
                    if (index == _channels.Length - 1)
                    {
                        index = 0;
                    }
                    else
                    {
                        index++;
                    }
                    channel = _channels[index];
                }
                else
                {
                    channel = _channels[0];
                }
            }
            _currentChannel = channel;

            _scanResults.Clear();

            _adapter.Capture(256);

            return _scanResults.ToArray();
        }

        public override void DisconnectAndStop()
        {
            if (_adapter.Opened)
            {
                _adapter.Close();
            }
        }

        public override void DisconnectNetwork() { }

        public override void Dispose()
        {
            base.Dispose();
            _adapter.Dispose();
        }
    }
}