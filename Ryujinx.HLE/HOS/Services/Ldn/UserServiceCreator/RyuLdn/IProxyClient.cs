namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn
{
    internal interface IProxyClient
    {
        bool SendAsync(byte[] buffer);
    }
}
