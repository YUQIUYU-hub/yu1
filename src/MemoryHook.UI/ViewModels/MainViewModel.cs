using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MemoryHook.Core.Interfaces;
using MemoryHook.Core.Models;

namespace MemoryHook.UI.ViewModels
{
    /// <summary>
    /// 主窗口视图模型
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly IProcessService _processService;
        private readonly IMemoryService _memoryService;
        private readonly ILogger<MainViewModel> _logger;
        private readonly DispatcherTimer _timer;
        private CancellationTokenSource? _searchCancellationTokenSource;

        #region 属性

        [ObservableProperty]
        private ObservableCollection<ProcessInfo> _processes = new();

        [ObservableProperty]
        private ObservableCollection<ProcessInfo> _filteredProcesses = new();

        [ObservableProperty]
        private ProcessInfo? _selectedProcess;

        [ObservableProperty]
        private string _processSearchText = string.Empty;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private string _connectionStatusText = "未连接";

        [ObservableProperty]
        private Brush _connectionStatusBrush = Brushes.Gray;

        [ObservableProperty]
        private string _connectButtonText = "连接";

        [ObservableProperty]
        private bool _canConnect;

        [ObservableProperty]
        private string _selectedProcessInfo = string.Empty;

        [ObservableProperty]
        private string _memoryAddress = string.Empty;

        [ObservableProperty]
        private DataTypeInfo? _selectedDataType;

        [ObservableProperty]
        private string _currentValue = string.Empty;

        [ObservableProperty]
        private string _newValue = string.Empty;

        [ObservableProperty]
        private string _searchValue = string.Empty;

        [ObservableProperty]
        private DataTypeInfo? _searchDataType;

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private int _searchProgress;

        [ObservableProperty]
        private ObservableCollection<MemorySearchResult> _searchResults = new();

        [ObservableProperty]
        private MemorySearchResult? _selectedSearchResult;

        [ObservableProperty]
        private ObservableCollection<MemoryRegionInfo> _memoryRegions = new();

        [ObservableProperty]
        private string _statusMessage = "就绪";

        [ObservableProperty]
        private int _processCount;

        [ObservableProperty]
        private DateTime _currentTime = DateTime.Now;

        public ObservableCollection<DataTypeInfo> DataTypes { get; } = new()
        {
            new DataTypeInfo { Name = "8位整数 (Int8)", Type = DataType.Int8 },
            new DataTypeInfo { Name = "8位无符号整数 (UInt8)", Type = DataType.UInt8 },
            new DataTypeInfo { Name = "16位整数 (Int16)", Type = DataType.Int16 },
            new DataTypeInfo { Name = "16位无符号整数 (UInt16)", Type = DataType.UInt16 },
            new DataTypeInfo { Name = "32位整数 (Int32)", Type = DataType.Int32 },
            new DataTypeInfo { Name = "32位无符号整数 (UInt32)", Type = DataType.UInt32 },
            new DataTypeInfo { Name = "64位整数 (Int64)", Type = DataType.Int64 },
            new DataTypeInfo { Name = "64位无符号整数 (UInt64)", Type = DataType.UInt64 },
            new DataTypeInfo { Name = "32位浮点数 (Float)", Type = DataType.Float },
            new DataTypeInfo { Name = "64位浮点数 (Double)", Type = DataType.Double },
            new DataTypeInfo { Name = "ASCII字符串", Type = DataType.StringAscii },
            new DataTypeInfo { Name = "Unicode字符串", Type = DataType.StringUnicode },
            new DataTypeInfo { Name = "UTF-8字符串", Type = DataType.StringUtf8 },
            new DataTypeInfo { Name = "字节数组", Type = DataType.ByteArray }
        };

        #endregion

        public MainViewModel(IProcessService processService, IMemoryService memoryService, ILogger<MainViewModel> logger)
        {
            _processService = processService;
            _memoryService = memoryService;
            _logger = logger;

            // 设置默认数据类型
            SelectedDataType = DataTypes.FirstOrDefault(d => d.Type == DataType.Int32);
            SearchDataType = DataTypes.FirstOrDefault(d => d.Type == DataType.Int32);

            // 设置定时器更新时间
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, e) => CurrentTime = DateTime.Now;
            _timer.Start();

            // 订阅事件
            _processService.ProcessListChanged += OnProcessListChanged;
            _memoryService.ProcessConnectionChanged += OnProcessConnectionChanged;
            _memoryService.MemoryValueChanged += OnMemoryValueChanged;

            // 初始化
            _ = InitializeAsync();
        }

        #region 命令

        [RelayCommand]
        private async Task RefreshProcessesAsync()
        {
            try
            {
                StatusMessage = "正在刷新进程列表...";
                // 通过ProcessService的RefreshProcessListAsync方法触发事件机制
                // 这样避免重复获取进程
                await _processService.RefreshProcessListAsync();
                StatusMessage = "进程列表刷新完成";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新进程列表失败");
                StatusMessage = "刷新进程列表失败";
            }
        }

        [RelayCommand]
        private async Task ToggleConnectionAsync()
        {
            try
            {
                if (IsConnected)
                {
                    await DisconnectAsync();
                }
                else
                {
                    await ConnectAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "切换连接状态失败");
                StatusMessage = "连接操作失败";
            }
        }

        [RelayCommand]
        private async Task ReadMemoryAsync()
        {
            try
            {
                if (!IsConnected || SelectedDataType == null)
                    return;

                if (!TryParseAddress(MemoryAddress, out IntPtr address))
                {
                    StatusMessage = "无效的内存地址";
                    return;
                }

                StatusMessage = "正在读取内存...";
                var result = await _memoryService.ReadMemoryAsync(address, SelectedDataType.Type);
                
                if (result.Success && result.Data != null)
                {
                    CurrentValue = result.Data.ValueAsString;
                    StatusMessage = "内存读取成功";
                }
                else
                {
                    StatusMessage = $"内存读取失败: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取内存失败");
                StatusMessage = "读取内存失败";
            }
        }

        [RelayCommand]
        private async Task WriteMemoryAsync()
        {
            try
            {
                if (!IsConnected || SelectedDataType == null || string.IsNullOrEmpty(NewValue))
                    return;

                if (!TryParseAddress(MemoryAddress, out IntPtr address))
                {
                    StatusMessage = "无效的内存地址";
                    return;
                }

                var memoryValue = MemoryValue.CreateEmpty(address, SelectedDataType.Type);
                memoryValue.SetValueFromString(NewValue);

                StatusMessage = "正在写入内存...";
                var result = await _memoryService.WriteMemoryAsync(memoryValue);
                
                if (result.Success)
                {
                    StatusMessage = "内存写入成功";
                    // 自动读取新值
                    await ReadMemoryAsync();
                }
                else
                {
                    StatusMessage = $"内存写入失败: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "写入内存失败");
                StatusMessage = "写入内存失败";
            }
        }

        [RelayCommand]
        private async Task BackupMemoryAsync()
        {
            try
            {
                if (!IsConnected || SelectedDataType == null)
                    return;

                if (!TryParseAddress(MemoryAddress, out IntPtr address))
                {
                    StatusMessage = "无效的内存地址";
                    return;
                }

                var size = MemoryValue.GetTypeSize(SelectedDataType.Type);
                if (size <= 0)
                {
                    StatusMessage = "无法确定数据大小";
                    return;
                }

                StatusMessage = "正在备份内存...";
                var result = await _memoryService.BackupMemoryAsync(address, size);
                
                if (result.Success)
                {
                    StatusMessage = "内存备份成功";
                }
                else
                {
                    StatusMessage = $"内存备份失败: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "备份内存失败");
                StatusMessage = "备份内存失败";
            }
        }

        [RelayCommand]
        private async Task SearchMemoryAsync()
        {
            try
            {
                if (!IsConnected || SearchDataType == null || string.IsNullOrEmpty(SearchValue))
                    return;

                // 取消之前的搜索
                _searchCancellationTokenSource?.Cancel();
                _searchCancellationTokenSource = new CancellationTokenSource();

                IsSearching = true;
                SearchProgress = 0;
                SearchResults.Clear();
                StatusMessage = "正在搜索内存...";

                var searchParams = new MemorySearchParams
                {
                    SearchValue = SearchValue,
                    DataType = SearchDataType.Type,
                    MaxResults = 1000,
                    IncludeReadOnly = true,
                    IncludeExecutable = false
                };

                var progress = new Progress<MemorySearchProgress>(p =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SearchProgress = p.ProgressPercentage;
                        StatusMessage = p.StatusMessage;
                    });
                });

                var result = await _memoryService.SearchMemoryAsync(searchParams, progress, _searchCancellationTokenSource.Token);

                if (result.Success && result.Data != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SearchResults.Clear();
                        foreach (var searchResult in result.Data)
                        {
                            SearchResults.Add(searchResult);
                        }
                    });

                    StatusMessage = $"搜索完成，找到 {result.Data.Count} 个结果";
                }
                else
                {
                    StatusMessage = $"搜索失败: {result.ErrorMessage}";
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "搜索已取消";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜索内存失败");
                StatusMessage = "搜索内存失败";
            }
            finally
            {
                IsSearching = false;
                SearchProgress = 0;
            }
        }

        [RelayCommand]
        private void OpenSettings()
        {
            // TODO: 实现设置对话框
            StatusMessage = "设置功能开发中...";
        }

        [RelayCommand]
        private void ShowAbout()
        {
            var aboutMessage = "MemoryHook v1.0\n\n" +
                              "基于EasyHook技术的Windows内存修改工具\n" +
                              "支持进程内存读写、搜索和监控\n\n" +
                              "开发团队: MemoryHook Team\n" +
                              "版权所有 © 2025";

            MessageBox.Show(aboutMessage, "关于 MemoryHook", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region 私有方法

        private async Task InitializeAsync()
        {
            try
            {
                StatusMessage = "正在初始化...";
                _logger.LogInformation("开始初始化MainViewModel");

                // 短暂延迟确保事件订阅完成
                await Task.Delay(100);

                // 手动触发一次进程列表刷新以确保UI有数据
                _logger.LogDebug("触发初始进程列表刷新");
                await _processService.RefreshProcessListAsync();

                StatusMessage = "初始化完成";
                _logger.LogInformation("MainViewModel初始化完成，当前进程数: {Count}", Processes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化失败");
                StatusMessage = "初始化失败";
            }
        }

        private async Task ConnectAsync()
        {
            if (SelectedProcess == null)
            {
                StatusMessage = "请选择一个进程";
                return;
            }

            try
            {
                StatusMessage = "正在连接到进程...";
                var result = await _memoryService.ConnectToProcessAsync(SelectedProcess);

                if (result.Success)
                {
                    StatusMessage = $"已连接到进程: {SelectedProcess.ProcessName}";
                    await LoadMemoryRegionsAsync();
                }
                else
                {
                    StatusMessage = $"连接失败: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "连接到进程失败");
                StatusMessage = "连接到进程失败";
            }
        }

        private async Task DisconnectAsync()
        {
            try
            {
                StatusMessage = "正在断开连接...";
                var result = await _memoryService.DisconnectAsync();

                if (result.Success)
                {
                    StatusMessage = "已断开连接";
                    MemoryRegions.Clear();
                    CurrentValue = string.Empty;
                    SearchResults.Clear();
                }
                else
                {
                    StatusMessage = $"断开连接失败: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "断开连接失败");
                StatusMessage = "断开连接失败";
            }
        }

        private async Task LoadMemoryRegionsAsync()
        {
            try
            {
                var result = await _memoryService.GetMemoryRegionsAsync();

                if (result.Success && result.Data != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MemoryRegions.Clear();
                        foreach (var region in result.Data)
                        {
                            MemoryRegions.Add(region);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载内存区域失败");
            }
        }

        private void FilterProcesses()
        {
            try
            {
                FilteredProcesses.Clear();

                if (string.IsNullOrWhiteSpace(ProcessSearchText))
                {
                    // 如果没有搜索文本，显示所有进程
                    foreach (var process in Processes)
                    {
                        FilteredProcesses.Add(process);
                    }
                }
                else
                {
                    // 根据搜索文本过滤进程
                    var filteredProcesses = Processes.Where(process =>
                    {
                        // 确保进程名称不为空
                        var processName = process.ProcessName ?? string.Empty;
                        var windowTitle = process.WindowTitle ?? string.Empty;
                        var processIdStr = process.ProcessId.ToString();

                        return processName.Contains(ProcessSearchText, StringComparison.OrdinalIgnoreCase) ||
                               processIdStr.Contains(ProcessSearchText) ||
                               (!string.IsNullOrEmpty(windowTitle) &&
                                windowTitle.Contains(ProcessSearchText, StringComparison.OrdinalIgnoreCase));
                    });

                    foreach (var process in filteredProcesses)
                    {
                        FilteredProcesses.Add(process);
                    }
                }

                _logger.LogDebug("进程过滤完成，显示 {FilteredCount}/{TotalCount} 个进程",
                    FilteredProcesses.Count, Processes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "过滤进程列表时发生错误");
            }
        }

        private static bool TryParseAddress(string addressText, out IntPtr address)
        {
            address = IntPtr.Zero;

            if (string.IsNullOrWhiteSpace(addressText))
                return false;

            // 移除0x前缀
            if (addressText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                addressText = addressText[2..];
            }

            if (Environment.Is64BitProcess)
            {
                if (long.TryParse(addressText, System.Globalization.NumberStyles.HexNumber, null, out long longValue))
                {
                    address = new IntPtr(longValue);
                    return true;
                }
            }
            else
            {
                if (int.TryParse(addressText, System.Globalization.NumberStyles.HexNumber, null, out int intValue))
                {
                    address = new IntPtr(intValue);
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region 事件处理

        private void OnProcessListChanged(object? sender, ProcessListChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 添加新进程
                foreach (var process in e.AddedProcesses)
                {
                    // 验证进程名称
                    if (string.IsNullOrEmpty(process.ProcessName))
                    {
                        _logger.LogWarning("UI接收到进程名称为空的进程: PID {ProcessId}", process.ProcessId);
                        process.ProcessName = $"未知进程_{process.ProcessId}";
                    }

                    _logger.LogDebug("UI添加进程: {ProcessName} (PID: {ProcessId}) - ToString: {ToString}",
                        process.ProcessName, process.ProcessId, process.ToString());
                    Processes.Add(process);
                }

                // 移除已退出的进程
                foreach (var process in e.RemovedProcesses)
                {
                    var existingProcess = Processes.FirstOrDefault(p => p.ProcessId == process.ProcessId);
                    if (existingProcess != null)
                    {
                        _logger.LogDebug("UI移除进程: {ProcessName} (PID: {ProcessId})",
                            existingProcess.ProcessName, existingProcess.ProcessId);
                        Processes.Remove(existingProcess);
                    }
                }

                ProcessCount = Processes.Count;
                FilterProcesses();

                _logger.LogDebug("UI进程列表更新完成，当前进程数: {Count}", Processes.Count);
            });
        }

        private void OnProcessConnectionChanged(object? sender, ProcessConnectionChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsConnected = e.IsConnected;

                if (e.IsConnected && e.ProcessInfo != null)
                {
                    ConnectionStatusText = "已连接";
                    ConnectionStatusBrush = Brushes.Green;
                    ConnectButtonText = "断开";
                    SelectedProcessInfo = $"{e.ProcessInfo.ProcessName} (PID: {e.ProcessInfo.ProcessId})";
                }
                else
                {
                    ConnectionStatusText = "未连接";
                    ConnectionStatusBrush = Brushes.Gray;
                    ConnectButtonText = "连接";
                    SelectedProcessInfo = string.Empty;
                }
            });
        }

        private void OnMemoryValueChanged(object? sender, MemoryValueChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusMessage = $"内存值已更改: {e.Address:X}";
            });
        }

        partial void OnSelectedProcessChanged(ProcessInfo? value)
        {
            CanConnect = value != null && !IsConnected;
        }

        partial void OnProcessSearchTextChanged(string value)
        {
            FilterProcesses();
        }

        partial void OnSelectedSearchResultChanged(MemorySearchResult? value)
        {
            if (value != null)
            {
                MemoryAddress = value.AddressHex;
                CurrentValue = value.ValueString;
            }
        }

        #endregion

        public void Dispose()
        {
            _timer?.Stop();
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// 数据类型信息
    /// </summary>
    public class DataTypeInfo
    {
        public string Name { get; set; } = string.Empty;
        public DataType Type { get; set; }
    }
}
