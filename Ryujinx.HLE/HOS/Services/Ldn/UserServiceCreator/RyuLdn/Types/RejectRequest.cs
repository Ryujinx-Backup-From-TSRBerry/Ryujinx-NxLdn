using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    internal struct RejectRequest
    {
        public uint NodeId;

        public DisconnectReason DisconnectReason;

        public RejectRequest(DisconnectReason disconnectReason, uint nodeId)
        {
            DisconnectReason = disconnectReason;
            NodeId = nodeId;
        }
    }
}
