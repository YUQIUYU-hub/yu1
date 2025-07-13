# MemoryHook 构建脚本
# 用于自动化编译和打包项目

param(
    [string]$Configuration = "Release",
    [string]$Platform = "Any CPU",
    [switch]$Clean,
    [switch]$Restore,
    [switch]$Build,
    [switch]$Package,
    [switch]$All
)

# 设置错误处理
$ErrorActionPreference = "Stop"

# 项目路径
$SolutionFile = "MemoryHook.sln"
$OutputDir = "bin"
$PackageDir = "package"

Write-Host "=== MemoryHook 构建脚本 ===" -ForegroundColor Green
Write-Host "配置: $Configuration" -ForegroundColor Yellow
Write-Host "平台: $Platform" -ForegroundColor Yellow
Write-Host ""

# 检查.NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "检测到 .NET SDK 版本: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Error ".NET SDK 未安装或不在PATH中"
    exit 1
}

# 检查解决方案文件
if (-not (Test-Path $SolutionFile)) {
    Write-Error "找不到解决方案文件: $SolutionFile"
    exit 1
}

# 清理
if ($Clean -or $All) {
    Write-Host "正在清理项目..." -ForegroundColor Yellow
    
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
        Write-Host "已删除输出目录: $OutputDir" -ForegroundColor Green
    }
    
    if (Test-Path $PackageDir) {
        Remove-Item $PackageDir -Recurse -Force
        Write-Host "已删除打包目录: $PackageDir" -ForegroundColor Green
    }
    
    dotnet clean $SolutionFile --configuration $Configuration
    Write-Host "项目清理完成" -ForegroundColor Green
    Write-Host ""
}

# 还原依赖
if ($Restore -or $All) {
    Write-Host "正在还原NuGet包..." -ForegroundColor Yellow
    dotnet restore $SolutionFile
    Write-Host "依赖还原完成" -ForegroundColor Green
    Write-Host ""
}

# 编译
if ($Build -or $All) {
    Write-Host "正在编译项目..." -ForegroundColor Yellow
    
    # 编译解决方案
    dotnet build $SolutionFile --configuration $Configuration --no-restore
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "编译失败"
        exit 1
    }
    
    Write-Host "编译完成" -ForegroundColor Green
    Write-Host ""
}

# 打包
if ($Package -or $All) {
    Write-Host "正在打包应用程序..." -ForegroundColor Yellow
    
    # 创建打包目录
    if (-not (Test-Path $PackageDir)) {
        New-Item -ItemType Directory -Path $PackageDir | Out-Null
    }
    
    # 发布主应用程序
    $PublishDir = "$PackageDir\MemoryHook"
    dotnet publish "src\MemoryHook.UI\MemoryHook.UI.csproj" `
        --configuration $Configuration `
        --output $PublishDir `
        --self-contained false `
        --no-restore
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "发布失败"
        exit 1
    }
    
    # 复制示例文件
    $ExamplesDir = "$PackageDir\Examples"
    if (-not (Test-Path $ExamplesDir)) {
        New-Item -ItemType Directory -Path $ExamplesDir | Out-Null
    }
    
    if (Test-Path "examples") {
        Copy-Item "examples\*" $ExamplesDir -Recurse -Force
    }
    
    # 复制文档
    Copy-Item "README.md" $PackageDir -Force
    
    if (Test-Path "LICENSE") {
        Copy-Item "LICENSE" $PackageDir -Force
    }
    
    # 创建启动脚本
    $StartScript = @"
@echo off
echo 正在启动 MemoryHook...
echo 注意: 需要管理员权限才能正常工作
echo.

REM 检查管理员权限
net session >nul 2>&1
if %errorLevel% == 0 (
    echo 检测到管理员权限，正在启动...
    MemoryHook.UI.exe
) else (
    echo 警告: 未检测到管理员权限
    echo 某些功能可能无法正常工作
    echo.
    choice /C YN /M "是否继续启动"
    if errorlevel 2 goto :end
    MemoryHook.UI.exe
)

:end
pause
"@
    
    $StartScript | Out-File -FilePath "$PackageDir\启动MemoryHook.bat" -Encoding ASCII
    
    # 创建压缩包
    $ZipFile = "$PackageDir\..\MemoryHook-$Configuration.zip"
    if (Test-Path $ZipFile) {
        Remove-Item $ZipFile -Force
    }
    
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($PackageDir, $ZipFile)
    
    Write-Host "打包完成" -ForegroundColor Green
    Write-Host "输出目录: $PackageDir" -ForegroundColor Yellow
    Write-Host "压缩包: $ZipFile" -ForegroundColor Yellow
    Write-Host ""
}

# 显示完成信息
Write-Host "=== 构建完成 ===" -ForegroundColor Green

if ($All) {
    Write-Host "所有操作已完成:" -ForegroundColor Yellow
    Write-Host "  ✓ 清理项目" -ForegroundColor Green
    Write-Host "  ✓ 还原依赖" -ForegroundColor Green
    Write-Host "  ✓ 编译项目" -ForegroundColor Green
    Write-Host "  ✓ 打包应用" -ForegroundColor Green
    Write-Host ""
    Write-Host "可以在以下位置找到输出文件:" -ForegroundColor Yellow
    Write-Host "  应用程序: $PackageDir\MemoryHook\" -ForegroundColor Cyan
    Write-Host "  示例代码: $PackageDir\Examples\" -ForegroundColor Cyan
    Write-Host "  压缩包: MemoryHook-$Configuration.zip" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "使用说明:" -ForegroundColor Yellow
Write-Host "  .\build.ps1 -All          # 执行所有操作"
Write-Host "  .\build.ps1 -Clean        # 仅清理"
Write-Host "  .\build.ps1 -Restore      # 仅还原依赖"
Write-Host "  .\build.ps1 -Build        # 仅编译"
Write-Host "  .\build.ps1 -Package      # 仅打包"
