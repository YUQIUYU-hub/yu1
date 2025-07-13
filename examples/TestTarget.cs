using System;
using System.Threading;

namespace MemoryHook.Examples
{
    /// <summary>
    /// 测试目标程序 - 用于演示MemoryHook的功能
    /// </summary>
    class TestTarget
    {
        // 测试变量 - 不同数据类型
        private static int _intValue = 12345;
        private static float _floatValue = 3.14159f;
        private static double _doubleValue = 2.71828;
        private static string _stringValue = "Hello MemoryHook!";
        private static byte[] _byteArray = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // 计数器
        private static int _counter = 0;

        static void Main(string[] args)
        {
            Console.WriteLine("=== MemoryHook 测试目标程序 ===");
            Console.WriteLine($"进程ID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
            Console.WriteLine($"架构: {(Environment.Is64BitProcess ? "64位" : "32位")}");
            Console.WriteLine();

            // 显示变量地址信息
            unsafe
            {
                fixed (int* intPtr = &_intValue)
                fixed (float* floatPtr = &_floatValue)
                fixed (double* doublePtr = &_doubleValue)
                {
                    Console.WriteLine("=== 内存地址信息 ===");
                    Console.WriteLine($"Int32 值: {_intValue}, 地址: 0x{(IntPtr)intPtr:X}");
                    Console.WriteLine($"Float 值: {_floatValue}, 地址: 0x{(IntPtr)floatPtr:X}");
                    Console.WriteLine($"Double 值: {_doubleValue}, 地址: 0x{(IntPtr)doublePtr:X}");
                    Console.WriteLine($"String 值: \"{_stringValue}\"");
                    Console.WriteLine($"ByteArray: [{string.Join(", ", _byteArray)}]");
                    Console.WriteLine();
                }
            }

            Console.WriteLine("=== 使用说明 ===");
            Console.WriteLine("1. 使用MemoryHook连接到此进程");
            Console.WriteLine("2. 尝试读取上述地址的内存值");
            Console.WriteLine("3. 修改内存值并观察变化");
            Console.WriteLine("4. 使用内存搜索功能查找特定值");
            Console.WriteLine();

            Console.WriteLine("程序运行中... 按 Ctrl+C 退出");
            Console.WriteLine("每5秒显示一次当前值:");
            Console.WriteLine();

            // 主循环 - 定期显示当前值
            while (true)
            {
                try
                {
                    _counter++;
                    
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 循环 #{_counter}");
                    Console.WriteLine($"  Int32: {_intValue}");
                    Console.WriteLine($"  Float: {_floatValue:F5}");
                    Console.WriteLine($"  Double: {_doubleValue:F5}");
                    Console.WriteLine($"  String: \"{_stringValue}\"");
                    Console.WriteLine($"  ByteArray: [{string.Join(", ", _byteArray)}]");
                    Console.WriteLine();

                    // 模拟一些内存变化
                    if (_counter % 10 == 0)
                    {
                        _intValue += 100;
                        _floatValue += 0.1f;
                        _doubleValue += 0.01;
                        Console.WriteLine("  >>> 自动增加了一些值 <<<");
                        Console.WriteLine();
                    }

                    Thread.Sleep(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"错误: {ex.Message}");
                    break;
                }
            }
        }
    }
}

/*
编译和运行说明:

1. 编译程序:
   csc TestTarget.cs /unsafe

2. 运行程序:
   TestTarget.exe

3. 使用MemoryHook测试:
   - 启动MemoryHook工具
   - 在进程列表中找到TestTarget进程
   - 连接到该进程
   - 使用显示的内存地址进行读写测试
   - 尝试搜索特定的值（如12345、3.14159等）

4. 测试场景:
   a) 基本读写测试:
      - 读取Int32地址的值，应该显示12345
      - 修改为其他值，观察控制台输出变化
      
   b) 浮点数测试:
      - 读取Float地址的值，应该显示3.14159
      - 修改为其他浮点数值
      
   c) 字符串测试:
      - 搜索"Hello MemoryHook!"字符串
      - 尝试修改字符串内容
      
   d) 内存搜索测试:
      - 搜索值12345，应该找到Int32变量
      - 搜索值3.14159，应该找到Float变量
      - 搜索字节序列01 02 03 04 05

5. 预期结果:
   - 成功连接到进程
   - 能够读取正确的内存值
   - 修改内存后控制台显示更新的值
   - 搜索功能能找到对应的内存地址
*/
