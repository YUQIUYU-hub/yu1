using System;
using System.Collections.Generic;

namespace MemoryHook.Core.Models
{
    /// <summary>
    /// 内存搜索结果
    /// </summary>
    public class MemorySearchResult
    {
        /// <summary>
        /// 找到的地址
        /// </summary>
        public IntPtr Address { get; set; }

        /// <summary>
        /// 匹配的值
        /// </summary>
        public MemoryValue Value { get; set; } = new();

        /// <summary>
        /// 内存区域信息
        /// </summary>
        public MemoryRegionInfo RegionInfo { get; set; } = new();

        /// <summary>
        /// 地址的十六进制表示
        /// </summary>
        public string AddressHex => $"0x{Address.ToInt64():X}";

        /// <summary>
        /// 匹配的值字符串
        /// </summary>
        public string ValueString => Value.ValueAsString;
    }

    /// <summary>
    /// 内存搜索参数
    /// </summary>
    public class MemorySearchParams
    {
        /// <summary>
        /// 搜索值
        /// </summary>
        public string SearchValue { get; set; } = string.Empty;

        /// <summary>
        /// 数据类型
        /// </summary>
        public DataType DataType { get; set; }

        /// <summary>
        /// 搜索范围起始地址
        /// </summary>
        public IntPtr StartAddress { get; set; }

        /// <summary>
        /// 搜索范围结束地址
        /// </summary>
        public IntPtr EndAddress { get; set; }

        /// <summary>
        /// 是否区分大小写 (仅对字符串有效)
        /// </summary>
        public bool CaseSensitive { get; set; } = true;

        /// <summary>
        /// 最大结果数量
        /// </summary>
        public int MaxResults { get; set; } = 1000;

        /// <summary>
        /// 内存保护类型过滤
        /// </summary>
        public MemoryProtection ProtectionFilter { get; set; } = MemoryProtection.All;

        /// <summary>
        /// 是否搜索只读内存
        /// </summary>
        public bool IncludeReadOnly { get; set; } = true;

        /// <summary>
        /// 是否搜索可执行内存
        /// </summary>
        public bool IncludeExecutable { get; set; } = false;

        /// <summary>
        /// 对齐要求 (字节)
        /// </summary>
        public int Alignment { get; set; } = 1;
    }

    /// <summary>
    /// 内存区域信息
    /// </summary>
    public class MemoryRegionInfo
    {
        /// <summary>
        /// 区域起始地址
        /// </summary>
        public IntPtr BaseAddress { get; set; }

        /// <summary>
        /// 区域大小
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// 内存保护类型
        /// </summary>
        public MemoryProtection Protection { get; set; }

        /// <summary>
        /// 内存状态
        /// </summary>
        public MemoryState State { get; set; }

        /// <summary>
        /// 内存类型
        /// </summary>
        public MemoryType Type { get; set; }

        /// <summary>
        /// 模块名称 (如果属于某个模块)
        /// </summary>
        public string ModuleName { get; set; } = string.Empty;

        /// <summary>
        /// 区域结束地址
        /// </summary>
        public IntPtr EndAddress => new(BaseAddress.ToInt64() + Size);

        /// <summary>
        /// 区域描述
        /// </summary>
        public string Description
        {
            get
            {
                var desc = new List<string>();
                
                if (!string.IsNullOrEmpty(ModuleName))
                    desc.Add($"模块: {ModuleName}");
                
                desc.Add($"保护: {GetProtectionString()}");
                desc.Add($"状态: {GetStateString()}");
                desc.Add($"类型: {GetTypeString()}");
                
                return string.Join(", ", desc);
            }
        }

        private string GetProtectionString()
        {
            return Protection switch
            {
                MemoryProtection.NoAccess => "无访问",
                MemoryProtection.ReadOnly => "只读",
                MemoryProtection.ReadWrite => "读写",
                MemoryProtection.WriteCopy => "写时复制",
                MemoryProtection.Execute => "执行",
                MemoryProtection.ExecuteRead => "执行读",
                MemoryProtection.ExecuteReadWrite => "执行读写",
                MemoryProtection.ExecuteWriteCopy => "执行写时复制",
                _ => "未知"
            };
        }

        private string GetStateString()
        {
            return State switch
            {
                MemoryState.Commit => "已提交",
                MemoryState.Free => "空闲",
                MemoryState.Reserve => "已保留",
                _ => "未知"
            };
        }

        private string GetTypeString()
        {
            return Type switch
            {
                MemoryType.Image => "映像",
                MemoryType.Mapped => "映射",
                MemoryType.Private => "私有",
                _ => "未知"
            };
        }
    }

    /// <summary>
    /// 内存保护类型
    /// </summary>
    [Flags]
    public enum MemoryProtection : uint
    {
        NoAccess = 0x01,
        ReadOnly = 0x02,
        ReadWrite = 0x04,
        WriteCopy = 0x08,
        Execute = 0x10,
        ExecuteRead = 0x20,
        ExecuteReadWrite = 0x40,
        ExecuteWriteCopy = 0x80,
        All = 0xFF
    }

    /// <summary>
    /// 内存状态
    /// </summary>
    public enum MemoryState : uint
    {
        Commit = 0x1000,
        Free = 0x10000,
        Reserve = 0x2000
    }

    /// <summary>
    /// 内存类型
    /// </summary>
    public enum MemoryType : uint
    {
        Image = 0x1000000,
        Mapped = 0x40000,
        Private = 0x20000
    }
}
