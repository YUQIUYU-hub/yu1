using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SimpleBindingTest
{
    // 简化的ProcessInfo类用于测试
    public class SimpleProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string DisplayName => string.IsNullOrEmpty(ProcessName) ? $"进程_{ProcessId}" : ProcessName;
        
        public override string ToString()
        {
            return DisplayName;
        }
    }

    public partial class TestApp : Application
    {
        [STAThread]
        public static void Main()
        {
            var app = new TestApp();
            app.Run();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 创建测试窗口
            var window = new Window
            {
                Title = "ListView绑定测试",
                Width = 600,
                Height = 400
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 创建ListView
            var listView = new ListView();
            Grid.SetRow(listView, 0);

            // 设置GridView
            var gridView = new GridView();
            
            // 进程名列
            var nameColumn = new GridViewColumn
            {
                Header = "进程名",
                Width = 200,
                DisplayMemberBinding = new System.Windows.Data.Binding("ProcessName")
            };
            gridView.Columns.Add(nameColumn);

            // PID列
            var pidColumn = new GridViewColumn
            {
                Header = "PID",
                Width = 80,
                DisplayMemberBinding = new System.Windows.Data.Binding("ProcessId")
            };
            gridView.Columns.Add(pidColumn);

            // ToString列
            var toStringColumn = new GridViewColumn
            {
                Header = "ToString()",
                Width = 200,
                DisplayMemberBinding = new System.Windows.Data.Binding()
            };
            gridView.Columns.Add(toStringColumn);

            listView.View = gridView;

            // 创建数据
            var processes = new ObservableCollection<SimpleProcessInfo>();
            
            // 添加一些测试数据
            processes.Add(new SimpleProcessInfo { ProcessId = 1234, ProcessName = "notepad" });
            processes.Add(new SimpleProcessInfo { ProcessId = 5678, ProcessName = "explorer" });
            processes.Add(new SimpleProcessInfo { ProcessId = 9999, ProcessName = "" }); // 空名称测试

            // 添加真实进程数据
            try
            {
                var systemProcesses = Process.GetProcesses().Take(10);
                foreach (var process in systemProcesses)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            processes.Add(new SimpleProcessInfo
                            {
                                ProcessId = process.Id,
                                ProcessName = process.ProcessName ?? ""
                            });
                        }
                    }
                    catch
                    {
                        // 忽略无法访问的进程
                    }
                    finally
                    {
                        process?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取进程时出错: {ex.Message}");
            }

            listView.ItemsSource = processes;

            // 状态标签
            var statusLabel = new Label
            {
                Content = $"总共 {processes.Count} 个进程"
            };
            Grid.SetRow(statusLabel, 1);

            grid.Children.Add(listView);
            grid.Children.Add(statusLabel);
            window.Content = grid;

            // 调试输出
            Console.WriteLine("=== 绑定测试开始 ===");
            foreach (var process in processes.Take(5))
            {
                Console.WriteLine($"进程: {process.ProcessName} (PID: {process.ProcessId}) - ToString: {process.ToString()}");
            }

            window.Show();
        }
    }
}
