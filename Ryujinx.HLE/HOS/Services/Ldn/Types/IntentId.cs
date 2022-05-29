using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct IntentId
    {
        public long LocalCommunicationId;

        public ushort Reserved1;

        // could also be called game mode
        public ushort SceneId;

        public uint Reserved2;
    }
}
