using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MemoryHook.Core.Interfaces;
using MemoryHook.Core.Models;

namespace MemoryHook.Core.Services
{
    /// <summary>
    /// 内存操作服务实现
    /// </summary>
    public class MemoryService : IMemoryService
    {
        private readonly ILogger<MemoryService> _logger;
        private ProcessInfo? _targetProcess;
        private IntPtr _processHandle = IntPtr.Zero;
        private readonly List<MemoryBackup> _memoryBackups = new();
        private readonly object _lockObject = new();

        // Windows API 导入
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        // 进程访问权限常量
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;

        // 内存保护常量
        private const uint PAGE_NOACCESS = 0x01;
        private const uint PAGE_READONLY = 0x02;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_WRITECOPY = 0x08;
        private const uint PAGE_EXECUTE = 0x10;
        private const uint PAGE_EXECUTE_READ = 0x20;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint PAGE_EXECUTE_WRITECOPY = 0x80;

        // 内存状态常量
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_FREE = 0x10000;
        private const uint MEM_RESERVE = 0x2000;

        // 内存类型常量
        private const uint MEM_IMAGE = 0x1000000;
        private const uint MEM_MAPPED = 0x40000;
        private const uint MEM_PRIVATE = 0x20000;

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        public ProcessInfo? TargetProcess => _targetProcess;
        public bool IsConnected => _processHandle != IntPtr.Zero && _targetProcess != null;

        public event EventHandler<MemoryValueChangedEventArgs>? MemoryValueChanged;
        public event EventHandler<ProcessConnectionChangedEventArgs>? ProcessConnectionChanged;

        public MemoryService(ILogger<MemoryService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 连接到目标进程
        /// </summary>
        public async Task<MemoryOperationResult> ConnectToProcessAsync(ProcessInfo processInfo, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();
                
                try
                {
                    // 如果已经连接到其他进程，先断开
                    if (IsConnected)
                    {
                        DisconnectSync();
                    }

                    // 检查进程是否仍在运行
                    try
                    {
                        using var process = Process.GetProcessById(processInfo.ProcessId);
                        if (process.HasExited)
                        {
                            return MemoryOperationResult.CreateFailure("目标进程已退出", -1);
                        }
                    }
                    catch (ArgumentException)
                    {
                        return MemoryOperationResult.CreateFailure("目标进程不存在", -1);
                    }

                    // 尝试打开进程句柄
                    var requiredAccess = PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION;
                    _processHandle = OpenProcess(requiredAccess, false, processInfo.ProcessId);

                    if (_processHandle == IntPtr.Zero)
                    {
                        var errorCode = Marshal.GetLastWin32Error();
                        return MemoryOperationResult.CreateFailure($"无法打开进程句柄，错误代码: {errorCode}。可能需要管理员权限。", errorCode);
                    }

                    _targetProcess = processInfo;
                    
                    var result = MemoryOperationResult.CreateSuccess();
                    result.Duration = stopwatch.Elapsed;

                    // 触发连接状态变化事件
                    ProcessConnectionChanged?.Invoke(this, new ProcessConnectionChangedEventArgs
                    {
                        IsConnected = true,
                        ProcessInfo = processInfo
                    });

                    _logger.LogInformation("成功连接到进程: {ProcessName} (PID: {ProcessId})", processInfo.ProcessName, processInfo.ProcessId);
                    
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "连接到进程失败: {ProcessName} (PID: {ProcessId})", processInfo.ProcessName, processInfo.ProcessId);
                    
                    var result = MemoryOperationResult.CreateFailure($"连接失败: {ex.Message}");
                    result.Duration = stopwatch.Elapsed;
                    
                    return result;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 断开与目标进程的连接
        /// </summary>
        public async Task<MemoryOperationResult> DisconnectAsync()
        {
            return await Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    DisconnectSync();

                    var result = MemoryOperationResult.CreateSuccess();
                    result.Duration = stopwatch.Elapsed;

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "断开连接失败");

                    var result = MemoryOperationResult.CreateFailure($"断开连接失败: {ex.Message}");
                    result.Duration = stopwatch.Elapsed;

                    return result;
                }
            });
        }

        /// <summary>
        /// 读取内存值
        /// </summary>
        public async Task<MemoryOperationResult<MemoryValue>> ReadMemoryAsync(IntPtr address, DataType dataType, int size = 0, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    if (!IsConnected)
                    {
                        return MemoryOperationResult<MemoryValue>.CreateFailure("未连接到目标进程");
                    }

                    // 确定读取大小
                    var readSize = size > 0 ? size : MemoryValue.GetTypeSize(dataType);
                    if (readSize <= 0)
                    {
                        return MemoryOperationResult<MemoryValue>.CreateFailure("无效的数据类型或大小");
                    }

                    // 读取内存
                    var buffer = new byte[readSize];
                    if (!ReadProcessMemory(_processHandle, address, buffer, readSize, out int bytesRead))
                    {
                        var errorCode = Marshal.GetLastWin32Error();
                        return MemoryOperationResult<MemoryValue>.CreateFailure($"读取内存失败，错误代码: {errorCode}", errorCode);
                    }

                    if (bytesRead != readSize)
                    {
                        return MemoryOperationResult<MemoryValue>.CreateFailure($"读取字节数不匹配，期望: {readSize}，实际: {bytesRead}");
                    }

                    var memoryValue = new MemoryValue
                    {
                        Address = address,
                        Type = dataType,
                        RawData = buffer
                    };

                    var result = MemoryOperationResult<MemoryValue>.CreateSuccess(memoryValue);
                    result.Duration = stopwatch.Elapsed;

                    _logger.LogDebug("成功读取内存: 地址 {Address}, 类型 {DataType}, 值 {Value}",
                        address.ToString("X"), dataType, memoryValue.ValueAsString);

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "读取内存失败: 地址 {Address}", address.ToString("X"));

                    var result = MemoryOperationResult<MemoryValue>.CreateFailure($"读取内存失败: {ex.Message}");
                    result.Duration = stopwatch.Elapsed;

                    return result;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 写入内存值
        /// </summary>
        public async Task<MemoryOperationResult> WriteMemoryAsync(MemoryValue memoryValue, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    if (!IsConnected)
                    {
                        return MemoryOperationResult.CreateFailure("未连接到目标进程");
                    }

                    if (memoryValue.RawData.Length == 0)
                    {
                        return MemoryOperationResult.CreateFailure("没有要写入的数据");
                    }

                    // 备份原始值
                    var backupResult = BackupMemorySync(memoryValue.Address, memoryValue.RawData.Length);
                    if (backupResult.Success && backupResult.Data != null)
                    {
                        lock (_lockObject)
                        {
                            _memoryBackups.Add(backupResult.Data);
                        }
                    }

                    // 尝试修改内存保护属性以允许写入
                    var oldProtect = ChangeMemoryProtection(memoryValue.Address, memoryValue.RawData.Length, PAGE_EXECUTE_READWRITE);

                    try
                    {
                        // 写入内存
                        if (!WriteProcessMemory(_processHandle, memoryValue.Address, memoryValue.RawData, memoryValue.RawData.Length, out int bytesWritten))
                        {
                            var errorCode = Marshal.GetLastWin32Error();
                            return MemoryOperationResult.CreateFailure($"写入内存失败，错误代码: {errorCode}", errorCode);
                        }

                        if (bytesWritten != memoryValue.RawData.Length)
                        {
                            return MemoryOperationResult.CreateFailure($"写入字节数不匹配，期望: {memoryValue.RawData.Length}，实际: {bytesWritten}");
                        }

                        var result = MemoryOperationResult.CreateSuccess();
                        result.Duration = stopwatch.Elapsed;

                        // 触发内存值变化事件
                        MemoryValueChanged?.Invoke(this, new MemoryValueChangedEventArgs
                        {
                            Address = memoryValue.Address,
                            NewValue = memoryValue,
                            OldValue = backupResult.Data != null ? new MemoryValue
                            {
                                Address = memoryValue.Address,
                                Type = memoryValue.Type,
                                RawData = backupResult.Data.Data
                            } : null
                        });

                        _logger.LogInformation("成功写入内存: 地址 {Address}, 类型 {DataType}, 值 {Value}",
                            memoryValue.Address.ToString("X"), memoryValue.Type, memoryValue.ValueAsString);

                        return result;
                    }
                    finally
                    {
                        // 恢复原始内存保护属性
                        if (oldProtect.HasValue)
                        {
                            ChangeMemoryProtection(memoryValue.Address, memoryValue.RawData.Length, oldProtect.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "写入内存失败: 地址 {Address}", memoryValue.Address.ToString("X"));

                    var result = MemoryOperationResult.CreateFailure($"写入内存失败: {ex.Message}");
                    result.Duration = stopwatch.Elapsed;

                    return result;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 搜索内存
        /// </summary>
        public async Task<MemoryOperationResult<List<MemorySearchResult>>> SearchMemoryAsync(MemorySearchParams searchParams, IProgress<MemorySearchProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();
                var results = new List<MemorySearchResult>();

                try
                {
                    if (!IsConnected)
                    {
                        return MemoryOperationResult<List<MemorySearchResult>>.CreateFailure("未连接到目标进程");
                    }

                    // 获取搜索的字节模式
                    var searchBytes = GetSearchBytes(searchParams);
                    if (searchBytes.Length == 0)
                    {
                        return MemoryOperationResult<List<MemorySearchResult>>.CreateFailure("无效的搜索值");
                    }

                    // 获取内存区域
                    var regions = GetMemoryRegionsSync();
                    var searchableRegions = regions.Where(r => IsRegionSearchable(r, searchParams)).ToList();

                    var totalRegions = searchableRegions.Count;
                    var processedRegions = 0;

                    foreach (var region in searchableRegions)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        if (results.Count >= searchParams.MaxResults)
                            break;

                        try
                        {
                            var regionResults = SearchInRegion(region, searchBytes, searchParams);
                            results.AddRange(regionResults.Take(searchParams.MaxResults - results.Count));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "搜索内存区域失败: {BaseAddress}", region.BaseAddress.ToString("X"));
                        }

                        processedRegions++;

                        // 报告进度
                        progress?.Report(new MemorySearchProgress
                        {
                            ProgressPercentage = (int)((double)processedRegions / totalRegions * 100),
                            CurrentAddress = region.BaseAddress,
                            ResultsFound = results.Count,
                            RegionsSearched = processedRegions,
                            TotalRegions = totalRegions,
                            StatusMessage = $"正在搜索内存区域 {processedRegions}/{totalRegions}"
                        });
                    }

                    var result = MemoryOperationResult<List<MemorySearchResult>>.CreateSuccess(results);
                    result.Duration = stopwatch.Elapsed;

                    _logger.LogInformation("内存搜索完成: 找到 {ResultCount} 个结果，耗时 {Duration}ms",
                        results.Count, stopwatch.ElapsedMilliseconds);

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "内存搜索失败");

                    var result = MemoryOperationResult<List<MemorySearchResult>>.CreateFailure($"内存搜索失败: {ex.Message}");
                    result.Duration = stopwatch.Elapsed;

                    return result;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 获取内存区域信息
        /// </summary>
        public async Task<MemoryOperationResult<MemoryRegionInfo>> GetMemoryRegionInfoAsync(IntPtr address, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    if (!IsConnected)
                    {
                        return MemoryOperationResult<MemoryRegionInfo>.CreateFailure("未连接到目标进程");
                    }

                    var regionInfo = GetMemoryRegionInfoSync(address);
                    if (regionInfo == null)
                    {
                        return MemoryOperationResult<MemoryRegionInfo>.CreateFailure("无法获取内存区域信息");
                    }

                    var result = MemoryOperationResult<MemoryRegionInfo>.CreateSuccess(regionInfo);
                    result.Duration = stopwatch.Elapsed;

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "获取内存区域信息失败: 地址 {Address}", address.ToString("X"));

                    var result = MemoryOperationResult<MemoryRegionInfo>.CreateFailure($"获取内存区域信息失败: {ex.Message}");
                    result.Duration = stopwatch.Elapsed;

                    return result;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 获取所有内存区域
        /// </summary>
        public async Task<MemoryOperationResult<List<MemoryRegionInfo>>> GetMemoryRegionsAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    if (!IsConnected)
                    {
                        return MemoryOperationResult<List<MemoryRegionInfo>>.CreateFailure("未连接到目标进程");
                    }

                    var regions = GetMemoryRegionsSync();

                    var result = MemoryOperationResult<List<MemoryRegionInfo>>.CreateSuccess(regions);
                    result.Duration = stopwatch.Elapsed;

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "获取内存区域列表失败");

                    var result = MemoryOperationResult<List<MemoryRegionInfo>>.CreateFailure($"获取内存区域列表失败: {ex.Message}");
                    result.Duration = stopwatch.Elapsed;

                    return result;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 备份内存值
        /// </summary>
        public async Task<MemoryOperationResult<MemoryBackup>> BackupMemoryAsync(IntPtr address, int size, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => BackupMemorySync(address, size), cancellationToken);
        }

        /// <summary>
        /// 恢复内存值
        /// </summary>
        public async Task<MemoryOperationResult> RestoreMemoryAsync(MemoryBackup backup, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    if (!IsConnected)
                    {
                        return MemoryOperationResult.CreateFailure("未连接到目标进程");
                    }

                    if (backup.Data.Length == 0)
                    {
                        return MemoryOperationResult.CreateFailure("备份数据为空");
                    }

                    // 尝试修改内存保护属性
                    var oldProtect = ChangeMemoryProtection(backup.Address, backup.Data.Length, PAGE_EXECUTE_READWRITE);

                    try
                    {
                        // 恢复内存
                        if (!WriteProcessMemory(_processHandle, backup.Address, backup.Data, backup.Data.Length, out int bytesWritten))
                        {
                            var errorCode = Marshal.GetLastWin32Error();
                            return MemoryOperationResult.CreateFailure($"恢复内存失败，错误代码: {errorCode}", errorCode);
                        }

                        if (bytesWritten != backup.Data.Length)
                        {
                            return MemoryOperationResult.CreateFailure($"恢复字节数不匹配，期望: {backup.Data.Length}，实际: {bytesWritten}");
                        }

                        var result = MemoryOperationResult.CreateSuccess();
                        result.Duration = stopwatch.Elapsed;

                        _logger.LogInformation("成功恢复内存: 地址 {Address}, 大小 {Size} 字节",
                            backup.Address.ToString("X"), backup.Data.Length);

                        return result;
                    }
                    finally
                    {
                        // 恢复原始内存保护属性
                        if (oldProtect.HasValue)
                        {
                            ChangeMemoryProtection(backup.Address, backup.Data.Length, oldProtect.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "恢复内存失败: 地址 {Address}", backup.Address.ToString("X"));

                    var result = MemoryOperationResult.CreateFailure($"恢复内存失败: {ex.Message}");
                    result.Duration = stopwatch.Elapsed;

                    return result;
                }
            }, cancellationToken);
        }

        #region 私有方法

        /// <summary>
        /// 同步断开连接
        /// </summary>
        private void DisconnectSync()
        {
            lock (_lockObject)
            {
                if (_processHandle != IntPtr.Zero)
                {
                    CloseHandle(_processHandle);
                    _processHandle = IntPtr.Zero;
                }

                var oldProcess = _targetProcess;
                _targetProcess = null;

                // 清理内存备份
                _memoryBackups.Clear();

                // 触发连接状态变化事件
                ProcessConnectionChanged?.Invoke(this, new ProcessConnectionChangedEventArgs
                {
                    IsConnected = false,
                    ProcessInfo = oldProcess
                });

                if (oldProcess != null)
                {
                    _logger.LogInformation("已断开与进程的连接: {ProcessName} (PID: {ProcessId})",
                        oldProcess.ProcessName, oldProcess.ProcessId);
                }
            }
        }

        /// <summary>
        /// 同步备份内存
        /// </summary>
        private MemoryOperationResult<MemoryBackup> BackupMemorySync(IntPtr address, int size)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (!IsConnected)
                {
                    return MemoryOperationResult<MemoryBackup>.CreateFailure("未连接到目标进程");
                }

                var buffer = new byte[size];
                if (!ReadProcessMemory(_processHandle, address, buffer, size, out int bytesRead))
                {
                    var errorCode = Marshal.GetLastWin32Error();
                    return MemoryOperationResult<MemoryBackup>.CreateFailure($"备份内存失败，错误代码: {errorCode}", errorCode);
                }

                if (bytesRead != size)
                {
                    return MemoryOperationResult<MemoryBackup>.CreateFailure($"备份字节数不匹配，期望: {size}，实际: {bytesRead}");
                }

                var backup = new MemoryBackup
                {
                    Address = address,
                    Data = buffer,
                    BackupTime = DateTime.Now,
                    Description = $"地址 {address:X} 的内存备份"
                };

                var result = MemoryOperationResult<MemoryBackup>.CreateSuccess(backup);
                result.Duration = stopwatch.Elapsed;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "备份内存失败: 地址 {Address}", address.ToString("X"));

                var result = MemoryOperationResult<MemoryBackup>.CreateFailure($"备份内存失败: {ex.Message}");
                result.Duration = stopwatch.Elapsed;

                return result;
            }
        }

        /// <summary>
        /// 修改内存保护属性
        /// </summary>
        private uint? ChangeMemoryProtection(IntPtr address, int size, uint newProtect)
        {
            try
            {
                if (VirtualProtectEx(_processHandle, address, (UIntPtr)size, newProtect, out uint oldProtect))
                {
                    return oldProtect;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "修改内存保护属性失败: 地址 {Address}", address.ToString("X"));
            }

            return null;
        }

        /// <summary>
        /// 同步获取内存区域信息
        /// </summary>
        private MemoryRegionInfo? GetMemoryRegionInfoSync(IntPtr address)
        {
            try
            {
                if (VirtualQueryEx(_processHandle, address, out MEMORY_BASIC_INFORMATION mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                {
                    return null;
                }

                return new MemoryRegionInfo
                {
                    BaseAddress = mbi.BaseAddress,
                    Size = mbi.RegionSize.ToInt64(),
                    Protection = ConvertProtection(mbi.Protect),
                    State = ConvertState(mbi.State),
                    Type = ConvertType(mbi.Type)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取内存区域信息失败: 地址 {Address}", address.ToString("X"));
                return null;
            }
        }

        /// <summary>
        /// 同步获取所有内存区域
        /// </summary>
        private List<MemoryRegionInfo> GetMemoryRegionsSync()
        {
            var regions = new List<MemoryRegionInfo>();

            try
            {
                var address = IntPtr.Zero;
                var maxAddress = Environment.Is64BitProcess ? new IntPtr(0x7FFFFFFFFFFF) : new IntPtr(0x7FFFFFFF);

                while (address.ToInt64() < maxAddress.ToInt64())
                {
                    if (VirtualQueryEx(_processHandle, address, out MEMORY_BASIC_INFORMATION mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                    {
                        break;
                    }

                    if (mbi.State == MEM_COMMIT)
                    {
                        var region = new MemoryRegionInfo
                        {
                            BaseAddress = mbi.BaseAddress,
                            Size = mbi.RegionSize.ToInt64(),
                            Protection = ConvertProtection(mbi.Protect),
                            State = ConvertState(mbi.State),
                            Type = ConvertType(mbi.Type)
                        };

                        regions.Add(region);
                    }

                    address = new IntPtr(mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取内存区域列表失败");
            }

            return regions;
        }

        /// <summary>
        /// 获取搜索字节数组
        /// </summary>
        private byte[] GetSearchBytes(MemorySearchParams searchParams)
        {
            try
            {
                var memoryValue = MemoryValue.CreateEmpty(IntPtr.Zero, searchParams.DataType);
                memoryValue.SetValueFromString(searchParams.SearchValue);
                return memoryValue.RawData;
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// 检查内存区域是否可搜索
        /// </summary>
        private bool IsRegionSearchable(MemoryRegionInfo region, MemorySearchParams searchParams)
        {
            // 检查保护类型
            if (!searchParams.IncludeReadOnly &&
                (region.Protection == MemoryProtection.ReadOnly || region.Protection == MemoryProtection.ExecuteRead))
            {
                return false;
            }

            if (!searchParams.IncludeExecutable &&
                (region.Protection == MemoryProtection.Execute ||
                 region.Protection == MemoryProtection.ExecuteRead ||
                 region.Protection == MemoryProtection.ExecuteReadWrite ||
                 region.Protection == MemoryProtection.ExecuteWriteCopy))
            {
                return false;
            }

            // 检查地址范围
            if (searchParams.StartAddress != IntPtr.Zero && region.BaseAddress.ToInt64() < searchParams.StartAddress.ToInt64())
            {
                return false;
            }

            if (searchParams.EndAddress != IntPtr.Zero && region.EndAddress.ToInt64() > searchParams.EndAddress.ToInt64())
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 在内存区域中搜索
        /// </summary>
        private List<MemorySearchResult> SearchInRegion(MemoryRegionInfo region, byte[] searchBytes, MemorySearchParams searchParams)
        {
            var results = new List<MemorySearchResult>();

            try
            {
                var regionSize = (int)Math.Min(region.Size, int.MaxValue);
                var buffer = new byte[regionSize];

                if (!ReadProcessMemory(_processHandle, region.BaseAddress, buffer, regionSize, out int bytesRead))
                {
                    return results;
                }

                // 搜索字节模式
                for (int i = 0; i <= bytesRead - searchBytes.Length; i += searchParams.Alignment)
                {
                    if (results.Count >= searchParams.MaxResults)
                        break;

                    bool found = true;
                    for (int j = 0; j < searchBytes.Length; j++)
                    {
                        if (buffer[i + j] != searchBytes[j])
                        {
                            found = false;
                            break;
                        }
                    }

                    if (found)
                    {
                        var address = new IntPtr(region.BaseAddress.ToInt64() + i);
                        var foundBytes = new byte[searchBytes.Length];
                        Array.Copy(buffer, i, foundBytes, 0, searchBytes.Length);

                        var result = new MemorySearchResult
                        {
                            Address = address,
                            Value = new MemoryValue
                            {
                                Address = address,
                                Type = searchParams.DataType,
                                RawData = foundBytes
                            },
                            RegionInfo = region
                        };

                        results.Add(result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "在内存区域中搜索失败: {BaseAddress}", region.BaseAddress.ToString("X"));
            }

            return results;
        }

        /// <summary>
        /// 转换内存保护类型
        /// </summary>
        private MemoryProtection ConvertProtection(uint protect)
        {
            return protect switch
            {
                PAGE_NOACCESS => MemoryProtection.NoAccess,
                PAGE_READONLY => MemoryProtection.ReadOnly,
                PAGE_READWRITE => MemoryProtection.ReadWrite,
                PAGE_WRITECOPY => MemoryProtection.WriteCopy,
                PAGE_EXECUTE => MemoryProtection.Execute,
                PAGE_EXECUTE_READ => MemoryProtection.ExecuteRead,
                PAGE_EXECUTE_READWRITE => MemoryProtection.ExecuteReadWrite,
                PAGE_EXECUTE_WRITECOPY => MemoryProtection.ExecuteWriteCopy,
                _ => MemoryProtection.NoAccess
            };
        }

        /// <summary>
        /// 转换内存状态
        /// </summary>
        private MemoryState ConvertState(uint state)
        {
            return state switch
            {
                MEM_COMMIT => MemoryState.Commit,
                MEM_FREE => MemoryState.Free,
                MEM_RESERVE => MemoryState.Reserve,
                _ => MemoryState.Free
            };
        }

        /// <summary>
        /// 转换内存类型
        /// </summary>
        private MemoryType ConvertType(uint type)
        {
            return type switch
            {
                MEM_IMAGE => MemoryType.Image,
                MEM_MAPPED => MemoryType.Mapped,
                MEM_PRIVATE => MemoryType.Private,
                _ => MemoryType.Private
            };
        }

        #endregion

        public void Dispose()
        {
            DisconnectSync();
        }
    }
}
