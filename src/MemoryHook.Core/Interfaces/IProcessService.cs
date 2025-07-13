using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemoryHook.Core.Models;

namespace MemoryHook.Core.Interfaces
{
    /// <summary>
    /// 进程管理服务接口
    /// </summary>
    public interface IProcessService
    {
        /// <summary>
        /// 获取所有运行中的进程
        /// </summary>
        /// <param name="includeSystemProcesses">是否包含系统进程</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>进程列表</returns>
        Task<List<ProcessInfo>> GetRunningProcessesAsync(bool includeSystemProcesses = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// 根据进程ID获取进程信息
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>进程信息</returns>
        Task<ProcessInfo?> GetProcessByIdAsync(int processId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 根据进程名称获取进程列表
        /// </summary>
        /// <param name="processName">进程名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>进程列表</returns>
        Task<List<ProcessInfo>> GetProcessesByNameAsync(string processName, CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查进程是否存在
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在</returns>
        Task<bool> IsProcessRunningAsync(int processId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取进程架构
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>进程架构</returns>
        Task<ProcessArchitecture> GetProcessArchitectureAsync(int processId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查是否可以访问进程
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>访问检查结果</returns>
        Task<ProcessAccessResult> CheckProcessAccessAsync(int processId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查进程是否为保护进程
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否为保护进程</returns>
        Task<bool> IsProtectedProcessAsync(int processId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取进程模块列表
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>模块列表</returns>
        Task<List<ProcessModuleInfo>> GetProcessModulesAsync(int processId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 刷新进程列表
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>刷新后的进程列表</returns>
        Task<List<ProcessInfo>> RefreshProcessListAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 进程列表变化事件
        /// </summary>
        event EventHandler<ProcessListChangedEventArgs>? ProcessListChanged;

        /// <summary>
        /// 进程状态变化事件
        /// </summary>
        event EventHandler<ProcessStatusChangedEventArgs>? ProcessStatusChanged;
    }

    /// <summary>
    /// 进程访问结果
    /// </summary>
    public class ProcessAccessResult
    {
        /// <summary>
        /// 是否可以访问
        /// </summary>
        public bool CanAccess { get; set; }

        /// <summary>
        /// 访问级别
        /// </summary>
        public ProcessAccessLevel AccessLevel { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 需要的权限
        /// </summary>
        public string RequiredPrivileges { get; set; } = string.Empty;

        /// <summary>
        /// 是否需要管理员权限
        /// </summary>
        public bool RequiresAdminRights { get; set; }
    }

    /// <summary>
    /// 进程访问级别
    /// </summary>
    public enum ProcessAccessLevel
    {
        /// <summary>
        /// 无访问权限
        /// </summary>
        None,

        /// <summary>
        /// 基本信息访问
        /// </summary>
        BasicInfo,

        /// <summary>
        /// 内存读取
        /// </summary>
        MemoryRead,

        /// <summary>
        /// 内存写入
        /// </summary>
        MemoryWrite,

        /// <summary>
        /// 完全访问
        /// </summary>
        FullAccess
    }

    /// <summary>
    /// 进程模块信息
    /// </summary>
    public class ProcessModuleInfo
    {
        /// <summary>
        /// 模块名称
        /// </summary>
        public string ModuleName { get; set; } = string.Empty;

        /// <summary>
        /// 模块文件路径
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// 基地址
        /// </summary>
        public IntPtr BaseAddress { get; set; }

        /// <summary>
        /// 模块大小
        /// </summary>
        public int ModuleMemorySize { get; set; }

        /// <summary>
        /// 入口点地址
        /// </summary>
        public IntPtr EntryPointAddress { get; set; }

        /// <summary>
        /// 文件版本信息
        /// </summary>
        public string FileVersionInfo { get; set; } = string.Empty;

        /// <summary>
        /// 是否为主模块
        /// </summary>
        public bool IsMainModule { get; set; }

        /// <summary>
        /// 结束地址
        /// </summary>
        public IntPtr EndAddress => new(BaseAddress.ToInt64() + ModuleMemorySize);

        /// <summary>
        /// 基地址的十六进制表示
        /// </summary>
        public string BaseAddressHex => $"0x{BaseAddress.ToInt64():X}";

        /// <summary>
        /// 模块大小的格式化字符串
        /// </summary>
        public string SizeFormatted => FormatBytes(ModuleMemorySize);

        /// <summary>
        /// 格式化字节数
        /// </summary>
        /// <param name="bytes">字节数</param>
        /// <returns>格式化后的字符串</returns>
        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }

    /// <summary>
    /// 进程列表变化事件参数
    /// </summary>
    public class ProcessListChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 新增的进程
        /// </summary>
        public List<ProcessInfo> AddedProcesses { get; set; } = new();

        /// <summary>
        /// 移除的进程
        /// </summary>
        public List<ProcessInfo> RemovedProcesses { get; set; } = new();

        /// <summary>
        /// 更新的进程
        /// </summary>
        public List<ProcessInfo> UpdatedProcesses { get; set; } = new();
    }

    /// <summary>
    /// 进程状态变化事件参数
    /// </summary>
    public class ProcessStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 进程信息
        /// </summary>
        public ProcessInfo ProcessInfo { get; set; } = new();

        /// <summary>
        /// 状态变化类型
        /// </summary>
        public ProcessStatusChangeType ChangeType { get; set; }

        /// <summary>
        /// 变化时间
        /// </summary>
        public DateTime ChangeTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 进程状态变化类型
    /// </summary>
    public enum ProcessStatusChangeType
    {
        /// <summary>
        /// 进程启动
        /// </summary>
        Started,

        /// <summary>
        /// 进程退出
        /// </summary>
        Exited,

        /// <summary>
        /// 进程信息更新
        /// </summary>
        Updated,

        /// <summary>
        /// 访问权限变化
        /// </summary>
        AccessChanged
    }
}
