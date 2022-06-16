namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    internal enum DisconnectReason
    {
        None,
        DisconnectedByUser,
        DisconnectedBySystem,
        DestroyedByUser,
        DestroyedBySystem,
        Rejected,
        SignalLost
    }
}
