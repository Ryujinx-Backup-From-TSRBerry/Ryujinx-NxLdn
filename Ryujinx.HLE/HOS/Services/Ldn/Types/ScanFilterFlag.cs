namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    internal enum ScanFilterFlag
    {
        LocalCommunicationId = 1,
        SessionId = 2,
        NetworkType = 4,
        MacAddress = 8,
        Ssid = 0x10,
        SceneId = 0x20,
        IntentId = 33,
        NetworkId = 35,
        All = 0x3F
    }
}
