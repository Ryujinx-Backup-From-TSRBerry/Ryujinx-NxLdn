using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Ryujinx.Common.Utilities
{
    public static class NetworkHelpers
    {
        private static (IPInterfaceProperties, UnicastIPAddressInformation) GetLocalInterface(NetworkInterface adapter, bool isPreferred)
        {
            IPInterfaceProperties properties = adapter.GetIPProperties();
            if (isPreferred || (properties.GatewayAddresses.Count > 0 && properties.DnsAddresses.Count > 0))
            {
                foreach (UnicastIPAddressInformation info in properties.UnicastAddresses)
                {
                    if (info.Address.GetAddressBytes().Length == 4)
                    {
                        return (properties, info);
                    }
                }
            }
            return (null, null);
        }

        public static (IPInterfaceProperties, UnicastIPAddressInformation) GetLocalInterface(string lanInterfaceId = "0")
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return (null, null);
            }
            IPInterfaceProperties targetProperties = null;
            UnicastIPAddressInformation targetAddressInfo = null;
            NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            bool hasPreference = lanInterfaceId != "0";
            NetworkInterface[] array = allNetworkInterfaces;
            foreach (NetworkInterface adapter in array)
            {
                bool isPreferred = adapter.Id == lanInterfaceId;
                if (!isPreferred && (targetProperties != null || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback || !adapter.Supports(NetworkInterfaceComponent.IPv4)))
                {
                    continue;
                }
                var (properties, info) = GetLocalInterface(adapter, isPreferred);
                if (properties != null)
                {
                    targetProperties = properties;
                    targetAddressInfo = info;
                    if (isPreferred || !hasPreference)
                    {
                        break;
                    }
                }
            }
            return (targetProperties, targetAddressInfo);
        }

        public static bool SupportsDynamicDns()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        public static uint ConvertIpv4Address(IPAddress ipAddress)
        {
            byte[] addressBytes = ipAddress.GetAddressBytes();
            Array.Reverse(addressBytes);
            return BitConverter.ToUInt32(addressBytes);
        }

        public static uint ConvertIpv4Address(string ipAddress)
        {
            return ConvertIpv4Address(IPAddress.Parse(ipAddress));
        }
    }
}
