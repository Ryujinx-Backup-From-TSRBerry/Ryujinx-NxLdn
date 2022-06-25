using PacketDotNet;
using PacketDotNet.Ieee80211;
using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Services.Ldn.NxLdn.Capabilities;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Packets;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.Types;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;

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

        public override bool CreateNetwork(CreateAccessPointRequest request, Array384<byte> advertiseData, ushort advertiseDataLength, out NetworkInfo networkInfo)
        {
            _ap = new Network.AccessPoint(this);
            _ap.BuildNewNetworkInfo(request, advertiseData, advertiseDataLength);
            SetAdapterChannel(_networkInfo.Common.Channel);
            networkInfo = _networkInfo;
            return _ap.Start();
        }

        public override NetworkError Connect(ConnectRequest request)
        {
            NxAuthenticationFrame authRequest = base.BuildAuthenticationRequest(request);
            PhysicalAddress destAddr = new PhysicalAddress(request.NetworkInfo.Common.MacAddress.AsSpan().ToArray());
            InformationElementList infoElementList = new InformationElementList();
            AuthenticationFrame authFrame = new AuthenticationFrame(_adapter.MacAddress, destAddr, destAddr, infoElementList);
            authFrame.PayloadData = authRequest.Encode();
            _adapter.SendPacket(authFrame);
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
            if (_scanResults.Count > 0)
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