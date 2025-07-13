@echo off
chcp 65001 >nul
title TestTarget - MemoryHook测试目标程序

echo.
echo ========================================
echo    TestTarget - MemoryHook测试目标程序
echo ========================================
echo.

echo [INFO] 正在启动测试目标程序...
echo [INFO] 该程序将显示内存地址信息供MemoryHook测试使用
echo.

REM 启动测试程序
"examples\bin\Release\net8.0\TestTarget.exe"

echo.
echo [INFO] 测试程序已退出
pause
