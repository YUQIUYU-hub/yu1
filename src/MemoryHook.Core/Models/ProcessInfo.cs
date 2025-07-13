using System;
using System.Diagnostics;

namespace MemoryHook.Core.Models
{
    /// <summary>
    /// 进程信息模型
    /// </summary>
    public class ProcessInfo
    {
        /// <summary>
        /// 进程ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// 进程名称
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// 进程标题
        /// </summary>
        public string WindowTitle { get; set; } = string.Empty;

        /// <summary>
        /// 进程架构 (x86/x64)
        /// </summary>
        public ProcessArchitecture Architecture { get; set; }

        /// <summary>
        /// 进程路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 是否为系统进程
        /// </summary>
        public bool IsSystemProcess { get; set; }

        /// <summary>
        /// 是否为保护进程
        /// </summary>
        public bool IsProtectedProcess { get; set; }

        /// <summary>
        /// 进程启动时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 内存使用量 (字节)
        /// </summary>
        public long MemoryUsage { get; set; }

        /// <summary>
        /// 是否可以访问
        /// </summary>
        public bool CanAccess { get; set; }

        /// <summary>
        /// 进程句柄
        /// </summary>
        public IntPtr Handle { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName => string.IsNullOrEmpty(WindowTitle) ? ProcessName : $"{ProcessName} - {WindowTitle}";

        /// <summary>
        /// 架构显示文本
        /// </summary>
        public string ArchitectureText => Architecture switch
        {
            ProcessArchitecture.x86 => "32位",
            ProcessArchitecture.x64 => "64位",
            _ => "未知"
        };

        /// <summary>
        /// 内存使用量显示文本
        /// </summary>
        public string MemoryUsageText => FormatBytes(MemoryUsage);

        /// <summary>
        /// 格式化字节数
        /// </summary>
        /// <param name="bytes">字节数</param>
        /// <returns>格式化后的字符串</returns>
        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }

        /// <summary>
        /// 从Process对象创建ProcessInfo
        /// </summary>
        /// <param name="process">Process对象</param>
        /// <returns>ProcessInfo实例</returns>
        public static ProcessInfo FromProcess(Process process)
        {
            var info = new ProcessInfo
            {
                ProcessId = process.Id
            };

            // 安全地获取进程名称
            try
            {
                info.ProcessName = process.ProcessName ?? string.Empty;

                // 如果进程名称为空，尝试从文件路径获取
                if (string.IsNullOrEmpty(info.ProcessName))
                {
                    try
                    {
                        var fileName = process.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            info.ProcessName = System.IO.Path.GetFileNameWithoutExtension(fileName);
                        }
                    }
                    catch
                    {
                        // 忽略获取文件名失败的异常
                    }
                }

                // 如果仍然为空，使用默认名称
                if (string.IsNullOrEmpty(info.ProcessName))
                {
                    info.ProcessName = $"进程_{process.Id}";
                }
            }
            catch (Exception)
            {
                // 如果无法获取进程名称，使用默认名称
                info.ProcessName = $"进程_{process.Id}";
            }

            // 安全地获取其他属性
            try
            {
                info.WindowTitle = process.MainWindowTitle ?? string.Empty;
            }
            catch
            {
                info.WindowTitle = string.Empty;
            }

            try
            {
                info.StartTime = process.StartTime;
            }
            catch
            {
                info.StartTime = DateTime.MinValue;
            }

            try
            {
                info.MemoryUsage = process.WorkingSet64;
            }
            catch
            {
                info.MemoryUsage = 0;
            }

            try
            {
                info.FilePath = process.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                info.FilePath = string.Empty;
            }

            return info;
        }
    }

    /// <summary>
    /// 进程架构枚举
    /// </summary>
    public enum ProcessArchitecture
    {
        /// <summary>
        /// 未知架构
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 32位进程
        /// </summary>
        x86 = 1,

        /// <summary>
        /// 64位进程
        /// </summary>
        x64 = 2
    }
}
