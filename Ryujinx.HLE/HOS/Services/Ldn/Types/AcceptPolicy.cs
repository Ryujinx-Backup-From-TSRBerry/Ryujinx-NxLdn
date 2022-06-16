namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    internal enum AcceptPolicy : byte
    {
        AcceptAll,
        RejectAll,
        BlackList,
        WhiteList
    }
}
