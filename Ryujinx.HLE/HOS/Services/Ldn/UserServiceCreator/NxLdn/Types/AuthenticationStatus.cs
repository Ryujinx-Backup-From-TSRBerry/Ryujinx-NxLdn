namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types
{
    internal enum AuthenticationStatus : byte
    {
        Success,
        DeniedByPolicy,
        MalformedAuthRequest,
        InvalidVersion,
        UnexpectedRequest,
        InvalidChallenge
    }
}
