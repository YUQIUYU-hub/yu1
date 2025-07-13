using System;
using System.Diagnostics;
using System.Linq;

namespace ProcessNameTest
{
    /// <summary>
    /// 测试进程名称获取功能
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== 进程名称获取测试 ===");
            Console.WriteLine();

            try
            {
                // 获取所有进程
                var processes = Process.GetProcesses();
                Console.WriteLine($"系统中共有 {processes.Length} 个进程");
                Console.WriteLine();

                // 统计进程名称获取情况
                int successCount = 0;
                int failureCount = 0;
                int emptyNameCount = 0;

                Console.WriteLine("进程名称获取测试结果:");
                Console.WriteLine("PID\t进程名称\t\t状态");
                Console.WriteLine("".PadRight(50, '-'));

                foreach (var process in processes.Take(20)) // 只显示前20个进程
                {
                    try
                    {
                        string processName = "";
                        string status = "";

                        // 检查进程是否已退出
                        if (process.HasExited)
                        {
                            status = "已退出";
                            failureCount++;
                        }
                        else
                        {
                            try
                            {
                                processName = process.ProcessName ?? "";
                                
                                if (string.IsNullOrEmpty(processName))
                                {
                                    // 尝试从文件路径获取
                                    try
                                    {
                                        var fileName = process.MainModule?.FileName;
                                        if (!string.IsNullOrEmpty(fileName))
                                        {
                                            processName = System.IO.Path.GetFileNameWithoutExtension(fileName);
                                            status = "从路径获取";
                                        }
                                        else
                                        {
                                            processName = $"进程_{process.Id}";
                                            status = "使用默认名称";
                                            emptyNameCount++;
                                        }
                                    }
                                    catch
                                    {
                                        processName = $"进程_{process.Id}";
                                        status = "使用默认名称";
                                        emptyNameCount++;
                                    }
                                }
                                else
                                {
                                    status = "成功";
                                    successCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                processName = $"进程_{process.Id}";
                                status = $"异常: {ex.GetType().Name}";
                                failureCount++;
                            }
                        }

                        Console.WriteLine($"{process.Id}\t{processName.PadRight(20)}\t{status}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{process.Id}\t{"无法访问".PadRight(20)}\t异常: {ex.GetType().Name}");
                        failureCount++;
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

                Console.WriteLine("".PadRight(50, '-'));
                Console.WriteLine($"统计结果:");
                Console.WriteLine($"  成功获取: {successCount}");
                Console.WriteLine($"  空名称: {emptyNameCount}");
                Console.WriteLine($"  失败: {failureCount}");
                Console.WriteLine($"  总计: {successCount + emptyNameCount + failureCount}");

                // 测试特定进程
                Console.WriteLine();
                Console.WriteLine("=== 测试当前进程 ===");
                var currentProcess = Process.GetCurrentProcess();
                Console.WriteLine($"当前进程ID: {currentProcess.Id}");
                Console.WriteLine($"当前进程名称: {currentProcess.ProcessName}");
                Console.WriteLine($"当前进程路径: {currentProcess.MainModule?.FileName ?? "无法获取"}");
                Console.WriteLine($"当前进程窗口标题: {currentProcess.MainWindowTitle ?? "无"}");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试过程中发生错误: {ex.Message}");
                Console.WriteLine($"错误类型: {ex.GetType().Name}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }

            Console.WriteLine();
            Console.WriteLine("测试完成，按任意键退出...");
            Console.ReadKey();
        }
    }
}
