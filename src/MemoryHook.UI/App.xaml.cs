using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using MemoryHook.Core.Interfaces;
using MemoryHook.Core.Services;
using MemoryHook.UI.ViewModels;

namespace MemoryHook.UI
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private IHost? _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 配置日志
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs/memoryhook-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                // 构建主机
                _host = Host.CreateDefaultBuilder()
                    .UseSerilog()
                    .ConfigureServices(ConfigureServices)
                    .Build();

                // 启动主机
                _host.Start();

                // 创建并显示主窗口
                var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
                var mainWindow = new MainWindow(mainViewModel);
                mainWindow.Show();

                // 同时显示测试窗口用于调试
                var testWindow = new TestWindow();
                testWindow.Show();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "应用程序启动失败");
                MessageBox.Show($"应用程序启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _host?.StopAsync(TimeSpan.FromSeconds(5)).Wait();
                _host?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "应用程序关闭时发生错误");
            }
            finally
            {
                Log.CloseAndFlush();
            }

            base.OnExit(e);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // 注册核心服务
            services.AddSingleton<IProcessService, ProcessService>();
            services.AddSingleton<IMemoryService, MemoryService>();

            // 注册视图模型
            services.AddTransient<MainViewModel>();

            // 添加日志
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog();
            });
        }
    }
}
