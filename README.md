# Lithe

![Tauri](https://img.shields.io/badge/Tauri-2.x-24C8DB?logo=tauri&logoColor=white&style=flat-square)
![React](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=111&style=flat-square)
![TypeScript](https://img.shields.io/badge/TypeScript-5.x-3178C6?logo=typescript&logoColor=white&style=flat-square)
![Rust](https://img.shields.io/badge/Rust-1.90%2B-000000?logo=rust&logoColor=white&style=flat-square)
![License](https://img.shields.io/github/license/lithe-org/lithe?style=flat-square&color=22c55e)
![Release](https://img.shields.io/github/v/release/lithe-org/lithe?style=flat-square)

**Windows 11 的 macOS 化顶部状态栏。轻量、原生、可定制。**

Lithe 是一个常驻屏幕顶部的状态栏应用，为 Windows 11 带来 macOS 风格的全局菜单栏体验。它不注入系统进程，不修改 explorer.exe，所有能力均通过公开的 Win32 / DWM 接口实现。

---

## 功能

- **全局状态栏**：在所有显示器顶部常驻显示，通过 AppBar 机制预留工作区，最大化窗口自动避让
- **Logo 菜单**：系统信息、设置、锁定、睡眠、重启、关机、任务管理器，以及 Lithe 自身的设置与关于入口
- **前台应用名**：实时显示当前焦点应用名称，浏览器显示应用名而非网页标题
- **窗口控制**：置顶、最大化/还原、最小化、分屏布局选择，作用于点击瞬间的前台窗口
- **系统状态**：资源监控、Wi-Fi、音量、电池、通知入口、时间日期
- **控制中心面板**：点击任一状态图标展开统一面板，聚焦对应模块
- **全屏自动隐藏**：检测到全屏应用时隐藏状态栏并注销 AppBar，退出后恢复
- **主题与定制**：亮色/暗色/跟随系统，自定义强调色，中英双语
- **自动更新**：基于 Tauri updater，多镜像拉取版本清单，签名校验后静默升级

## 技术栈

| 层级 | 选型 |
|---|---|
| 桌面框架 | Tauri v2 |
| 前端 | React 19 + TypeScript |
| 状态管理 | Zustand |
| 样式 | Tailwind CSS |
| 图标 | lucide-react |
| 后端 | Rust（Win32 / DWM / WUAPI） |
| 通信 | Tauri invoke + event |

## 系统要求

- **操作系统**：Windows 11（Build 22000 及以上）
- **架构**：x86-64
- **运行时**：WebView2 Runtime（Windows 11 通常已预装）

> Windows 10 及以下版本不受支持。

## 快速开始

### 环境准备

- Node.js 22+
- pnpm
- Rust 1.90+
- Microsoft C++ Build Tools（含 Windows SDK）

### 开发

```bash
# 克隆仓库
git clone https://github.com/lithe-org/lithe.git
cd lithe

# 安装前端依赖
pnpm install

# 安装 Rust 依赖（如尚未安装）
cd src-tauri && cargo fetch && cd ..

# 启动开发模式（Vite 端口 1422）
pnpm tauri dev
```

### 构建

```bash
pnpm tauri build
```

构建产物位于 `src-tauri/target/release/bundle/`。安装包格式由 Tauri bundle 配置决定，同时生成签名更新产物。

## 发布

安装包与更新产物作为 GitHub Release 附件发布。仓库内 `release/latest.json` 为版本清单，自动更新链路从该文件拉取版本信息与下载地址。

## 项目结构（节选）

```
app/
├── src/                  # React 前端
│   ├── components/       # UI 组件
│   ├── stores/           # Zustand 状态
│   ├── locales/          # i18n 文案
│   └── styles/           # Tailwind 与主题
├── src-tauri/            # Rust 后端
│   ├── src/
│   │   ├── window/       # 窗口管理
│   │   ├── appbar/       # AppBar 注册
│   │   ├── status/       # 系统状态采集
│   │   └── updater/      # 更新链路
│   └── tauri.conf.json
├── release/
│   └── latest.json       # 版本清单
└── package.json
```

## 许可证

GPL v3。完整条款见 [LICENSE](./LICENSE)。

## 链接

- 组织：https://github.com/lithe-org
- 仓库：https://github.com/lithe-org/lithe
- 问题反馈：https://github.com/lithe-org/lithe/issues