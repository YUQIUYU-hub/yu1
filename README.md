# MemoryHook - Windows内存修改工具

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)

基于EasyHook技术和.NET 8的现代化Windows内存修改工具，提供美观的WPF界面和强大的内存操作功能。

## ✨ 主要特性

### 🎯 核心功能
- **进程管理**: 实时显示系统进程列表，支持搜索和筛选
- **内存读写**: 支持多种数据类型的内存读取和写入操作
- **内存搜索**: 强大的内存搜索功能，支持值搜索和模式匹配
- **内存监控**: 实时监控内存变化和进程状态
- **数据备份**: 自动备份原始内存值，支持一键恢复

### 🎨 用户界面
- **Material Design**: 采用现代化Material Design设计风格
- **响应式布局**: 自适应窗口大小，支持多分辨率显示
- **实时更新**: 进程列表和状态信息实时刷新
- **直观操作**: 简洁明了的操作界面，易于使用

### 🔧 技术特性
- **架构兼容**: 自动检测并支持32位和64位进程
- **权限管理**: 智能权限检查和管理员权限提升
- **错误处理**: 完善的错误处理和用户反馈机制
- **安全保护**: 进程保护检测，避免操作系统关键进程

## 🚀 快速开始

### 系统要求

- **操作系统**: Windows 7 SP1 或更高版本
- **运行时**: .NET 8.0 Runtime
- **权限**: 管理员权限（用于内存操作）
- **架构**: 支持 x86 和 x64

### 安装步骤

1. **下载发布版本**
   ```bash
   # 从 GitHub Releases 下载最新版本
   # 或者克隆源代码自行编译
   git clone https://github.com/your-repo/MemoryHook.git
   ```

2. **编译项目**
   ```bash
   cd MemoryHook
   dotnet restore
   dotnet build --configuration Release
   ```

3. **运行应用程序**
   ```bash
   # 以管理员身份运行
   cd src/MemoryHook.UI/bin/Release/net8.0-windows
   MemoryHook.UI.exe
   ```

## 📖 使用指南

### 基本操作流程

1. **启动应用程序**
   - 以管理员身份运行 MemoryHook.UI.exe
   - 应用程序将自动加载系统进程列表

2. **选择目标进程**
   - 在左侧进程列表中浏览或搜索目标进程
   - 点击进程名称选中目标进程
   - 查看进程详细信息（PID、架构、内存使用量等）

3. **连接到进程**
   - 选择目标进程后，点击"连接"按钮
   - 系统将尝试获取进程访问权限
   - 连接成功后状态栏显示"已连接"

4. **内存操作**
   - 切换到"内存读写"选项卡
   - 输入内存地址（支持十六进制格式，如 0x12345678）
   - 选择数据类型（Int32、Float、String等）
   - 点击"读取"查看当前值
   - 输入新值后点击"写入"修改内存

### 高级功能

#### 内存搜索
1. 切换到"内存搜索"选项卡
2. 输入要搜索的值
3. 选择数据类型
4. 点击"开始搜索"
5. 在结果列表中双击选择目标地址

#### 内存区域浏览
1. 切换到"内存区域"选项卡
2. 查看进程的所有内存区域
3. 了解内存保护属性和状态信息

## 🛠️ 项目结构

```
MemoryHook/
├── src/
│   ├── MemoryHook.Core/          # 核心库
│   │   ├── Interfaces/           # 服务接口定义
│   │   ├── Models/              # 数据模型
│   │   └── Services/            # 服务实现
│   ├── MemoryHook.UI/           # WPF用户界面
│   │   ├── ViewModels/          # MVVM视图模型
│   │   ├── Views/               # 视图文件
│   │   └── Controls/            # 自定义控件
│   └── MemoryHook.Injector/     # EasyHook注入器
├── docs/                        # 文档目录
├── tests/                       # 测试项目
└── README.md                    # 项目说明
```

## 🔧 开发指南

### 开发环境设置

1. **安装开发工具**
   - Visual Studio 2022 或 Visual Studio Code
   - .NET 8.0 SDK
   - Git

2. **克隆项目**
   ```bash
   git clone https://github.com/your-repo/MemoryHook.git
   cd MemoryHook
   ```

3. **还原依赖**
   ```bash
   dotnet restore
   ```

4. **编译项目**
   ```bash
   dotnet build
   ```

### 核心组件说明

#### IProcessService
进程管理服务，负责：
- 获取系统进程列表
- 进程信息查询和监控
- 进程访问权限检查
- 架构兼容性检测

#### IMemoryService  
内存操作服务，负责：
- 进程内存连接管理
- 内存读写操作
- 内存搜索功能
- 内存区域信息获取

#### MainViewModel
主视图模型，实现：
- MVVM模式的数据绑定
- 用户界面逻辑处理
- 异步操作管理
- 事件处理和状态更新

## ⚠️ 安全注意事项

### 使用警告
- **管理员权限**: 本工具需要管理员权限才能正常工作
- **系统稳定性**: 修改系统进程内存可能导致系统不稳定
- **数据安全**: 请在修改前备份重要数据
- **合法使用**: 仅用于合法的调试和研究目的

### 安全特性
- **进程保护检测**: 自动识别并避免操作系统关键进程
- **权限验证**: 操作前进行权限检查和验证
- **错误恢复**: 提供内存备份和恢复功能
- **操作日志**: 记录所有内存操作历史

## 🐛 故障排除

### 常见问题

**Q: 应用程序无法启动**
A: 确保以管理员身份运行，并检查是否安装了.NET 8.0 Runtime

**Q: 无法连接到目标进程**
A: 检查目标进程是否仍在运行，确认具有足够的访问权限

**Q: 内存读取失败**
A: 验证内存地址是否有效，检查目标进程的内存保护属性

**Q: 搜索结果为空**
A: 确认搜索值和数据类型正确，尝试调整搜索参数

### 日志文件
应用程序会在 `logs/` 目录下生成详细的日志文件，可用于问题诊断。

## 🤝 贡献指南

我们欢迎社区贡献！请遵循以下步骤：

1. Fork 项目仓库
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

### 代码规范
- 遵循 C# 编码规范
- 添加适当的注释和文档
- 编写单元测试
- 确保代码通过所有测试

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 🙏 致谢

- [EasyHook](https://easyhook.github.io/) - 强大的Windows API钩子库
- [MaterialDesignInXamlToolkit](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) - 美观的Material Design UI组件
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - 现代化的MVVM框架

## 📞 联系方式

- **项目主页**: https://github.com/your-repo/MemoryHook
- **问题反馈**: https://github.com/your-repo/MemoryHook/issues
- **邮箱**: your-email@example.com

---

**免责声明**: 本工具仅供学习和研究使用，使用者需自行承担使用风险。开发团队不对因使用本工具造成的任何损失负责。
