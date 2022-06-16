namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    internal enum NetworkError
    {
        None = 0,
        PortUnreachable = 1,
        TooManyPlayers = 2,
        VersionTooLow = 3,
        VersionTooHigh = 4,
        ConnectFailure = 5,
        ConnectNotFound = 6,
        ConnectTimeout = 7,
        ConnectRejected = 8,
        RejectFailed = 9,
        Unknown = -1
    }
}
