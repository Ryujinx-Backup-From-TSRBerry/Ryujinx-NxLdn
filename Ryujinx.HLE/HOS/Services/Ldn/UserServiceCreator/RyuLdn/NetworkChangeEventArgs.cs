using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn
{
    internal class NetworkChangeEventArgs : EventArgs
    {
        public NetworkInfo Info;

        public bool Connected;

        public DisconnectReason DisconnectReason;

        public NetworkChangeEventArgs(NetworkInfo info, bool connected, DisconnectReason disconnectReason = DisconnectReason.None)
        {
            Info = info;
            Connected = connected;
            DisconnectReason = disconnectReason;
        }

        public DisconnectReason DisconnectReasonOrDefault(DisconnectReason defaultReason)
        {
            if (DisconnectReason != 0)
            {
                return DisconnectReason;
            }
            return defaultReason;
        }
    }
}
