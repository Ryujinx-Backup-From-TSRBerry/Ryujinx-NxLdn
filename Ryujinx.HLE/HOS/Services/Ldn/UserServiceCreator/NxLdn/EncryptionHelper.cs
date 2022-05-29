using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using System;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn
{
    public static class EncryptionHelper {
        private static readonly byte[] AesKeyGenerationSource1 = { 0x4d, 0x87, 0x09, 0x86, 0xc4, 0x5d, 0x20, 0x72, 0x2f, 0xba, 0x10, 0x53, 0xda, 0x92, 0xe8, 0xa9 };
        private static readonly byte[] AesKeyGenerationSource2 = { 0x89, 0x61, 0x5e, 0xe0, 0x5c, 0x31, 0xb6, 0x80, 0x5f, 0xe5, 0x8f, 0x3d, 0xa2, 0x4f, 0x7a, 0xa8 };

        private static readonly byte[] MasterKey = { 0xc2, 0xca, 0xaf, 0xf0, 0x89, 0xb9, 0xae, 0xd5, 0x56, 0x94, 0x87, 0x60, 0x55, 0x27, 0x1c, 0x7d };

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

        private static Span<byte> DecryptKey(Span<byte> input, Span<byte> key) {
            Span<byte> output = new Span<byte>(new byte[0x10]);
            // LogMsg($"DecryptKey: Input length: {input.Length}");
            LibHac.Crypto.Aes.DecryptEcb128(input, output, key);
            return output;
        }

        public static byte[] DeriveKey(byte[] input, byte[] source) {
            Span<byte> key = DecryptKey(AesKeyGenerationSource1, MasterKey);
            // LogMsg("Decrypt Key1: ", key.ToArray());
            key = DecryptKey(source, key);
            key = DecryptKey(AesKeyGenerationSource2, key);
            // LogMsg("Decrypt Key3: ", key.ToArray());
            Span<byte> hash = new Span<byte>(new byte[0x20]);
            // LogMsg($"Input: ", input);
            LibHac.Crypto.Sha256.GenerateSha256Hash(input, hash);
            // LogMsg("Sha256: ", hash.ToArray());
            return DecryptKey(hash.Slice(0, 16), key).ToArray();
        }
    }
}
