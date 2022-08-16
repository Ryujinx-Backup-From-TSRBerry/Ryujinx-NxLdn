using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Capabilities
{
    [StructLayout(LayoutKind.Sequential)]
    public struct UserCapHeaderStruct
    {
        public uint version;
        public int pid;
    }
}
