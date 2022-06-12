using PacketDotNet;
using PacketDotNet.Ieee80211;
using Ryujinx.HLE.HOS.Services.Ldn.NxLdn.Capabilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using AuthenticationFrame = Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets.AuthenticationFrame;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn
{
    class AdapterHandler : BaseAdapterHandler
    {
        internal LibPcapLiveDevice _adapter;
        private Network.AccessPoint _ap;

        private bool SetAdapterChannel(ushort channel)
        {
            using (Process process = new Process())
            {
                process.StartInfo.CreateNoWindow = true;
                if (OperatingSystem.IsLinux())
                {
                    process.StartInfo.FileName = "iw";
                    process.StartInfo.Arguments = $"dev {_adapter.Name} set channel {channel}";
                }
                else if (OperatingSystem.IsWindows())
                {
                    process.StartInfo.FileName = $"{Environment.SystemDirectory}\\Npcap\\WlanHelper.exe";
                    process.StartInfo.Arguments = $"\"{_adapter.Name}\" channel {channel}";
                }
                else
                {
                    throw new NotImplementedException();
                }
                process.StartInfo.RedirectStandardError = true;
                // LogMsg($"AdapterHandler: Setting channel to {channel}...");
                process.Start();
                process.WaitForExit();
                // LogMsg($"AdapterHandler: process exited with code: {process.ExitCode} - Error Output: {process.StandardError.ReadToEnd()}");
                if (process.ExitCode == 0)
                {
                    currentChannel = channel;
                }

                return process.ExitCode == 0;
            }
        }

        /*
        * Handles everything related to the WiFi adapter
        */
        public AdapterHandler(LibPcapLiveDevice device, bool storeCapture = false) : base(storeCapture, false)
        {
            _adapter = device;

            // If this wasn't executed in main it will fail here
            // But if it was then there is no need for that call (since the caps are already set correctly)
            if (OperatingSystem.IsLinux() && !Capabilities.InheritCapabilities())
            {
                throw new SystemException("Raising capabilities failed");
            }

            LogMsg("AdapterHandler trying to access the adapter now...");

            // Crashing here means the device is not ready or "operation not permitted"
            // Linux: CAP_NET_RAW,CAP_NET_ADMIN are required
            // Windows: Npcap needs to be configured without admin-only access or Ryujinx needs to be started as Admin
            // Configure and open wifi adapter
            _adapter.Open(new DeviceConfiguration()
            {
                Monitor = MonitorMode.Active,
                // TODO: Test without monitor mode
                // Mode = DeviceModes.Promiscuous,
                LinkLayerType = LinkLayers.Ieee80211
            });

            if (_storeCapture)
            {
                LogMsg("AdapterHandler: Dumping raw packets to file...");
                _captureFileWriterDevice = new CaptureFileWriterDevice("debug-cap.pcap");
                _captureFileWriterDevice.Open(_adapter);
            }

            LogMsg("AdapterHandler opened the adapter successfully!");

            _adapter.OnPacketArrival += new PacketArrivalEventHandler(OnPacketArrival);
        }

        public override bool CreateNetwork(CreateAccessPointRequest request, byte[] advertiseData, out NetworkInfo networkInfo)
        {
            _ap = new Network.AccessPoint(this);
            _ap.BuildNewNetworkInfo(request, advertiseData);
            SetAdapterChannel(_networkInfo.Common.Channel);
            networkInfo = _networkInfo;
            return _ap.Start();
        }

        public NetworkError Connect(ConnectRequest request)
        {
            byte[] authKey = new byte[0x10];
            _random.NextBytes(authKey);
            AuthenticationRequest authRequest = new AuthenticationRequest()
            {
                UserName = request.UserConfig.UserName,
                AppVersion = BitConverter.ToUInt16(_gameVersion)
            };
            ChallengeRequestParameter challenge = new ChallengeRequest()
            {
                Token = request.NetworkInfo.Ldn.AuthenticationId,
                Nonce = (ulong)_random.NextInt64(0x100000000), // FIXME: This should probably be done in another way
                DeviceId = (ulong)_random.NextInt64(0x100000000) // FIXME: This should probably be done in another way
            }.Encode();
            // TODO: Figure out if this is the right packet type
            DataDataFrame data = new DataDataFrame()
            {
                SourceAddress = _adapter.MacAddress,
                DestinationAddress = new PhysicalAddress(request.NetworkInfo.Common.MacAddress),
                PayloadData = new AuthenticationFrame()
                {
                    Version = 3, // FIXME: usually this will be 3 (with encryption), but there needs to be a way to check this
                    StatusCode = AuthenticationStatusCode.Success,
                    IsResponse = false,
                    Header = request.NetworkInfo.NetworkId,
                    NetworkKey = request.NetworkInfo.NetworkId.SessionId,
                    AuthenticationKey = authKey, // FIXME: Secure RNG?
                    Size = 64 + 0x300 + 0x24,
                    Payload = authRequest,
                    ChallengeRequest = challenge
                }.Encode(),
            };
            _adapter.SendPacket(data);
            return NetworkError.None;
        }

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
            if (currentChannel != channel)
            {
                if (!SetAdapterChannel(channel))
                {
                    LogMsg($"Scan Error: Could not set adapter channel: {channel}");
                    return new NetworkInfo[] { };
                }
            }

            _scanResults.Clear();

            _adapter.StartCapture();

            Thread.Sleep(_scanDwellTime);

            LogMsg($"Returning Networks: {_scanResults.Count}");

            return _scanResults.ToArray();
        }

        public override void DisconnectAndStop()
        {
            LogMsg("AdapterHandler cleaning up...");
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
