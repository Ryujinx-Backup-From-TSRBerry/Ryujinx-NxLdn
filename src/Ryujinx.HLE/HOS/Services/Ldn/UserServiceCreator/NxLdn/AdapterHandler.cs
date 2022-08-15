using PacketDotNet;
using PacketDotNet.Ieee80211;
using Ryujinx.Common.Logging;
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
        internal LibPcapLiveDevice  _adapter;
        private Network.AccessPoint _accessPoint;

        private bool SetAdapterChannel(ushort channel)
        {
            if (OperatingSystem.IsLinux())
            {
                using (Process process = new Process())
                {
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.FileName = "iw";
                    process.StartInfo.Arguments = $"dev {_adapter.Name} set channel {channel}";
                    process.StartInfo.RedirectStandardError = true;
                    // Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"AdapterHandler: Setting channel to {channel}...");
                    process.Start();
                    process.WaitForExit();
                    // Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"AdapterHandler: process exited with code: {process.ExitCode} - Error Output: {process.StandardError.ReadToEnd()}");
                    if (process.ExitCode == 0)
                    {
                        _currentChannel = channel;
                    }

                    return process.ExitCode == 0;
                }
            }
            else if (OperatingSystem.IsWindows())
            {
                using (PacketSharp packet = new(_adapter.Name.Split('{', '}')[1]))
                {
                    uint oldChannel = packet.Channel;
                    packet.Channel = channel;

                    return true; // TODO: Do a better error handling on PacketSharp class if then channel can't be changed.
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public AdapterHandler(LibPcapLiveDevice adapter, bool storeCapture = false, bool debugMode = false) : base(storeCapture, debugMode)
        {
            _adapter = adapter;;

            // NOTE: If this wasn't executed in main it will fail here.
            //       But if it was then there is no need for that call (since the caps are already set correctly).
            if (OperatingSystem.IsLinux() && !Capabilities.InheritCapabilities())
            {
                throw new SystemException("Raising capabilities failed");
            }

            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "AdapterHandler trying to access the adapter now...");

            // NOTE: Crashing here means the device is not ready or "operation not permitted".
            //       - Linux: CAP_NET_RAW,CAP_NET_ADMIN are required.
            //       - Windows: Npcap needs to be configured without admin-only access or Ryujinx needs to be started as Admin.
            _adapter.Open(new DeviceConfiguration()
            {
                Mode          = DeviceModes.MaxResponsiveness,
                Monitor       = MonitorMode.Active, // TODO: Test without monitor mode
                LinkLayerType = LinkLayers.Ieee80211
            });

            if (_storeCapture)
            {
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, "AdapterHandler: Dumping raw packets to file...");

                _captureFileWriterDevice = new CaptureFileWriterDevice("debug-cap.pcap");
                _captureFileWriterDevice.Open(_adapter);
            }

            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "AdapterHandler opened the adapter successfully!");

            // Register our handler function to the "packet arrival" event.
            _adapter.OnPacketArrival += new PacketArrivalEventHandler(OnPacketArrival);
        }

        public override bool CreateNetwork(CreateAccessPointRequest request, out NetworkInfo networkInfo)
        {
            _accessPoint = new Network.AccessPoint(this);
            _accessPoint.BuildNewNetworkInfo(request);

            SetAdapterChannel(_networkInfo.Common.Channel);

            networkInfo = _networkInfo;

            return _accessPoint.Start();
        }

        public override NetworkError Connect(ConnectRequest request)
        {
            RadioPacket            radioPacket     = new RadioPacket();
            NxAuthenticationFrame  authRequest     = BuildAuthenticationRequest(request);
            PhysicalAddress        destAddr        = new PhysicalAddress(request.NetworkInfo.Common.MacAddress.AsSpan().ToArray()); // 512
            InformationElementList infoElementList = new InformationElementList();
            AuthenticationFrame    authFrame       = new AuthenticationFrame(_adapter.MacAddress, destAddr, destAddr, infoElementList)
            {
                PayloadData = authRequest.Encode()
            };

            radioPacket.PayloadPacket = authFrame;

            _adapter.SendPacket(radioPacket);

            if (_storeCapture)
            {
                _captureFileWriterDevice.SendPacket(radioPacket);
            }

            // TODO: Add AuthenticationResponse handling
            Thread.Sleep(5000);

            return NetworkError.None;
        }

        public override NetworkInfo[] Scan(ushort channel)
        {
            // NOTE: Channel should be checked somewhere else ?
            if (!_channels.Contains(channel))
            {
                // Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"Scan Warning: {channel} is not in channel list.");
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

            if (_currentChannel != channel)
            {
                if (!SetAdapterChannel(channel))
                {
                    Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"Scan Error: Could not set adapter channel: {channel}");

                    return Array.Empty<NetworkInfo>();
                }
            }

            _scanResults.Clear();

            // NOTE: Using _adapter.StartCapture() and _adapter.StartCapture() in a small delay doesn't seems to be handled correctly under windows.
            //       Capture a large amount of packet could avoid this issue without speed issues.
            _adapter.Capture(256);

            if (_scanResults.Count > 0)
            {
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"Returning Networks: {_scanResults.Count}");
            }

            return _scanResults.ToArray();
        }

        public override void DisconnectAndStop()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "AdapterHandler cleaning up...");

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