using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.NxLdn.Capabilities
{
    public static class Capabilities {
        private static void LogMsg(string msg, object obj = null) {
            if (obj != null) {
                string jsonString = JsonHelper.Serialize<object>(obj, true);
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, msg + "\n" + jsonString);
            }
            else {
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, msg);
            }
        }

        public static readonly uint _LINUX_CAPABILITY_VERSION_3 = 0x20080522;

        // Capabilities
        public static readonly uint CAP_NET_ADMIN = 12;
        public static readonly uint CAP_NET_RAW = 13;

        // PRCRTL options
        public static readonly int PR_GET_SECCOMP = 21;
        public static readonly int PR_CAPBSET_READ = 23;
        public static readonly int PR_CAP_AMBIENT = 47;

        // PR_CAP_AMBIENT args
        // https://man7.org/linux/man-pages/man2/prctl.2.html
        // > In all of the above operations, arg4 and arg5 must be specified as 0.
        public static readonly uint PR_CAP_AMBIENT_IS_SET = 1;
        public static readonly uint PR_CAP_AMBIENT_RAISE = 2;

        // Syscall numbers
        public static readonly long SYS_capget = 125;
        public static readonly long SYS_capset = 126;

        // Functions
        [DllImport("libc", SetLastError = true)]
        public static extern int prctl(int option, ulong arg2, ulong arg3, ulong arg4, ulong arg5);

        // Syscall for capget/capset
        [DllImport("libc", SetLastError = true)]
        public static extern int syscall(long number, ref UserCapHeaderStruct hdrp, ref UserCapDataStructArray datap);

        public static bool InheritCapabilities() {
            UserCapHeaderStruct hs = new UserCapHeaderStruct
            {
                pid = 0,
                version = Capabilities._LINUX_CAPABILITY_VERSION_3
            };
            UserCapDataStructArray ds = new UserCapDataStructArray();

            LogMsg("Enabling inheritable capabilities...");
            if (Capabilities.syscall(Capabilities.SYS_capget, ref hs, ref ds) != 0)
            {
                int errno = Marshal.GetLastPInvokeError();
                LogMsg($"CAPGET syscall failed: {errno}");
                return false;
            }
            // LogMsg("CAPGET result:", ds);
            ds.dataStructs[0].inheritable = ds.dataStructs[0].permitted;
            ds.dataStructs[1].inheritable = ds.dataStructs[1].permitted;
            if (Capabilities.syscall(Capabilities.SYS_capset, ref hs, ref ds) != 0)
            {
                int errno = Marshal.GetLastPInvokeError();
                LogMsg($"CAPSET syscall failed: {errno}");
                return false;
            }

            LogMsg("Setting ambient capabilities...");
            if (prctl(PR_CAP_AMBIENT, PR_CAP_AMBIENT_RAISE, CAP_NET_ADMIN, 0, 0) != 0) {
                int errno = Marshal.GetLastPInvokeError();
                LogMsg($"PR_CAP_AMBIENT_RAISE failed for CAP_NET_ADMIN: {errno}");
                return false;
            }

            if (prctl(PR_CAP_AMBIENT, PR_CAP_AMBIENT_RAISE, CAP_NET_RAW, 0, 0) != 0)
            {
                int errno = Marshal.GetLastPInvokeError();
                LogMsg($"PR_CAP_AMBIENT_RAISE failed for CAP_NET_RAW: {errno}");
                return false;
            }

            // LogMsg($"Ambient caps is_set rc: {prctl(PR_CAP_AMBIENT, PR_CAP_AMBIENT_IS_SET, CAP_NET_ADMIN, 0, 0)} {prctl(PR_CAP_AMBIENT, PR_CAP_AMBIENT_IS_SET, CAP_NET_RAW, 0, 0)}");
            return true;
        }
    }
}
