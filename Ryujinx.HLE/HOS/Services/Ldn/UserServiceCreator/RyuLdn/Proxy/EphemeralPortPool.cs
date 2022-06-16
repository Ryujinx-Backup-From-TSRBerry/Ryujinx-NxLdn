using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Proxy
{
    public class EphemeralPortPool
    {
        private const ushort EphemeralBase = 49152;

        private List<ushort> _ephemeralPorts = new List<ushort>();

        private object _lock = new object();

        public ushort Get()
        {
            ushort port = 49152;
            lock (_lock)
            {
                for (int i = 0; i < _ephemeralPorts.Count; i++)
                {
                    if (_ephemeralPorts[i] > port)
                    {
                        _ephemeralPorts.Insert(i, port);
                        return port;
                    }
                    port = (ushort)(port + 1);
                }
                if (port != 0)
                {
                    _ephemeralPorts.Add(port);
                }
                return port;
            }
        }

        public void Return(ushort port)
        {
            lock (_lock)
            {
                _ephemeralPorts.Remove(port);
            }
        }
    }
}
