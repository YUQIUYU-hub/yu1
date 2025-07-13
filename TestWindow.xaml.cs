using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using MemoryHook.Core.Models;

namespace MemoryHook.UI
{
    public partial class TestWindow : Window
    {
        private ObservableCollection<ProcessInfo> _processes = new();

        public TestWindow()
        {
            InitializeComponent();
            ProcessListView.ItemsSource = _processes;
            LoadProcesses();
        }

        private void LoadProcesses()
        {
            try
            {
                _processes.Clear();
                
                var systemProcesses = Process.GetProcesses().Take(20); // 只取前20个进程进行测试
                
                foreach (var process in systemProcesses)
                {
                    try
                    {
                        if (process.HasExited)
                            continue;

                        var processInfo = ProcessInfo.FromProcess(process);
                        
                        // 确保进程名称不为空
                        if (string.IsNullOrEmpty(processInfo.ProcessName))
                        {
                            processInfo.ProcessName = $"进程_{process.Id}";
                        }

                        _processes.Add(processInfo);
                        
                        // 调试输出
                        Console.WriteLine($"添加进程: {processInfo.ProcessName} (PID: {processInfo.ProcessId}) - ToString: {processInfo.ToString()}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"处理进程 {process.Id} 时出错: {ex.Message}");
                    }
                    finally
                    {
                        try
                        {
                            process?.Dispose();
                        }
                        catch { }
                    }
                }

                ProcessCountText.Text = _processes.Count.ToString();
                
                // 额外的调试信息
                Console.WriteLine($"总共加载了 {_processes.Count} 个进程");
                if (_processes.Count > 0)
                {
                    var firstProcess = _processes.First();
                    Console.WriteLine($"第一个进程详细信息:");
                    Console.WriteLine($"  ProcessName: '{firstProcess.ProcessName}'");
                    Console.WriteLine($"  ProcessId: {firstProcess.ProcessId}");
                    Console.WriteLine($"  ToString(): '{firstProcess.ToString()}'");
                    Console.WriteLine($"  DisplayName: '{firstProcess.DisplayName}'");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载进程列表时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"加载进程列表时出错: {ex}");
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadProcesses();
        }
    }
}
