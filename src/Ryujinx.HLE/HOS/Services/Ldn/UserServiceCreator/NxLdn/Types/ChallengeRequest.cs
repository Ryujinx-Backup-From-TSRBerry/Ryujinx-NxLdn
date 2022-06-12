using PacketDotNet.Utils.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.Types
{
    // Size: 0x2D0
    // Little endian
    public class ChallengeRequest
    {
        private static readonly byte[] HmacKey = new byte[] { 0xf8, 0x4b, 0x48, 0x7f, 0xb3, 0x72, 0x51, 0xc2, 0x63, 0xbf, 0x11, 0x60, 0x90, 0x36, 0x58, 0x92, 0x66, 0xaf, 0x70, 0xca, 0x79, 0xb4, 0x4c, 0x93, 0xc7, 0x37, 0x0c, 0x57, 0x69, 0xc0, 0xf6, 0x02 };

        private byte[] _data;

        public ChallengeRequest()
        {
            _data = new byte[0x2D0];
        }

        public ChallengeRequest(ChallengeRequestParameter challenge)
        {
            // https://github.com/kinnay/LDN/blob/15ab244703eb949be9d7b24da95a26336308c8e9/ldn/__init__.py#L331
            if (HMACSHA256.HashData(HmacKey, challenge.Body) != challenge.Hmac)
            {
                throw new Exception("Challenge request has wrong HMAC");
            }
            _data = challenge.Body;
        }

        private byte N1
        {
            get => _data.Skip(2).First();
            set => _data[2] = value;
        }

        private byte N2
        {
            get => _data.Skip(3).First();
            set => _data[3] = value;
        }

        private byte DebugCheck
        {
            get => _data.Skip(4).First();
            set => _data[4] = value;
        }

        public ulong Token
        {
            get => EndianBitConverter.Little.ToUInt64(_data, 8);
            set => EndianBitConverter.Little.CopyBytes(value, _data, 8);
        }

        public ulong Nonce
        {
            get => EndianBitConverter.Little.ToUInt64(_data, 16);
            set => EndianBitConverter.Little.CopyBytes(value, _data, 16);
        }

        public ulong DeviceId
        {
            get => EndianBitConverter.Little.ToUInt64(_data, 24);
            set => EndianBitConverter.Little.CopyBytes(value, _data, 24);
        }

        public ulong[] Params1
        {
            get
            {
                List<ulong> params1 = new List<ulong>(8);
                for (int i = 0; i < 8; i++)
                {
                    params1.Add(EndianBitConverter.Little.ToUInt64(_data, 144 + (8 * i)));
                }
                return params1.Take(N1).ToArray();
            }
            set
            {
                // TODO: This is not optimal
                for (int i = 0; i < 8; i++)
                {
                    EndianBitConverter.Little.CopyBytes(value[i], _data, 144 + (8 * i));
                }
            }
        }

        public ulong[] Params2
        {
            get
            {
                List<ulong> params2 = new List<ulong>(8);
                for (int i = 0; i < 8; i++)
                {
                    params2.Add(EndianBitConverter.Little.ToUInt64(_data, 208 + (8 * i)));
                }
                return params2.Take(N2).ToArray();
            }
            set
            {
                // TODO: This is not optimal
                for (int i = 0; i < 8; i++)
                {
                    EndianBitConverter.Little.CopyBytes(value[i], _data, 208 + (8 * i));
                }
            }
        }

        public ChallengeRequestParameter Encode()
        {
            return new ChallengeRequestParameter()
            {
                Hmac = HMACSHA256.HashData(HmacKey, _data),
                Body = _data
            };
        }
    }
}
