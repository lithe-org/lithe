# Lithe

<p align="left">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?style=flat-square&logo=windows" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 10">
  <img src="https://img.shields.io/badge/UI-WinUI%203-0078D4?style=flat-square" alt="WinUI 3">
  <img src="https://img.shields.io/badge/License-GPL%20v3-blue?style=flat-square" alt="License">
  <img src="https://img.shields.io/badge/Status-Alpha-orange?style=flat-square" alt="Status">
</p>

> 面向 Windows 的现代化桌面美化与增强工具。以极致的性能、高度的自定义和统一的 UI 设计，重塑你的桌面体验。

## 简介

Lithe 旨在解决当前 Windows 桌面美化工具普遍存在的资源占用高、UI 割裂、功能拼凑以及自定义程度低的问题。我们采用完全原生的 **WinUI 3 + DirectComposition** 技术栈，为你提供一个统一的桌面增强平台。

> **注意**：Lithe 目前处于早期开发阶段 (Alpha)，功能正在逐步完善中，暂不建议在生产环境中日常使用。

## 特性

### 🚀 极致轻量与原生
- 基于 C# 与 .NET 10 构建，深度适配 NativeAOT 编译。
- GPU 加速合成渲染，动画流畅且极低资源占用。
- 不属于 Electron 或 Web 套壳，纯粹的 Windows 原生体验。

### 🎨 统一的设计语言
- 消除传统美化工具“各个模块各管各的”视觉割裂感。
- Dock、状态栏、通知中心与桌面小组件共享同一套变量（圆角、亚克力材质、色彩与动画曲线）。

### 🖥️ 桌面接管
- **macOS 风格 Dock**：替换原生任务栏，支持应用分组、悬停放大动画、多窗口缩略图预览与 Jump List。
- **顶部状态栏**：完美接管系统托盘、网络、音量、电池及媒体控制，支持完整拖拽排序与自定义。

### 🧩 模块化与网格布局
- 任意添加和组合桌面小组件（时钟、天气、系统监控等）。
- 响应式网格吸附系统，支持自由缩放，自动适配不同分辨率。

### 🔔 聚合通知中心
- 接管第三方应用通知，支持“转为待办”、历史回溯与多维筛选。
- 告别 Windows 原生那“反人类”的通知体系。

## 系统要求

- **操作系统**：Windows 10 1809 (Build 17763) 或更高版本（推荐 Windows 11）。
- **运行环境**：安装包将自动配置所需环境；开发环境需安装 .NET 10 SDK 和 Windows App SDK。
- **开发IDE**：推荐使用 JetBrains Rider 或 Visual Studio 2022。

## 快速开始（面向开发者）

```bash
# 克隆仓库
git clone https://github.com/lithe-org/lithe.git
cd lithe

# 恢复依赖并构建
dotnet build

# 运行测试（可选）
dotnet test

# 运行应用（建议不要直接使用 IDE 的运行按钮）
dotnet run --project src/Lithe.App
```

## 参与贡献

Lithe 目前处于快速迭代期，非常欢迎各类贡献。
在提交代码之前，请务必阅读我们的开发指导文档 [DEV.md](./DEV.md)，其中包含了详细的**架构说明、里程碑排期以及严格的 Git 提交与发版规范**。

- 如果你发现了 Bug，请在 [Issues](https://github.com/lithe-org/lithe/issues) 中反馈。
- 如果你有好的想法，欢迎在 Issues 中发起讨论。

## 开源协议

Lithe 核心程序采用 **GPL v3** 协议开源。
详细的许可信息请参阅 [LICENSE](./LICENSE) 文件。

---
*Designed with ❤️ for Windows users.*