using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 2)]
    internal struct PingMessage
    {
        public byte Requester;

        public byte Id;
    }
}
