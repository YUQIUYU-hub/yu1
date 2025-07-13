using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MemoryHook.Core.Interfaces;
using MemoryHook.Core.Models;

namespace MemoryHook.Core.Services
{
    /// <summary>
    /// 进程管理服务实现
    /// </summary>
    public class ProcessService : IProcessService
    {
        private readonly ILogger<ProcessService> _logger;
        private readonly Timer _processMonitorTimer;
        private List<ProcessInfo> _cachedProcesses = new();
        private readonly object _lockObject = new();

        // Windows API 导入
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

        [DllImport("kernel32.dll")]
        private static extern bool IsWow64Process2(IntPtr hProcess, out ushort processMachine, out ushort nativeMachine);

        // 进程访问权限常量
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;

        public event EventHandler<ProcessListChangedEventArgs>? ProcessListChanged;
        public event EventHandler<ProcessStatusChangedEventArgs>? ProcessStatusChanged;

        public ProcessService(ILogger<ProcessService> logger)
        {
            _logger = logger;

            // 启动进程监控定时器，每10秒检查一次，延迟5秒开始
            _processMonitorTimer = new Timer(MonitorProcesses, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// 获取所有运行中的进程
        /// </summary>
        public async Task<List<ProcessInfo>> GetRunningProcessesAsync(bool includeSystemProcesses = false, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var processes = new List<ProcessInfo>();
                
                try
                {
                    var systemProcesses = Process.GetProcesses();
                    
                    foreach (var process in systemProcesses)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        try
                        {
                            // 检查进程是否仍然存在
                            if (process.HasExited)
                                continue;

                            var processInfo = ProcessInfo.FromProcess(process);

                            // 验证进程名称（FromProcess方法已经处理了空名称的情况）
                            if (string.IsNullOrEmpty(processInfo.ProcessName))
                            {
                                _logger.LogWarning("进程名称仍为空，使用后备名称: PID {ProcessId}", process.Id);
                                processInfo.ProcessName = $"未知进程_{process.Id}";
                            }

                            // 调试信息：记录进程名称
                            _logger.LogDebug("创建进程信息: {ProcessName} (PID: {ProcessId})",
                                processInfo.ProcessName, processInfo.ProcessId);

                            // 检查是否为系统进程
                            processInfo.IsSystemProcess = IsSystemProcess(process);

                            if (!includeSystemProcesses && processInfo.IsSystemProcess)
                                continue;

                            // 安全地获取进程架构
                            try
                            {
                                processInfo.Architecture = GetProcessArchitectureSync(process.Id);
                            }
                            catch
                            {
                                processInfo.Architecture = ProcessArchitecture.Unknown;
                            }

                            // 安全地检查访问权限
                            try
                            {
                                var accessResult = CheckProcessAccessSync(process.Id);
                                processInfo.CanAccess = accessResult.CanAccess;
                            }
                            catch
                            {
                                processInfo.CanAccess = false;
                            }

                            // 安全地检查是否为保护进程
                            try
                            {
                                processInfo.IsProtectedProcess = IsProtectedProcessSync(process.Id);
                            }
                            catch
                            {
                                processInfo.IsProtectedProcess = false;
                            }

                            processes.Add(processInfo);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "获取进程信息失败: PID {ProcessId}", process?.Id ?? -1);
                        }
                        finally
                        {
                            try
                            {
                                process?.Dispose();
                            }
                            catch
                            {
                                // 忽略Dispose异常
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "获取进程列表失败");
                }

                return processes.OrderBy(p => p.ProcessName).ToList();
            }, cancellationToken);
        }

        /// <summary>
        /// 根据进程ID获取进程信息
        /// </summary>
        public async Task<ProcessInfo?> GetProcessByIdAsync(int processId, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var process = Process.GetProcessById(processId);

                    // 检查进程是否仍然存在
                    if (process.HasExited)
                    {
                        _logger.LogDebug("进程已退出: PID {ProcessId}", processId);
                        return null;
                    }

                    var processInfo = ProcessInfo.FromProcess(process);

                    // 验证进程名称
                    if (string.IsNullOrEmpty(processInfo.ProcessName))
                    {
                        _logger.LogWarning("获取到的进程名称为空: PID {ProcessId}", processId);
                        processInfo.ProcessName = $"未知进程_{processId}";
                    }

                    processInfo.IsSystemProcess = IsSystemProcess(process);
                    processInfo.Architecture = GetProcessArchitectureSync(processId);

                    var accessResult = CheckProcessAccessSync(processId);
                    processInfo.CanAccess = accessResult.CanAccess;
                    processInfo.IsProtectedProcess = IsProtectedProcessSync(processId);

                    _logger.LogDebug("成功获取进程信息: {ProcessName} (PID: {ProcessId})",
                        processInfo.ProcessName, processId);

                    return processInfo;
                }
                catch (ArgumentException)
                {
                    // 进程不存在
                    _logger.LogDebug("进程不存在: PID {ProcessId}", processId);
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "获取进程信息失败: PID {ProcessId}", processId);
                    return null;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 根据进程名称获取进程列表
        /// </summary>
        public async Task<List<ProcessInfo>> GetProcessesByNameAsync(string processName, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var processes = new List<ProcessInfo>();
                
                try
                {
                    var systemProcesses = Process.GetProcessesByName(processName);
                    
                    foreach (var process in systemProcesses)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        try
                        {
                            // 检查进程是否仍然存在
                            if (process.HasExited)
                                continue;

                            var processInfo = ProcessInfo.FromProcess(process);

                            // 验证进程名称
                            if (string.IsNullOrEmpty(processInfo.ProcessName))
                            {
                                _logger.LogWarning("获取到的进程名称为空: PID {ProcessId}", process.Id);
                                processInfo.ProcessName = $"未知进程_{process.Id}";
                            }

                            processInfo.IsSystemProcess = IsSystemProcess(process);
                            processInfo.Architecture = GetProcessArchitectureSync(process.Id);

                            var accessResult = CheckProcessAccessSync(process.Id);
                            processInfo.CanAccess = accessResult.CanAccess;
                            processInfo.IsProtectedProcess = IsProtectedProcessSync(process.Id);

                            processes.Add(processInfo);

                            _logger.LogDebug("按名称获取进程信息: {ProcessName} (PID: {ProcessId})",
                                processInfo.ProcessName, process.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "获取进程信息失败: PID {ProcessId}", process.Id);
                        }
                        finally
                        {
                            process.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "根据名称获取进程失败: {ProcessName}", processName);
                }

                return processes;
            }, cancellationToken);
        }

        /// <summary>
        /// 检查进程是否存在
        /// </summary>
        public async Task<bool> IsProcessRunningAsync(int processId, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var process = Process.GetProcessById(processId);
                    return !process.HasExited;
                }
                catch
                {
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 获取进程架构
        /// </summary>
        public async Task<ProcessArchitecture> GetProcessArchitectureAsync(int processId, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => GetProcessArchitectureSync(processId), cancellationToken);
        }

        /// <summary>
        /// 检查是否可以访问进程
        /// </summary>
        public async Task<ProcessAccessResult> CheckProcessAccessAsync(int processId, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => CheckProcessAccessSync(processId), cancellationToken);
        }

        /// <summary>
        /// 检查进程是否为保护进程
        /// </summary>
        public async Task<bool> IsProtectedProcessAsync(int processId, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => IsProtectedProcessSync(processId), cancellationToken);
        }

        /// <summary>
        /// 获取进程模块列表
        /// </summary>
        public async Task<List<ProcessModuleInfo>> GetProcessModulesAsync(int processId, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var modules = new List<ProcessModuleInfo>();
                
                try
                {
                    using var process = Process.GetProcessById(processId);
                    
                    foreach (System.Diagnostics.ProcessModule module in process.Modules)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        try
                        {
                            var moduleInfo = new ProcessModuleInfo
                            {
                                ModuleName = module.ModuleName,
                                FileName = module.FileName,
                                BaseAddress = module.BaseAddress,
                                ModuleMemorySize = module.ModuleMemorySize,
                                EntryPointAddress = module.EntryPointAddress,
                                FileVersionInfo = module.FileVersionInfo?.ToString() ?? string.Empty,
                                IsMainModule = module == process.MainModule
                            };

                            modules.Add(moduleInfo);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "获取模块信息失败: {ModuleName}", module.ModuleName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "获取进程模块失败: PID {ProcessId}", processId);
                }

                return modules;
            }, cancellationToken);
        }

        /// <summary>
        /// 刷新进程列表
        /// </summary>
        public async Task<List<ProcessInfo>> RefreshProcessListAsync(CancellationToken cancellationToken = default)
        {
            var newProcesses = await GetRunningProcessesAsync(false, cancellationToken);
            
            lock (_lockObject)
            {
                var oldProcesses = _cachedProcesses;
                _cachedProcesses = newProcesses;
                
                // 检查变化并触发事件
                CheckProcessListChanges(oldProcesses, newProcesses);
            }
            
            return newProcesses;
        }

        #region 私有方法

        /// <summary>
        /// 同步获取进程架构
        /// </summary>
        private ProcessArchitecture GetProcessArchitectureSync(int processId)
        {
            var handle = OpenProcess(PROCESS_QUERY_INFORMATION, false, processId);
            if (handle == IntPtr.Zero)
                return ProcessArchitecture.Unknown;

            try
            {
                // 尝试使用新的 IsWow64Process2 API (Windows 10 1511+)
                if (IsWow64Process2(handle, out ushort processMachine, out ushort nativeMachine))
                {
                    return processMachine switch
                    {
                        0x014c => ProcessArchitecture.x86,  // IMAGE_FILE_MACHINE_I386
                        0x8664 => ProcessArchitecture.x64,  // IMAGE_FILE_MACHINE_AMD64
                        _ => ProcessArchitecture.Unknown
                    };
                }

                // 回退到旧的 IsWow64Process API
                if (IsWow64Process(handle, out bool isWow64))
                {
                    return isWow64 ? ProcessArchitecture.x86 : ProcessArchitecture.x64;
                }

                return ProcessArchitecture.Unknown;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        /// <summary>
        /// 同步检查进程访问权限
        /// </summary>
        private ProcessAccessResult CheckProcessAccessSync(int processId)
        {
            var result = new ProcessAccessResult();
            
            // 尝试不同级别的访问权限
            var accessLevels = new[]
            {
                (PROCESS_ALL_ACCESS, ProcessAccessLevel.FullAccess),
                (PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, ProcessAccessLevel.MemoryWrite),
                (PROCESS_VM_READ, ProcessAccessLevel.MemoryRead),
                (PROCESS_QUERY_INFORMATION, ProcessAccessLevel.BasicInfo)
            };

            foreach (var (access, level) in accessLevels)
            {
                var handle = OpenProcess(access, false, processId);
                if (handle != IntPtr.Zero)
                {
                    CloseHandle(handle);
                    result.CanAccess = true;
                    result.AccessLevel = level;
                    return result;
                }
            }

            result.CanAccess = false;
            result.AccessLevel = ProcessAccessLevel.None;
            result.ErrorMessage = "无法访问进程，可能需要管理员权限";
            result.RequiresAdminRights = true;
            
            return result;
        }

        /// <summary>
        /// 同步检查是否为保护进程
        /// </summary>
        private bool IsProtectedProcessSync(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                
                // 检查一些已知的保护进程
                var protectedProcessNames = new[]
                {
                    "csrss", "winlogon", "services", "lsass", "svchost",
                    "dwm", "explorer", "audiodg", "smss", "wininit"
                };

                return protectedProcessNames.Contains(process.ProcessName.ToLowerInvariant());
            }
            catch
            {
                return true; // 如果无法访问，假设是保护进程
            }
        }

        /// <summary>
        /// 检查是否为系统进程
        /// </summary>
        private bool IsSystemProcess(Process process)
        {
            try
            {
                // 系统关键进程列表
                var systemProcessNames = new[]
                {
                    "system", "idle", "csrss", "winlogon", "services", "lsass",
                    "svchost", "smss", "wininit", "dwm", "audiodg"
                };

                var processName = process.ProcessName.ToLowerInvariant();

                // 检查是否为系统关键进程
                return process.Id <= 4 ||
                       systemProcessNames.Any(name => processName.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// 监控进程变化
        /// </summary>
        private void MonitorProcesses(object? state)
        {
            try
            {
                _ = Task.Run(async () =>
                {
                    await RefreshProcessListAsync();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "进程监控失败");
            }
        }

        /// <summary>
        /// 检查进程列表变化
        /// </summary>
        private void CheckProcessListChanges(List<ProcessInfo> oldProcesses, List<ProcessInfo> newProcesses)
        {
            var oldIds = oldProcesses.Select(p => p.ProcessId).ToHashSet();
            var newIds = newProcesses.Select(p => p.ProcessId).ToHashSet();

            var addedIds = newIds.Except(oldIds).ToList();
            var removedIds = oldIds.Except(newIds).ToList();

            if (addedIds.Any() || removedIds.Any())
            {
                var args = new ProcessListChangedEventArgs
                {
                    AddedProcesses = newProcesses.Where(p => addedIds.Contains(p.ProcessId)).ToList(),
                    RemovedProcesses = oldProcesses.Where(p => removedIds.Contains(p.ProcessId)).ToList()
                };

                ProcessListChanged?.Invoke(this, args);
            }
        }

        #endregion

        public void Dispose()
        {
            _processMonitorTimer?.Dispose();
        }
    }
}
