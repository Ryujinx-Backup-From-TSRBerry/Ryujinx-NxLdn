using System;
using System.Diagnostics;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Capabilities
{
    static class WifiHelper
    {
        public static bool SetWifiAdapterChannel(string wifiAdapterName, ushort channel)
        {
            if (OperatingSystem.IsLinux())
            {
                using (Process process = new Process())
                {
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.FileName = "iw";
                    process.StartInfo.Arguments = $"dev {wifiAdapterName} set channel {channel}";
                    process.StartInfo.RedirectStandardError = true;
                    // Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"AdapterHandler: Setting channel to {channel}...");
                    process.Start();
                    process.WaitForExit();
                    // Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"AdapterHandler: process exited with code: {process.ExitCode} - Error Output: {process.StandardError.ReadToEnd()}");

                    return process.ExitCode == 0;
                }
            }
            else if (OperatingSystem.IsWindows())
            {
                using (PacketSharp packet = new(wifiAdapterName.Split('{', '}')[1]))
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
    }
}