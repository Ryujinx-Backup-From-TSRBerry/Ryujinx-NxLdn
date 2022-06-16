using System;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [Flags]
    internal enum NodeLatestUpdateFlags : byte
    {
        None = 0x0,
        Connect = 0x1,
        Disconnect = 0x2
    }
}
