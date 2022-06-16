using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Linq;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn
{
    class DebugAdapterHandler : BaseAdapterHandler
    {
        internal CaptureFileReaderDevice _adapter;
        private Network.DebugAccessPoint _ap;

        /*
        * Handles everything related to the WiFi adapter
        */
        public DebugAdapterHandler(CaptureFileReaderDevice device) : base(true, true)
        {
            _adapter = device;

            _adapter.OnPacketArrival += new PacketArrivalEventHandler(OnPacketArrival);

            try
            {
                _adapter.Open();
            }
            catch (Exception ex)
            {
                LogMsg($"DebugAdapterHandler: Exception: {ex}");
                throw ex;
            }

            if (_storeCapture)
            {
                LogMsg("AdapterHandler: Dumping raw packets to file...");
                _captureFileWriterDevice = new CaptureFileWriterDevice("debug-cap.pcap");
                _captureFileWriterDevice.Open(_adapter);
            }
        }

        public override bool CreateNetwork(CreateAccessPointRequest request, byte[] advertiseData, out NetworkInfo networkInfo)
        {
            _ap = new Network.DebugAccessPoint(this);
            _ap.BuildNewNetworkInfo(request, advertiseData);
            networkInfo = _networkInfo;
            return _ap.Start();
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

        public override NetworkInfo[] Scan(ushort channel)
        {
            if (!channels.Contains(channel))
            {
                // LogMsg($"Scan Warning: {channel} is not in channel list.");
                if (currentChannel != 0)
                {
                    int index = Array.IndexOf(channels, currentChannel);
                    if (index == channels.Length - 1)
                    {
                        index = 0;
                    }
                    else
                    {
                        index++;
                    }
                    channel = channels[index];
                }
                else
                {
                    channel = channels[0];
                }
            }
            currentChannel = channel;

            _scanResults.Clear();

            _adapter.StartCapture();

            Thread.Sleep(_scanDwellTime);

            return _scanResults.ToArray();
        }

        public override void DisconnectAndStop()
        {
            if (_adapter.Opened)
                _adapter.Close();
        }

        public override void DisconnectNetwork()
        {
            _adapter.StopCapture();
        }

        public override void Dispose()
        {
            base.Dispose();
            _adapter.Dispose();
        }
    }
}
