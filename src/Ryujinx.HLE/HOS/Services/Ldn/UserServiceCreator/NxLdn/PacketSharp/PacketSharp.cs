using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using static Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.NativeWin;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn
{
    public class PacketSharp : IDisposable
    {
        public Version Version
        {
            get
            {
                string version = Marshal.PtrToStringAnsi(PacketGetVersion());

                return version != null ? new Version(version) : new Version(0, 0, 0);
            }
        }

        public Version DriverVersion
        {
            get
            {
                string version = Marshal.PtrToStringAnsi(PacketGetDriverVersion());

                return version != null ? new Version(version) : new Version(0, 0, 0);
            }
        }

        public uint Channel
        {
            get => MakeOIDRequest<uint>(_lpAdapter, OID_DOT11_CURRENT_CHANNEL, false, 0);
            set
            {
                MakeOIDRequest(_lpAdapter, OID_DOT11_CURRENT_CHANNEL, true, value);
                Thread.Sleep(5); // NOTE: Update the channel too fast doesn't seems to works.
            }
        }

        public ModeCapability[] Modes
        {
            get
            {
                List<ModeCapability> modes = new();

                DOT11_OPERATION_MODE_CAPABILITY modeCapability = MakeOIDRequest<DOT11_OPERATION_MODE_CAPABILITY>(_lpAdapter, OID_DOT11_OPERATION_MODE_CAPABILITY, false, new());

                if ((modeCapability.OpModeCapability & DOT11_OPERATION_MODE_EXTENSIBLE_AP) == DOT11_OPERATION_MODE_EXTENSIBLE_AP)
                {
                    modes.Add(ModeCapability.Master);
                }

                if ((modeCapability.OpModeCapability & DOT11_OPERATION_MODE_EXTENSIBLE_STATION) == DOT11_OPERATION_MODE_EXTENSIBLE_STATION)
                {
                    modes.Add(ModeCapability.Managed);
                }

                if ((modeCapability.OpModeCapability & DOT11_OPERATION_MODE_NETWORK_MONITOR) == DOT11_OPERATION_MODE_NETWORK_MONITOR)
                {
                    modes.Add(ModeCapability.Monitor);
                }

                return modes.ToArray();
            }
        }

        private string    _guid;
        private IntPtr    _pLpAdapter;
        private LPADAPTER _lpAdapter;

        public PacketSharp(string guid)
        {
            _guid       = guid;
            _           = PacketGetMonitorMode(AdapterGuidToNPCAPString(_guid));
            _pLpAdapter = PacketOpenAdapter(AdapterGuidToNPCAPString(_guid));
            _lpAdapter  = Marshal.PtrToStructure<LPADAPTER>(_pLpAdapter);
        }

        private string AdapterGuidToNPCAPString(string guid)
        {
            return $"\\Device\\NPF_{{{guid}}}";
        }

        private T MakeOIDRequest<T>(LPADAPTER lpAdapter, uint oidIdentifier, bool set, T data) where T : unmanaged
        {
            Span<byte> tempBuffer = new byte[Marshal.SizeOf<PacketOidData>() + Marshal.SizeOf<T>()];

            ref PacketOidData packetOidData = ref MemoryMarshal.Cast<byte, PacketOidData>(tempBuffer[..Marshal.SizeOf<PacketOidData>()])[0];

            packetOidData.Oid = oidIdentifier;
            packetOidData.Length = (uint)Marshal.SizeOf<T>();

            if (set)
            {
                MemoryMarshal.Cast<byte, T>(tempBuffer.Slice(Marshal.SizeOf<PacketOidData>(), Marshal.SizeOf<T>()))[0] = data;
            }

            if (PacketRequest(ref lpAdapter, set, ref packetOidData))
            {
                return MemoryMarshal.Cast<byte, T>(tempBuffer[Marshal.SizeOf<PacketOidData>()..])[0];
            }
            else
            {
                return Span<T>.Empty[0];
            }
        }

        public void Dispose()
        {
            if (_pLpAdapter != IntPtr.Zero)
            {
                PacketCloseAdapter(_pLpAdapter);
                _pLpAdapter = IntPtr.Zero;
            }
        }
    }
}