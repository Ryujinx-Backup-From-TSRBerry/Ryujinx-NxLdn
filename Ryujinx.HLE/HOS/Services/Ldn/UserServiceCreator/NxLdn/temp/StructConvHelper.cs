using PacketDotNet.Utils.Converters;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn.temp {
    // Source: https://stackoverflow.com/a/2624377
    public static class StructConvHelper {
        // TODO: Remove debug stuff
        private static void LogMsg(string msg, object obj = null)
        {
            if (obj != null)
            {
                string jsonString = JsonHelper.Serialize<object>(obj, true);
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, "AdvertisementFrame: " + msg + "\n" + jsonString);
            }
            else
            {
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, "AdvertisementFrame: " + msg);
            }
        }

        private static void RespectEndiannessArray(Endianness endianness, int arrayOffset, FieldInfo arrayField, byte[] data) {
            // FIXME: I gave up on solving this issue, so I just set the size if I know the type atm
            int arrayLength = arrayField.FieldType.GetElementType() == typeof(Ryujinx.HLE.HOS.Services.Ldn.Types.NodeInfo) ? 8 : arrayField.FieldType.StructLayoutAttribute.Size;
            Type elementType = arrayField.FieldType.GetElementType();
            int elementSize = elementType.StructLayoutAttribute.Size;
            LogMsg($"arrayLength: {arrayLength} elementSize: {elementSize} elementType: {elementType}");
            for (int i = 0; i < arrayLength; i++)
            {
                var fields = elementType.GetFields().Where(f => f.FieldType != typeof(byte[])).Select(
                    f => new {
                        Field = f,
                        Offset = arrayOffset + (elementSize * i) + Marshal.OffsetOf(elementType, f.Name).ToInt32()
                    }
                ).ToList();

                foreach (var field in fields)
                {
                    if ((endianness == Endianness.BigEndian && BitConverter.IsLittleEndian) ||
                        (endianness == Endianness.LittleEndian && !BitConverter.IsLittleEndian))
                    {
                        if (field.Field.FieldType.IsArray) {
                            RespectEndiannessArray(endianness, field.Offset, field.Field, data);
                        }
                        else {
                            Array.Reverse(data, field.Offset, Marshal.SizeOf(field.Field.FieldType));
                        }
                    }
                }
            }
        }

        private static void RespectEndianness(Endianness endianness, Type type, byte[] data)
        {
            var fields = type.GetFields().Where(f => f.FieldType != typeof(byte[])).Select(
                f => new {
                    Field = f,
                    Offset = Marshal.OffsetOf(type, f.Name).ToInt32()
                }
            ).ToList();

            foreach (var field in fields)
            {
                if ((endianness == Endianness.BigEndian && BitConverter.IsLittleEndian) ||
                    (endianness == Endianness.LittleEndian && !BitConverter.IsLittleEndian))
                {
                    if (field.Field.FieldType.IsArray) {
                        RespectEndiannessArray(endianness, field.Offset, field.Field, data);
                    }
                    else {
                        Array.Reverse(data, field.Offset, Marshal.SizeOf(field.Field.FieldType));
                    }
                }
            }
        }

        public static T BytesToStruct<T>(Endianness endianness, byte[] rawData) where T : struct
        {
            T result = default(T);

            RespectEndianness(endianness, typeof(T), rawData);

            GCHandle handle = GCHandle.Alloc(rawData, GCHandleType.Pinned);

            try
            {
                IntPtr rawDataPtr = handle.AddrOfPinnedObject();
                result = (T)Marshal.PtrToStructure(rawDataPtr, typeof(T));
            }
            finally
            {
                handle.Free();
            }

            return result;
        }

        public static byte[] StructToBytes<T>(Endianness endianness, T data) where T : struct
        {
            byte[] rawData = new byte[Marshal.SizeOf(data)];
            GCHandle handle = GCHandle.Alloc(rawData, GCHandleType.Pinned);
            try
            {
                IntPtr rawDataPtr = handle.AddrOfPinnedObject();
                Marshal.StructureToPtr(data, rawDataPtr, false);
            }
            finally
            {
                handle.Free();
            }

            RespectEndianness(endianness, typeof(T), rawData);

            return rawData;
        }
    }
}
