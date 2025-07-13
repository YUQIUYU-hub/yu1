using System;
using System.Text;

namespace MemoryHook.Core.Models
{
    /// <summary>
    /// 内存值模型
    /// </summary>
    public class MemoryValue
    {
        /// <summary>
        /// 内存地址
        /// </summary>
        public IntPtr Address { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public DataType Type { get; set; }

        /// <summary>
        /// 原始字节数据
        /// </summary>
        public byte[] RawData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// 数据大小
        /// </summary>
        public int Size => RawData.Length;

        /// <summary>
        /// 地址的十六进制表示
        /// </summary>
        public string AddressHex => $"0x{Address.ToInt64():X}";

        /// <summary>
        /// 获取值作为字符串
        /// </summary>
        public string ValueAsString
        {
            get
            {
                if (RawData.Length == 0) return string.Empty;

                return Type switch
                {
                    DataType.Int8 => ((sbyte)RawData[0]).ToString(),
                    DataType.UInt8 => RawData[0].ToString(),
                    DataType.Int16 => BitConverter.ToInt16(RawData, 0).ToString(),
                    DataType.UInt16 => BitConverter.ToUInt16(RawData, 0).ToString(),
                    DataType.Int32 => BitConverter.ToInt32(RawData, 0).ToString(),
                    DataType.UInt32 => BitConverter.ToUInt32(RawData, 0).ToString(),
                    DataType.Int64 => BitConverter.ToInt64(RawData, 0).ToString(),
                    DataType.UInt64 => BitConverter.ToUInt64(RawData, 0).ToString(),
                    DataType.Float => BitConverter.ToSingle(RawData, 0).ToString("F6"),
                    DataType.Double => BitConverter.ToDouble(RawData, 0).ToString("F6"),
                    DataType.StringAscii => Encoding.ASCII.GetString(RawData).TrimEnd('\0'),
                    DataType.StringUnicode => Encoding.Unicode.GetString(RawData).TrimEnd('\0'),
                    DataType.StringUtf8 => Encoding.UTF8.GetString(RawData).TrimEnd('\0'),
                    DataType.ByteArray => BitConverter.ToString(RawData).Replace("-", " "),
                    _ => BitConverter.ToString(RawData).Replace("-", " ")
                };
            }
        }

        /// <summary>
        /// 设置值从字符串
        /// </summary>
        /// <param name="value">字符串值</param>
        public void SetValueFromString(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            try
            {
                RawData = Type switch
                {
                    DataType.Int8 => new[] { (byte)(sbyte.Parse(value)) },
                    DataType.UInt8 => new[] { byte.Parse(value) },
                    DataType.Int16 => BitConverter.GetBytes(short.Parse(value)),
                    DataType.UInt16 => BitConverter.GetBytes(ushort.Parse(value)),
                    DataType.Int32 => BitConverter.GetBytes(int.Parse(value)),
                    DataType.UInt32 => BitConverter.GetBytes(uint.Parse(value)),
                    DataType.Int64 => BitConverter.GetBytes(long.Parse(value)),
                    DataType.UInt64 => BitConverter.GetBytes(ulong.Parse(value)),
                    DataType.Float => BitConverter.GetBytes(float.Parse(value)),
                    DataType.Double => BitConverter.GetBytes(double.Parse(value)),
                    DataType.StringAscii => Encoding.ASCII.GetBytes(value + "\0"),
                    DataType.StringUnicode => Encoding.Unicode.GetBytes(value + "\0"),
                    DataType.StringUtf8 => Encoding.UTF8.GetBytes(value + "\0"),
                    DataType.ByteArray => ParseByteArray(value),
                    _ => Array.Empty<byte>()
                };
            }
            catch
            {
                // 解析失败时保持原值
            }
        }

        /// <summary>
        /// 解析字节数组字符串
        /// </summary>
        /// <param name="value">字节数组字符串 (如: "01 02 03" 或 "010203")</param>
        /// <returns>字节数组</returns>
        private static byte[] ParseByteArray(string value)
        {
            value = value.Replace(" ", "").Replace("-", "");
            if (value.Length % 2 != 0) return Array.Empty<byte>();

            var bytes = new byte[value.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        /// 获取数据类型的默认大小
        /// </summary>
        /// <param name="type">数据类型</param>
        /// <returns>字节大小</returns>
        public static int GetTypeSize(DataType type)
        {
            return type switch
            {
                DataType.Int8 or DataType.UInt8 => 1,
                DataType.Int16 or DataType.UInt16 => 2,
                DataType.Int32 or DataType.UInt32 or DataType.Float => 4,
                DataType.Int64 or DataType.UInt64 or DataType.Double => 8,
                _ => 0 // 字符串和字节数组大小可变
            };
        }

        /// <summary>
        /// 创建指定类型的空值
        /// </summary>
        /// <param name="address">内存地址</param>
        /// <param name="type">数据类型</param>
        /// <returns>MemoryValue实例</returns>
        public static MemoryValue CreateEmpty(IntPtr address, DataType type)
        {
            var size = GetTypeSize(type);
            return new MemoryValue
            {
                Address = address,
                Type = type,
                RawData = size > 0 ? new byte[size] : Array.Empty<byte>()
            };
        }
    }

    /// <summary>
    /// 数据类型枚举
    /// </summary>
    public enum DataType
    {
        /// <summary>
        /// 8位有符号整数
        /// </summary>
        Int8,

        /// <summary>
        /// 8位无符号整数
        /// </summary>
        UInt8,

        /// <summary>
        /// 16位有符号整数
        /// </summary>
        Int16,

        /// <summary>
        /// 16位无符号整数
        /// </summary>
        UInt16,

        /// <summary>
        /// 32位有符号整数
        /// </summary>
        Int32,

        /// <summary>
        /// 32位无符号整数
        /// </summary>
        UInt32,

        /// <summary>
        /// 64位有符号整数
        /// </summary>
        Int64,

        /// <summary>
        /// 64位无符号整数
        /// </summary>
        UInt64,

        /// <summary>
        /// 32位浮点数
        /// </summary>
        Float,

        /// <summary>
        /// 64位浮点数
        /// </summary>
        Double,

        /// <summary>
        /// ASCII字符串
        /// </summary>
        StringAscii,

        /// <summary>
        /// Unicode字符串
        /// </summary>
        StringUnicode,

        /// <summary>
        /// UTF-8字符串
        /// </summary>
        StringUtf8,

        /// <summary>
        /// 字节数组
        /// </summary>
        ByteArray
    }
}
