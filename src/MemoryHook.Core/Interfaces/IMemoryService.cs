using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemoryHook.Core.Models;

namespace MemoryHook.Core.Interfaces
{
    /// <summary>
    /// 内存操作服务接口
    /// </summary>
    public interface IMemoryService
    {
        /// <summary>
        /// 当前目标进程
        /// </summary>
        ProcessInfo? TargetProcess { get; }

        /// <summary>
        /// 是否已连接到目标进程
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接到目标进程
        /// </summary>
        /// <param name="processInfo">进程信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接结果</returns>
        Task<MemoryOperationResult> ConnectToProcessAsync(ProcessInfo processInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// 断开与目标进程的连接
        /// </summary>
        /// <returns>断开结果</returns>
        Task<MemoryOperationResult> DisconnectAsync();

        /// <summary>
        /// 读取内存值
        /// </summary>
        /// <param name="address">内存地址</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="size">读取大小 (对于可变长度类型)</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>内存值</returns>
        Task<MemoryOperationResult<MemoryValue>> ReadMemoryAsync(IntPtr address, DataType dataType, int size = 0, CancellationToken cancellationToken = default);

        /// <summary>
        /// 写入内存值
        /// </summary>
        /// <param name="memoryValue">要写入的内存值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>写入结果</returns>
        Task<MemoryOperationResult> WriteMemoryAsync(MemoryValue memoryValue, CancellationToken cancellationToken = default);

        /// <summary>
        /// 搜索内存
        /// </summary>
        /// <param name="searchParams">搜索参数</param>
        /// <param name="progress">进度报告</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>搜索结果</returns>
        Task<MemoryOperationResult<List<MemorySearchResult>>> SearchMemoryAsync(MemorySearchParams searchParams, IProgress<MemorySearchProgress>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取内存区域信息
        /// </summary>
        /// <param name="address">内存地址</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>内存区域信息</returns>
        Task<MemoryOperationResult<MemoryRegionInfo>> GetMemoryRegionInfoAsync(IntPtr address, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取所有内存区域
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>内存区域列表</returns>
        Task<MemoryOperationResult<List<MemoryRegionInfo>>> GetMemoryRegionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 备份内存值
        /// </summary>
        /// <param name="address">内存地址</param>
        /// <param name="size">备份大小</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>备份结果</returns>
        Task<MemoryOperationResult<MemoryBackup>> BackupMemoryAsync(IntPtr address, int size, CancellationToken cancellationToken = default);

        /// <summary>
        /// 恢复内存值
        /// </summary>
        /// <param name="backup">内存备份</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>恢复结果</returns>
        Task<MemoryOperationResult> RestoreMemoryAsync(MemoryBackup backup, CancellationToken cancellationToken = default);

        /// <summary>
        /// 内存值变化事件
        /// </summary>
        event EventHandler<MemoryValueChangedEventArgs>? MemoryValueChanged;

        /// <summary>
        /// 进程连接状态变化事件
        /// </summary>
        event EventHandler<ProcessConnectionChangedEventArgs>? ProcessConnectionChanged;
    }

    /// <summary>
    /// 内存操作结果
    /// </summary>
    public class MemoryOperationResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 错误代码
        /// </summary>
        public int ErrorCode { get; set; }

        /// <summary>
        /// 操作耗时
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        /// <returns>成功结果</returns>
        public static MemoryOperationResult CreateSuccess()
        {
            return new MemoryOperationResult { Success = true };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="errorCode">错误代码</param>
        /// <returns>失败结果</returns>
        public static MemoryOperationResult CreateFailure(string errorMessage, int errorCode = 0)
        {
            return new MemoryOperationResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }

    /// <summary>
    /// 带数据的内存操作结果
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public class MemoryOperationResult<T> : MemoryOperationResult
    {
        /// <summary>
        /// 结果数据
        /// </summary>
        public T? Data { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        /// <param name="data">结果数据</param>
        /// <returns>成功结果</returns>
        public static MemoryOperationResult<T> CreateSuccess(T data)
        {
            return new MemoryOperationResult<T>
            {
                Success = true,
                Data = data
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="errorCode">错误代码</param>
        /// <returns>失败结果</returns>
        public static new MemoryOperationResult<T> CreateFailure(string errorMessage, int errorCode = 0)
        {
            return new MemoryOperationResult<T>
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }

    /// <summary>
    /// 内存搜索进度
    /// </summary>
    public class MemorySearchProgress
    {
        /// <summary>
        /// 当前进度百分比 (0-100)
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// 当前搜索地址
        /// </summary>
        public IntPtr CurrentAddress { get; set; }

        /// <summary>
        /// 已找到的结果数量
        /// </summary>
        public int ResultsFound { get; set; }

        /// <summary>
        /// 已搜索的内存区域数量
        /// </summary>
        public int RegionsSearched { get; set; }

        /// <summary>
        /// 总内存区域数量
        /// </summary>
        public int TotalRegions { get; set; }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// 内存备份
    /// </summary>
    public class MemoryBackup
    {
        /// <summary>
        /// 备份地址
        /// </summary>
        public IntPtr Address { get; set; }

        /// <summary>
        /// 备份数据
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// 备份时间
        /// </summary>
        public DateTime BackupTime { get; set; }

        /// <summary>
        /// 备份描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// 内存值变化事件参数
    /// </summary>
    public class MemoryValueChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 变化的地址
        /// </summary>
        public IntPtr Address { get; set; }

        /// <summary>
        /// 旧值
        /// </summary>
        public MemoryValue? OldValue { get; set; }

        /// <summary>
        /// 新值
        /// </summary>
        public MemoryValue? NewValue { get; set; }
    }

    /// <summary>
    /// 进程连接状态变化事件参数
    /// </summary>
    public class ProcessConnectionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// 进程信息
        /// </summary>
        public ProcessInfo? ProcessInfo { get; set; }

        /// <summary>
        /// 错误消息 (如果连接失败)
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
