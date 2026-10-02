---
name: multidesktop-manager
description: >
  Windows 多桌面切换管理工具 (MultiDesktop) 的技能。用于管理多个桌面配置、切换桌面文件夹路径、
  自定义各桌面壁纸、以及为桌面设置加密密码。当用户提及多桌面、虚拟桌面切换、桌面管理、桌面壁纸配置、
  Windows 桌面文件夹重定向、桌面加密时使用。支持满血 CLI 命令行模式（含 JSON 输出）与 GUI 图形界面模式。
license: MIT
compatibility: Requires Windows 10+ (build 14393+) and .NET 10.0
metadata:
  author: Buger (Buger2008)
  version: "1.3.6.0"
  repository: https://github.com/Qibowen2008/MultiDesktop
  platform: net10.0-windows
  ui-framework: AntdUI v2.4.4
  tags:
    - windows
    - desktop
    - virtual-desktop
    - wallpaper
    - encryption
    - shell-integration
    - cli
---

# MultiDesktop — Windows 多桌面切换管理

基于 .NET 10 的 Windows 多桌面管理工具，通过 Win32 Shell API 修改桌面文件夹路径，实现多个虚拟桌面之间的一键切换。

**GUI 与 CLI 共用同一套核心逻辑**（`Core/`），因此两边行为与提示一致。

---

## 给 AI 的使用建议

1. **优先使用 CLI 而非 GUI**：CLI 可用 `--json` 获得结构化结果，并能通过退出码判断失败原因。
2. **切换桌面是有副作用的操作**：会真实改变用户的 Windows 桌面目录，执行前应确认用户意图。
3. **加密会删除明文文件夹**：`password` 设置密码后原文件夹会被压缩加密并删除，属于破坏性操作，务必先与用户确认。
4. **避免把密码写在命令行**：优先用 `--password-stdin`，或省略密码让程序交互式询问。
5. **先读后写**：用 `list --json` 确认桌面名称与当前状态，再执行修改。

---

## CLI 命令行

不带任何参数时启动 GUI；带参数即进入 CLI 模式。

### 命令一览

| 命令 | 说明 |
|---|---|
| `list` | 列出所有已配置的桌面 |
| `add` | 添加桌面（可选壁纸与加密） |
| `remove` | 删除桌面配置（不删除文件夹，也不解除加密） |
| `switch` | 切换当前桌面文件夹 |
| `wallpaper` | 设置或取消已有桌面的自定义壁纸 |
| `password` | 设置 / 修改 / 移除桌面加密密码 |
| `settings` | 查看或修改应用设置 |
| `install-skills` | 安装 SKILL.md 并把程序目录加入 PATH |
| `help` / `version` | 帮助 / 版本号 |

`help --json` 会输出机器可读的完整命令表（含每个命令的选项与示例）。

### 全局选项

| 选项 | 说明 |
|---|---|
| `--json` | 以 JSON 输出结果 |
| `--quiet` | 成功时不输出提示 |
| `--no-input` | 禁止交互式输入，缺少密码时直接失败（退出码 `5`） |
| `--password-stdin` | 从标准输入读取一个密码 |
| `--help` / `-h` | 显示帮助 |

### 各命令参数

| 命令 | 用法 |
|---|---|
| `list` | `list [--json] [--xml] [--quiet]` |
| `add` | `add --name <名称> --path <路径> [--wallpaper <壁纸>] [--style <显示方式>] [--password <密码>]` |
| `remove` | `remove --name <名称>` |
| `switch` | `switch --name <名称> [--password <密码>] [--reencrypt-password <密码>] [--no-reencrypt]` |
| `wallpaper` | `wallpaper --name <名称> (--wallpaper <壁纸> [--style <显示方式>] \| --clear)` |
| `password` | `password --name <名称> (--new <新密码> [--old <原密码>] \| --remove --old <原密码>)` |
| `settings` | `settings [--color <模式>] [--exit-mode <行为>] [--json]` |

### 退出码

| 退出码 | 含义 |
|---|---|
| `0` | 成功 |
| `1` | 一般错误（IO 失败、加密失败等） |
| `2` | 参数用法错误 |
| `3` | 桌面不存在 |
| `4` | 密码错误 |
| `5` | 需要密码但禁止交互 |
| `6` | 用户取消 |

### 示例

```powershell
# 查看
MultiDesktop list
MultiDesktop list --json
MultiDesktop settings --json

# 添加
MultiDesktop add --name "工作" --path "D:\WorkDesktop"
MultiDesktop add --name "娱乐" --path "E:\Game" --wallpaper "D:\wall.jpg" --style 拉伸
MultiDesktop add --name "私密" --path "D:\Private" --password 123456

# 切换
MultiDesktop switch --name "工作"
MultiDesktop switch --name "私密" --password 123456

# 壁纸
MultiDesktop wallpaper --name "工作" --wallpaper "D:\wall.jpg" --style 适应
MultiDesktop wallpaper --name "工作" --clear

# 密码
MultiDesktop password --name "私密" --new 654321 --old 123456
MultiDesktop password --name "私密" --remove --old 654321

# 设置
MultiDesktop settings --color 深色 --exit-mode 询问
```

### `switch` 的行为说明

- 目标桌面未加密：直接切换目录并按配置应用壁纸。
- 目标桌面已加密：需要密码（`--password`、`--password-stdin` 或交互式输入）。密码错误退出码为 `4`。
- 目标桌面文件夹已存在（此前已解锁过）：先验证密码再切换，避免用旧压缩包覆盖桌面上的新文件。
- 目标桌面文件夹不存在：解密还原后再切换。
- 离开的加密桌面若处于已解密状态，会自动重新加密；CLI 每次都是新进程，会话密码缓存为空，因此需要 `--reencrypt-password` 提供密码。**未提供时不会阻塞切换**，但会输出警告说明该桌面文件暂为明文。用 `--no-reencrypt` 可显式跳过。
- 切换到当前正在使用的桌面时不会触发重新加密。

### JSON 输出格式

成功与失败统一为：

```json
{
  "success": true,
  "message": "已切换到桌面: 工作 -> D:\\WorkDesktop",
  "data": { "switched": "工作", "path": "D:\\WorkDesktop", "warning": null }
}
```

`list --json` 的结构：

```json
{
  "success": true,
  "count": 2,
  "desktops": [
    {
      "name": "工作",
      "path": "D:\\WorkDesktop",
      "enableWallpaper": false,
      "wallpaperPath": "",
      "wallpaperStyle": "填充",
      "encrypted": false
    }
  ]
}
```

---

## 配置文件

### AppSettings.xml（与程序同目录，首次运行自动创建）

| 键 | 类型 | 默认值 | 有效值 | 说明 |
|---|---|---|---|---|
| `Color` | int | `0` | `0`="跟随系统" / `1`="浅色" / `2`="深色" | 颜色模式，重启生效 |
| `ExitMode` | int | `0` | `0`="询问" / `1`="最小化到后台" / `2`="退出程序" | 关闭窗口行为 |

### DesktopList.xml（与程序同目录）

存储所有桌面配置，主键为桌面名称。每个桌面的字段：

| 字段 | 类型 | 必需 | 默认值 | 说明 |
|---|---|---|---|---|
| `桌面名称` | string | 是 | — | 唯一标识，主键；**重名会被拒绝** |
| `桌面路径` | string | 是 | — | 作为桌面根目录的文件夹路径 |
| `是否开启自定义壁纸` | bool | 否 | `False` | 切换到此桌面时是否设置壁纸 |
| `自定义壁纸地址` | string | 否 | `""` | 壁纸图片完整路径 |
| `壁纸显示方式` | string | 否 | `"填充"` | 壁纸模式（见下表） |
| `是否加密` | bool | 否 | `False` | 该桌面是否已设置加密密码 |

> 桌面名称必须唯一：加密包 id 由名称哈希生成，同名桌面会共用同一个加密包而互相覆盖。

### 壁纸显示方式

| 值 | 说明 | 注册表 WallpaperStyle | 注册表 TileWallpaper |
|---|---|---|---|
| `填充` | 填充模式 | `"10"` | `"0"` |
| `适应` | 适应模式 | `"6"` | `"0"` |
| `拉伸` | 拉伸模式 | `"2"` | `"0"` |
| `平铺` | 平铺模式 | `"0"` | `"1"` |
| `居中` | 居中模式 | `"0"` | `"0"` |
| `跨屏` | 跨屏模式 | `"22"` | `"0"` |

壁纸支持格式：`.jpg` / `.jpeg` / `.png` / `.bmp` / `.gif`

---

## GUI 界面操作

### 主窗口（多桌面切换）

- 桌面列表展示，支持单选/多选操作
- 按钮：添加桌面、删除桌面、编辑桌面、切换到选中桌面、设置、关于
- 系统托盘最小化，右键菜单快速切换桌面

### 添加/编辑桌面窗口

| 输入项 | 控件 | 说明 |
|---|---|---|
| 桌面名称 | 文本框 | 唯一标识，不可重复 |
| 桌面路径 | 文本框 + 文件夹浏览按钮 | 必须为已存在的文件夹 |
| 启用自定义壁纸 | 复选框 | 勾选后显示壁纸配置 |
| 壁纸路径 | 文本框 + 文件浏览按钮 | 图片格式限制：jpg/png/bmp/gif |
| 显示方式 | 下拉框 | 填充/适应/拉伸/平铺/居中/跨屏 |
| 桌面加密设置 | 按钮 | 打开密码设置对话框（见下节） |

编辑模式下自动预填充已有值。

### 桌面加密（密码设置对话框）

点击"桌面加密设置"按钮打开密码对话框，支持三种操作：

- **首次设置密码**：只填写新密码与确认密码（原密码一栏自动隐藏），保存后桌面文件夹被压缩并加密（`Zips\<id>.zip.encrypted`），原明文文件夹被删除。
- **修改密码**：需先输入原密码（程序通过实际解密验证，密码错误会提示），验证通过后用新密码重新加密。
- **移除加密**：新密码留空并输入原密码，验证通过后删除加密包、保留明文文件夹。

### 加密桌面切换

- 目标桌面为加密桌面时，切换前弹窗要求输入密码；密码错误会重新弹窗。
- 输入正确密码后：文件夹不存在则解密还原再切换；文件夹已存在（本次会话已解锁过）则先验证密码再直接切换。
- 离开已解锁的加密桌面时自动重新加密：优先使用本次会话缓存的密码，缓存缺失时弹窗询问密码。
- 会话密码仅保存在内存中，退出程序即失效。

### 设置窗口

- 颜色模式下拉框：跟随系统 / 浅色 / 深色
- 关闭行为下拉框：询问 / 最小化到后台 / 退出程序
- 修改后重启软件生效
- **安装 Skills** 按钮：写入 SKILL.md 到 `%UserProfile%\.workbuddy\skills\MultiDesktop\`，并把程序目录加入用户 PATH

### 关闭确认窗口

- 确认操作对话框
- "不再询问"复选框：勾选后永久保存当前选择的退出模式

---

## 技术实现细节

### 架构分层

核心层 `Core/` 不依赖 WinForms，通过 `OperationResult`（提示文案 + 退出码 + 结构化数据）报告结果，由调用方决定呈现方式：GUI 弹 `MessageBox`，CLI 打印或输出 JSON。密码由调用方收集后作为参数传入。

```
Core/OperationResult.cs        统一结果模型 + 退出码
Core/AppPaths.cs               配置目录解析
Core/DesktopRepository.cs      DesktopList.xml 唯一读写出口
Core/DesktopService.cs         桌面增删改查、壁纸配置、当前桌面检测
Core/DesktopSwitchService.cs   切换工作流 + Win32 Shell API
Core/EncryptionService.cs      加解密与会话密码缓存
Core/PasswordService.cs        密码设置 / 修改 / 移除
Core/SettingsService.cs        AppSettings 读写
Core/WallpaperService.cs       壁纸应用
Core/SkillInstaller.cs         SKILL.md 安装与 PATH 写入
Core/CliArgs.cs / CliRunner.cs 命令行解析与调度
```

### 桌面切换机制

1. **优先方案**：通过 `SHSetKnownFolderPath(FOLDERID_Desktop, ...)` 修改桌面文件夹路径，无需重启 explorer。
2. **回退方案**：修改注册表键值
   - `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders\Desktop`
   - `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders\Desktop`

### 壁纸设置

通过 `SystemParametersInfo(SPI_SETDESKWALLPAPER, ...)` 设置壁纸，同步写入注册表：
- `HKCU\Control Panel\Desktop\WallpaperStyle`
- `HKCU\Control Panel\Desktop\TileWallpaper`

### 加密机制

1. 压缩桌面文件夹为 zip，用 `PostQuantum.FileEncryption`（`PqFileEncryptor` / `PqFileDecryptor`）加密为 `Zips\<id>.zip.encrypted`，成功后删除原文件夹。
2. 加密包 id 由桌面名称经 FNV-1a 哈希生成（`EncryptionService.GetZipId`），删除/重排桌面不会导致 id 错位。
3. 密码不落盘，通过实际解密校验；已验证的密码缓存在内存中供本次会话重新加密使用。
4. 任何一步失败都保留原文件夹，不会丢失数据。

### 构建与发布

- 目标框架：`net10.0-windows`
- 支持 AOT 发布 (`PublishAot=true`)，但未验证全部架构与功能
- 支持 MSIX 打包（包名：`Buger2008.MultiDesktop`，版本 `1.3.6.0`）
- 目标平台：x86 / x64 / ARM / ARM64
- 默认语言：zh-CN

---

## 常见操作步骤

### 新增一个桌面

1. 准备好一个已存在的文件夹作为桌面路径
2. CLI：`MultiDesktop add --name "新桌面" --path "D:\NewDesktop"`
   需要自定义壁纸时追加 `--wallpaper "D:\wall.jpg" --style 拉伸`
3. GUI：点击"添加桌面"，填写名称和路径后保存

### 切换桌面

1. CLI：`MultiDesktop switch --name "工作"`（加密桌面追加 `--password`）
2. GUI：主窗口选中桌面后点击"切换到选中桌面"，或从托盘右键菜单选择

### 加密一个桌面

1. CLI：`MultiDesktop add --name "私密" --path "D:\Private" --password 123456`
   或为已有桌面设置：`MultiDesktop password --name "私密" --new 123456`
2. GUI：添加/编辑桌面窗口中点击"桌面加密设置"并输入密码

> ⚠️ 加密会真实删除原明文文件夹，操作前应确认用户已备份。

### 修改 / 移除加密密码

```powershell
MultiDesktop password --name "私密" --new 654321 --old 123456
MultiDesktop password --name "私密" --remove --old 654321
```

忘记密码的桌面无法解锁，程序没有密码找回机制。

### 删除不用的桌面

1. CLI：`MultiDesktop remove --name "桌面名称"`
2. GUI：主窗口勾选要删除的桌面（支持多选）后点击"删除"

删除的只是配置，不会删除文件夹，也不会解除加密。
