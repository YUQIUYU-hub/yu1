@echo off
chcp 65001 >nul
title MemoryHook - Windows内存修改工具

echo.
echo ========================================
echo    MemoryHook - Windows内存修改工具
echo ========================================
echo.

REM 检查管理员权限
net session >nul 2>&1
if %errorLevel% == 0 (
    echo [✓] 检测到管理员权限
    echo [INFO] 正在启动MemoryHook...
    echo.
    
    REM 启动主程序
    start "" "src\MemoryHook.UI\bin\Release\net8.0-windows\MemoryHook.UI.exe"
    
    echo [✓] MemoryHook已启动
    echo.
    echo 提示：
    echo - 如需测试，可运行 examples\bin\Release\net8.0\TestTarget.exe
    echo - 详细使用说明请查看 README.md 和 USAGE.md
    echo.
) else (
    echo [!] 警告: 未检测到管理员权限
    echo [!] MemoryHook需要管理员权限才能正常工作
    echo.
    choice /C YN /M "是否继续启动 (某些功能可能无法使用)"
    if errorlevel 2 goto :end
    
    echo [INFO] 正在启动MemoryHook (受限模式)...
    start "" "src\MemoryHook.UI\bin\Release\net8.0-windows\MemoryHook.UI.exe"
    echo [✓] MemoryHook已启动 (受限模式)
    echo.
)

echo 按任意键退出...
pause >nul
goto :end

:end
exit /b 0
