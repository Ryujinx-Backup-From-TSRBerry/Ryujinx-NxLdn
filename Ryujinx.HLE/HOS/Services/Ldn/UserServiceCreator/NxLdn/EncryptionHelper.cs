using LibHac.Common.Keys;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using System;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn
{
    public static class EncryptionHelper
    {
        private static KeySet _keySet;

        public static void Initialize(KeySet keySet)
        {
            EncryptionHelper._keySet = keySet;
        }

        // TODO: Remove debug stuff
        // private static void LogMsg(string msg, object obj = null)
        // {
        //     if (obj != null)
        //     {
        //         string jsonString = JsonHelper.Serialize<object>(obj, true);
        //         Logger.Info?.PrintMsg(LogClass.ServiceLdn, msg + "\n" + jsonString);
        //     }
        //     else
        //     {
        //         Logger.Info?.PrintMsg(LogClass.ServiceLdn, msg);
        //     }
        // }

        private static Span<byte> DecryptKey(Span<byte> input, Span<byte> key)
        {
            Span<byte> output = new Span<byte>(new byte[0x10]);
            // LogMsg($"DecryptKey: Input length: {input.Length}");
            LibHac.Crypto.Aes.DecryptEcb128(input, output, key);
            return output;
        }

        public static byte[] DeriveKey(byte[] input, byte[] source)
        {
            if (_keySet is null)
            {
                throw new InvalidOperationException("EncryptionHelper was not initialized.");
            }

            Span<byte> key = DecryptKey(_keySet.AesKekGenerationSource, _keySet.MasterKeys[0]);
            // LogMsg("Decrypt Key1: ", key.ToArray());
            key = DecryptKey(source, key);
            key = DecryptKey(_keySet.AesKeyGenerationSource, key);
            // LogMsg("Decrypt Key3: ", key.ToArray());
            Span<byte> hash = new Span<byte>(new byte[0x20]);
            // LogMsg($"Input: ", input);
            LibHac.Crypto.Sha256.GenerateSha256Hash(input, hash);
            // LogMsg("Sha256: ", hash.ToArray());
            return DecryptKey(hash.Slice(0, 16), key).ToArray();
        }
    }
}
