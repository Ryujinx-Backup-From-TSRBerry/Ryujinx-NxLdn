using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Ldn.NxLdn.Capabilities
{
    public class PacketSharp : IDisposable
    {
        public Version Version
        {
            get
            {
                string version = Marshal.PtrToStringAnsi(NativeWin.PacketGetVersion());

                return version != null ? new Version(version) : new Version(0, 0, 0);
            }
        }

        public Version DriverVersion
        {
            get
            {
                string version = Marshal.PtrToStringAnsi(NativeWin.PacketGetDriverVersion());

                return version != null ? new Version(version) : new Version(0, 0, 0);
            }
        }

        private uint _channel;

        public uint Channel
        {
            get => _channel;
            set
            {
                _channel = MakeOIDRequest(_lpAdapter, NativeWin.OID_DOT11_CURRENT_CHANNEL, true, value);
                Thread.Sleep(5); // NOTE: Update the channel too fast doesn't seems to works.
            }
        }

        public ModeCapability[] Modes
        {
            get
            {
                List<ModeCapability> modes = new();

                NativeWin.DOT11_OPERATION_MODE_CAPABILITY modeCapability = MakeOIDRequest<NativeWin.DOT11_OPERATION_MODE_CAPABILITY>(_lpAdapter, NativeWin.OID_DOT11_OPERATION_MODE_CAPABILITY, false, new());

                if ((modeCapability.OpModeCapability & NativeWin.DOT11_OPERATION_MODE_EXTENSIBLE_AP) == NativeWin.DOT11_OPERATION_MODE_EXTENSIBLE_AP)
                {
                    modes.Add(ModeCapability.Master);
                }

                if ((modeCapability.OpModeCapability & NativeWin.DOT11_OPERATION_MODE_EXTENSIBLE_STATION) == NativeWin.DOT11_OPERATION_MODE_EXTENSIBLE_STATION)
                {
                    modes.Add(ModeCapability.Managed);
                }

                if ((modeCapability.OpModeCapability & NativeWin.DOT11_OPERATION_MODE_NETWORK_MONITOR) == NativeWin.DOT11_OPERATION_MODE_NETWORK_MONITOR)
                {
                    modes.Add(ModeCapability.Monitor);
                }

                return modes.ToArray();
            }
        }

        private string              _guid;
        private IntPtr              _pLpAdapter;
        private NativeWin.LPADAPTER _lpAdapter;

        public PacketSharp(string guid)
        {
            _guid       = guid;
            _           = NativeWin.PacketGetMonitorMode(AdapterGuidToNPCAPString(_guid));
            _pLpAdapter = NativeWin.PacketOpenAdapter(AdapterGuidToNPCAPString(_guid));
            _lpAdapter  = Marshal.PtrToStructure<NativeWin.LPADAPTER>(_pLpAdapter);

            _channel = MakeOIDRequest<uint>(_lpAdapter, NativeWin.OID_DOT11_CURRENT_CHANNEL, false, 0);
        }

        private string AdapterGuidToNPCAPString(string guid)
        {
            return $"\\Device\\NPF_{{{guid}}}";
        }

        private T MakeOIDRequest<T>(NativeWin.LPADAPTER lpAdapter, uint oidIdentifier, bool set, T data) where T : unmanaged
        {
            Span<byte> tempBuffer = new byte[Marshal.SizeOf<NativeWin.PacketOidData>() + Marshal.SizeOf<T>()];

            ref NativeWin.PacketOidData packetOidData = ref MemoryMarshal.Cast<byte, NativeWin.PacketOidData>(tempBuffer[..Marshal.SizeOf<NativeWin.PacketOidData>()])[0];

            packetOidData.Oid = oidIdentifier;
            packetOidData.Length = (uint)Marshal.SizeOf<T>();

            if (set)
            {
                MemoryMarshal.Cast<byte, T>(tempBuffer.Slice(Marshal.SizeOf<NativeWin.PacketOidData>(), Marshal.SizeOf<T>()))[0] = data;
            }

            if (NativeWin.PacketRequest(ref lpAdapter, set, ref packetOidData))
            {
                return MemoryMarshal.Cast<byte, T>(tempBuffer[Marshal.SizeOf<NativeWin.PacketOidData>()..])[0];
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
                NativeWin.PacketCloseAdapter(_pLpAdapter);
                _pLpAdapter = IntPtr.Zero;
            }
        }
    }
}