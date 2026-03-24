# PLC Injector Pro v1.3 — C# WinForms 桌面版

PLC 数据 → Windows 界面自动注入工具（桌面版）

## 环境要求

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（编译用，运行时已内嵌）
- 浏览器注入功能需要 Playwright（见下方说明）

## 快速开始

### 编译运行
```
双击 build.bat
```
编译完成后可执行文件在 `dist\PlcInjector.exe`

### 开发模式直接运行
```
dotnet run
```

---

## 功能说明

### 规则列表
| 操作 | 说明 |
|------|------|
| ＋ 新建规则 | 创建注入规则 |
| ✎ 编辑规则 | 双击规则也可编辑 |
| ▶ 全部启动 | 启动所有已启用规则的轮询 |
| 📡 Ping PLC | 单独测试 PLC 连通性，不注入 |
| ⚡ 测试注入 | 单次读取并注入，验证配置 |

### 注入目标类型

#### 🖱 Screen — 屏幕坐标点击（推荐）
完全绕过控件识别，直接在指定像素坐标点击后输入文字。
- 支持单击 / 双击 / 右击
- 点击后全选清空原有内容（Ctrl+A）
- 使用 Unicode SendInput，支持中文/特殊字符
- 可选注入后恢复鼠标原位
- 编辑规则时点击「📍 3秒后捕获坐标」自动填入

#### 🌐 Browser — 浏览器 Playwright
需要提前安装 Playwright：
```powershell
# 在项目目录执行
dotnet add package Microsoft.Playwright
dotnet build
.\bin\Debug\net8.0-windows\playwright.ps1 install chromium
```

Chrome 开启调试端口启动：
```
chrome.exe --remote-debugging-port=9222
```

连接方式：
- `active_tab` — 自动扫描 9222-9224 端口
- `cdp` — 指定 CDP 调试端口
- `new_window` — 启动新浏览器窗口

#### 🪟 Window — Win32 窗口
通过进程名或窗口标题定位，发送 WM_SETTEXT 消息注入。适合旧式 Win32 程序。

---

### UpdateFlag 握手协议
复刻 PIT.exe 的握手机制：

```
PC 轮询 → 读 DM0201 → 值=1？→ 读 DM0202（数据）→ 注入 → 写 DM0201=0（ACK）
```

寄存器分配建议（KV-8000）：
| 地址 | 用途 |
|------|------|
| DM0200 | 心跳（PLC 定时置1，PC 读后清零） |
| DM0201 | UpdateFlag（PLC 有新数据时置1） |
| DM0202 起 | 实际数据 |

---

### KV-8000 Modbus 地址格式
| 格式 | 类型 | 说明 |
|------|------|------|
| `DM0100` | 数据内存 | Holding Register |
| `W0010`  | 工作区   | Holding Register |
| `MR000`  | 内部继电器 | Coil |
| `TN001`  | 定时器当前值 | Holding Register |
| `CN001`  | 计数器当前值 | Holding Register |

---

### 值变换示例
| 表达式 | 效果 |
|--------|------|
| （留空）| 原始整数值 |
| `{value:F2}` | 保留两位小数 |
| `{value:D6}` | 补零到6位 |
| `Math.Round(value,2)` | 四舍五入 |

---

### 系统托盘
关闭窗口后程序在后台运行，托盘图标双击可恢复。
右键托盘图标可快速控制启停或退出。

---

## 项目结构

```
PlcInjector/
├── Program.cs                   # 入口
├── PlcInjector.csproj           # 项目文件
├── build.bat                    # 一键编译脚本
├── config/
│   └── rules.json               # 规则配置（自动保存）
├── logs/                        # 运行日志（待实现）
└── src/
    ├── Models/Models.cs          # 数据模型
    ├── Plc/PlcClients.cs         # PLC 客户端（KV-8000 / Mock）
    ├── Injection/Injectors.cs    # 注入引擎
    ├── Core/RuleEngine.cs        # 规则调度器
    ├── Core/ConfigStore.cs       # 配置读写
    └── UI/
        ├── MainForm.cs           # 主窗口
        └── RuleEditDialog.cs     # 规则编辑对话框
```
