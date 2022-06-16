namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types
{
    internal enum PacketId
    {
        Initialize = 0,
        Passphrase = 1,
        CreateAccessPoint = 2,
        CreateAccessPointPrivate = 3,
        ExternalProxy = 4,
        ExternalProxyToken = 5,
        ExternalProxyState = 6,
        SyncNetwork = 7,
        Reject = 8,
        RejectReply = 9,
        Scan = 10,
        ScanReply = 11,
        ScanReplyEnd = 12,
        Connect = 13,
        ConnectPrivate = 14,
        Connected = 0xF,
        Disconnect = 0x10,
        ProxyConfig = 17,
        ProxyConnect = 18,
        ProxyConnectReply = 19,
        ProxyData = 20,
        ProxyDisconnect = 21,
        SetAcceptPolicy = 22,
        SetAdvertiseData = 23,
        Ping = 254,
        NetworkError = 0xFF
    }
}
