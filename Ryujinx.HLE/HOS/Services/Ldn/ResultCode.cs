namespace Ryujinx.HLE.HOS.Services.Ldn
{
    enum ResultCode
    {
        ModuleId = 203,
        ErrorCodeShift = 9,
        Success = 0,
        DeviceNotAvailable = 8395,
        DeviceDisabled = 11467,
        InvalidState = 16587,
        NodeNotFound = 24779,
        ConnectFailure = 32971,
        ConnectNotFound = 33483,
        ConnectTimeout = 33995,
        ConnectRejected = 34507,
        InvalidArgument = 49355,
        InvalidObject = 49867,
        VersionTooLow = 58059,
        VersionTooHigh = 58571,
        TooManyPlayers = 73931
    }
}
