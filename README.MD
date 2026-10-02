# MultiDesktop

**为工作、学习和生活，切换不同的 Windows 桌面文件夹。**

[快速开始](#快速开始) · [命令行使用](#命令行使用) · [加密安全说明](#加密安全说明) · [源码构建](#源码构建) · [发行页面](https://github.com/Qibowen2008/MultiDesktop/releases)

> **项目定位：** MultiDesktop 切换的是当前用户的 Windows 桌面目录及可选壁纸，不是 `Win + Tab` 中的应用窗口工作区，也不是虚拟机或隔离沙箱。运行中的应用不会被隔离。

![多桌面切换主界面](pics/1.png)

一款基于 **C# / .NET 10 / Windows Forms** 的本地桌面管理工具。将工作文件、学习资料和娱乐快捷方式分别保存在不同文件夹，通过主窗口或系统托盘切换桌面内容，并为不同桌面配置独立壁纸。

切换优先使用 Win32 Shell API；失败时回退到修改注册表并重启资源管理器。添加配置不会自动迁移原桌面文件，公共桌面中的快捷方式仍可能显示。

> 支持 **桌面加密**：可以为任意桌面设置密码，切换时需输入密码解锁，离开时自动重新加密，保护你的私密文件。

---

## ✨ 功能特性

- **一键切换桌面**：通过 `SHSetKnownFolderPath` 无感切换桌面文件夹，explorer 自动刷新，无需重启
- **自定义壁纸**：每个桌面可设置独立的壁纸与显示方式（填充/适应/拉伸/平铺/居中/跨屏）
- **🔒 桌面加密**：为桌面设置密码后，文件夹被压缩加密（基于后量子密码学文件加密库），切换时输入密码解锁，离开时自动重新加密
- **系统托盘**：最小化到托盘，右键菜单快速切换桌面
- **CLI 支持**：添加 / 删除 / 列出桌面，设置壁纸与加密均可通过命令行完成
- **深色 / 浅色主题**：跟随系统或手动切换

---

## 快速开始

### 运行环境

- Windows 系统；项目目标框架为 `net10.0-windows`，不支持直接在 Linux 或 macOS 上运行。
- 依赖框架的发行版本需要 **.NET 10 Desktop Runtime**，从源码构建需要 **.NET 10 SDK**。
- 自包含或 NativeAOT 发行包的运行要求、支持架构和最低系统版本，以具体发行说明和测试结果为准。

### 安装

1. 从 [Releases](https://github.com/Qibowen2008/MultiDesktop/releases) 选择适合系统的发行包，按包内说明运行 `MultiDesktop.exe`。
2. 备份重要文件并记录原桌面目录。建议先创建一个非重要的测试文件夹体验功能。
3. 点击 **添加桌面**，填写唯一名称并选择已有文件夹，可选配置壁纸。
4. 保存后选中桌面，点击 **切换到选中桌面**，或从系统托盘菜单切换。
5. 将原桌面目录也保存为配置，方便切回。退出程序不会自动恢复原桌面。

首次使用时会按需创建配置。普通桌面切换后，原文件仍在原目录；删除桌面配置不等于删除文件，也不等于解除加密。

### 从源码构建

```bash
dotnet build src/MultiDesktop/MultiDesktop.csproj -c Release
```

---

## 🖥️ 界面预览

![主界面与添加桌面](pics/2.png)

![桌面加密设置](pics/3.png)

### 主窗口

- 桌面列表展示所有已配置的桌面，支持单选 / 多选
- 按钮：添加桌面、删除、切换到选中桌面、编辑选中桌面、设置、关于软件
- 加密桌面在列表中标记为"已加密"，切换时会要求输入密码

### 添加 / 编辑桌面

| 输入项 | 说明 |
|---|---|
| 桌面名称 | 唯一标识，不可重复 |
| 桌面路径 | 必须是已存在的文件夹 |
| 启用自定义壁纸 | 勾选后配置壁纸路径与显示方式 |
| 壁纸路径 | 支持 jpg / png / bmp / gif |
| 显示方式 | 填充 / 适应 / 拉伸 / 平铺 / 居中 / 跨屏 |
| 桌面加密设置 | 设置 / 修改 / 移除桌面密码 |

---

## 🔒 桌面加密

### 加密原理

1. 设置密码时，桌面文件夹先被压缩为 zip，再用 **后量子密码学文件加密**（`PostQuantum.FileEncryption`）加密为 `Zips\<id>.zip.encrypted`，然后删除原明文文件夹
2. 加密包 id 由桌面名称经 FNV-1a 哈希生成，删除或重排桌面不会导致 id 错位
3. 密码不写入桌面配置，但已验证的密码会缓存在本次会话的内存中，以便重新加密。正常密码校验通过实际解密完成，项目没有密码找回机制。

### 加密安全说明

> **加密会实际删除原明文文件夹，不只是锁定界面。请先备份，再用非重要数据测试。**

- **退出不等于锁定：** 当前退出逻辑不会自动重新加密正在使用的桌面。应先切换到普通桌面，确认重新加密成功后再退出。
- **解锁时文件为明文：** 有权限的其他进程仍可读取文件，不提供应用隔离。
- **重新加密可能失败：** 文件占用、权限不足或空间不足等异常可能让文件保持明文，应认真检查提示。
- **临时明文会落盘：** 压缩、解密与密码验证过程中会产生明文 ZIP 或临时解压目录，正常清理不等于安全擦除。
- **加密包不是版本备份：** 重新加密会替换旧包，应保留独立备份，不应宣称异常情况下绝不丢失数据。
- **谨慎改名：** 加密包 ID 依赖桌面名称。建议先移除加密，再修改名称，最后重新设置密码。
- **优先使用 GUI 输入密码：** 命令行密码可能出现在终端历史、脚本和进程参数中。

引用加密库不代表整个应用已通过安全审计。

### 加密流程（GUI）

1. 添加 / 编辑桌面窗口中点击 **"桌面加密设置"**
2. 首次设置：输入新密码与确认密码，点击确定
3. 修改密码：输入原密码 + 新密码（原密码错误会提示）
4. 移除加密：新密码留空，输入原密码即可
5. 切换到加密桌面时弹窗输入密码；离开已解锁的加密桌面时自动重新加密（会话内密码缓存在内存，退出程序后失效）

### 加密流程（CLI）

```bash
# 参数语法示意；请优先在图形界面设置密码
MultiDesktop --AddDesktop <名称> <已有文件夹路径> --SetPassword <密码>
```

---

## 💻 CLI 命令行

### 命令行使用

不提供任何参数时默认启动 GUI 模式。在 PowerShell 的程序目录下运行时，使用 `.\MultiDesktop.exe`。

```powershell
.\MultiDesktop.exe --help
.\MultiDesktop.exe --version
.\MultiDesktop.exe --AddDesktop "工作" "D:\WorkDesktop"
.\MultiDesktop.exe --ListDesktop
```

目标文件夹需事先存在。`--ListDesktop` 当前输出 XML 配置内容，而不是格式化表格。`--SetBack`、`--SetStyle`、`--SetPassword` 是 `--AddDesktop` 的附加参数，应放在名称和路径后面，不是独立编辑命令。建议每次调用执行一项操作。

**当前没有独立的 CLI 桌面切换命令，切换请使用主窗口或托盘菜单。**

| 参数 | 说明 | 示例 |
|---|---|---|
| `--help` / `-h` | 显示帮助信息并退出 | `MultiDesktop --help` |
| `--version` | 显示版本号并退出 | `MultiDesktop --version` |
| `--AddDesktop <名称> <路径>` | 添加桌面，可选 `--SetBack <壁纸>`、`--SetStyle <显示方式>`、`--SetPassword <密码>` | `MultiDesktop --AddDesktop "工作" "D:\WorkDesktop" --SetBack "D:\wall.jpg" --SetStyle 拉伸` |
| `--DeleteDesktop <名称>` | 删除指定桌面配置 | `MultiDesktop --DeleteDesktop "工作"` |
| `--ListDesktop` | 列出所有已配置桌面 | `MultiDesktop --ListDesktop` |
| `--ChangePassword <名称> <原密码> <新密码>` | 修改加密桌面的密码（原密码校验通过才执行） | `MultiDesktop --ChangePassword "私密" 123456 654321` |
| `--RemovePassword <名称> <密码>` | 移除桌面的加密保护（需密码验证） | `MultiDesktop --RemovePassword "私密" 123456` |

---

## ⚙️ 配置文件

配置文件位于程序同目录（不可写时回退到 `%AppData%\MultiDesktop`）。

### DesktopList.xml

存储所有桌面配置，主键为桌面名称：

| 字段 | 类型 | 说明 |
|---|---|---|
| `桌面名称` | string | 唯一标识，主键 |
| `桌面路径` | string | 桌面根目录文件夹路径 |
| `是否开启自定义壁纸` | bool | 切换到此桌面时是否设置壁纸 |
| `自定义壁纸地址` | string | 壁纸图片完整路径 |
| `壁纸显示方式` | string | 填充 / 适应 / 拉伸 / 平铺 / 居中 / 跨屏 |
| `是否加密` | bool | 该桌面是否已加密 |

### AppSettings.xml

| 键 | 类型 | 有效值 | 说明 |
|---|---|---|---|
| `Color` | int | `0`=跟随系统 / `1`=浅色 / `2`=深色 | 颜色模式 |
| `ExitMode` | int | `0`=询问 / `1`=最小化到后台 / `2`=退出程序 | 关闭窗口行为 |

---

## 🛠️ 技术实现

### 桌面切换

1. **优先方案**：`SHSetKnownFolderPath(FOLDERID_Desktop, ...)` —— 无感切换，无需重启 explorer
2. **回退方案**：修改注册表 `HKCU\...\Explorer\User Shell Folders\Desktop` 与 `Shell Folders\Desktop`，重启 explorer 生效

### 壁纸设置

通过 `SystemParametersInfo(SPI_SETDESKWALLPAPER, ...)` 设置壁纸，同步写入注册表 `HKCU\Control Panel\Desktop\WallpaperStyle` / `TileWallpaper`。

### 构建与发布

### 源码构建

当前项目配置：

| 组件 | 版本 / 用途 |
| --- | --- |
| C# / .NET | `net10.0-windows` |
| Windows Forms | 窗口与事件处理 |
| AntdUI | `2.4.4`，界面控件 |
| I18N.DotNet | `1.3.1`，国际化相关支持 |
| PostQuantum.FileEncryption | `1.6.0`，文件加解密 |
| 程序集版本 | `1.3.6.0` |

在 Windows 上安装 .NET 10 SDK，在仓库根目录执行：

```powershell
# 构建，默认自动还原依赖
 dotnet build src/MultiDesktop/MultiDesktop.csproj -c Release

# 启动 GUI；实际操作会修改桌面设置
 dotnet run --project src/MultiDesktop/MultiDesktop.csproj

# Windows x64 自包含发布示例
 dotnet publish src/MultiDesktop/MultiDesktop.csproj -c Release -r win-x64 --self-contained true -o publish/local-win-x64
```

常规 Release 输出目录为 `src/MultiDesktop/bin/Release/net10.0-windows/`。

`MultiDesktop.AOT.csproj` 中启用了 `PublishAot=true`。这是一份 NativeAOT 构建配置，不代表所有架构与功能均已通过验证；发布前应检查本机工具链、WinForms 与依赖库的裁剪兼容性，并测试窗口、XML 配置和加解密功能。

### 源码导航

| 文件（相对 `src/MultiDesktop/`） | 主要职责 |
| --- | --- |
| `Program.cs` | 启动、CLI、配置路径、桌面和壁纸切换、加解密 |
| `frmMain.cs` | 桌面列表、切换工作流与托盘交互 |
| `frmAddDesktop.cs` | 添加和编辑桌面 |
| `frmPassword.cs` | 设置、修改和移除密码 |
| `frmInputPassword.cs` | 解锁密码输入 |
| `frmSet.cs` / `frmClose.cs` | 设置与关闭行为 |
| `frmAbout.cs` | 关于窗口 |
| `*.Designer.cs` | 对应窗体布局 |

`Program.cs` 中的核心类包括 `AppPaths`、`DesktopManager`、`AppSettingsManager` 和 `EncryptManager`。

### 迁移与问题反馈

迁移时应同时保留 `DesktopList.xml`、`AppSettings.xml`、配置目录下的 `Zips/`，以及配置目录之外的桌面文件夹。仅复制 EXE 不会迁移数据，绝对路径也可能需要重新配置。

启动顶层异常时，程序会尝试在配置目录写入 `error.log`。欢迎通过 [Issues](https://github.com/Qibowen2008/MultiDesktop/issues) 提交系统版本、程序版本、复现步骤和脱敏后的错误信息。不要上传密码、私密文件或未经检查的配置与加密包。

---

## 📄 许可证

MIT License — 详见 [LICENSE](LICENSE)。

项目主页：<https://github.com/Qibowen2008/MultiDesktop>
