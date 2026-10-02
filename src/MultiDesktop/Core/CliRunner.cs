using System.Text;
using System.Text.Json;
using PostQuantum.FileEncryption;

namespace MultiDesktop.Core
{
    /// <summary>
    /// 满血版命令行入口。设计目标：适合 AI 与 GUI 稳定调用 ——
    /// 子命令 + 具名选项、结构化 JSON 输出、稳定退出码、数据与提示分离。
    /// 不依赖 WinForms：全部业务逻辑来自 Core 服务层。
    /// </summary>
    public static class CliRunner
    {
        public const string Version = "1.3.6.0";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            // 不转义中文，便于 AI 与人工直接阅读
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        // ==================================================================
        //  入口
        // ==================================================================

        public static int Run(string[] argv)
        {
            var (args, error) = CliParser.Parse(argv);
            if (error != null)
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine("运行 \"MultiDesktop help\" 查看用法。");
                return ExitCodes.Usage;
            }
            if (args == null) return ExitCodes.Usage;

            bool noInput = args.Has("--no-input");

            try
            {
                return args.Command switch
                {
                    "help" => CmdHelp(args),
                    "version" => CmdVersion(args),
                    "list" => CmdList(args),
                    "add" => CmdAdd(args, noInput),
                    "remove" => CmdRemove(args),
                    "switch" => CmdSwitch(args, noInput),
                    "wallpaper" => CmdWallpaper(args),
                    "password" => CmdPassword(args, noInput),
                    "settings" => CmdSettings(args),
                    "install-skills" => CmdInstallSkills(args),
                    "" => CmdHelp(args),
                    _ => Emit(args, OperationResult.Fail($"未知命令: {args.Command}", ExitCodes.Usage)),
                };
            }
            catch (Exception ex)
            {
                return Emit(args, OperationResult.Fail(ex.Message));
            }
        }

        // ==================================================================
        //  输出
        // ==================================================================

        private static int Emit(CliArgs a, OperationResult r)
        {
            if (a.Has("--json"))
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(
                    new { success = r.Success, message = r.Message, data = r.Data }, JsonOpts));
            }
            else if (r.Success)
            {
                if (!a.Has("--quiet") && !string.IsNullOrEmpty(r.Message))
                    Console.Out.WriteLine(r.Message);
            }
            else
            {
                if (!string.IsNullOrEmpty(r.Message))
                    Console.Error.WriteLine(r.Message);
            }
            return r.ExitCode;
        }

        // ==================================================================
        //  命令实现
        // ==================================================================

        private static int CmdVersion(CliArgs a)
        {
            if (a.Has("--json"))
                Console.Out.WriteLine(JsonSerializer.Serialize(
                    new { success = true, name = "MultiDesktop", version = Version }, JsonOpts));
            else
                Console.Out.WriteLine($"MultiDesktop v{Version}");
            return ExitCodes.Success;
        }

        private static int CmdList(CliArgs a)
        {
            // --xml：保留旧版直接输出原始配置文件的形态
            if (a.Has("--xml"))
            {
                if (File.Exists(AppPaths.DesktopList))
                    Console.Out.Write(File.ReadAllText(AppPaths.DesktopList));
                else
                {
                    Console.Out.WriteLine("当前没有配置任何桌面。");
                    Console.Out.WriteLine("提示: 使用 add --name <名称> --path <路径> 添加桌面");
                }
                return ExitCodes.Success;
            }

            var items = DesktopService.List();

            if (a.Has("--json"))
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(
                    new { success = true, count = items.Count, desktops = items }, JsonOpts));
                return ExitCodes.Success;
            }

            if (a.Has("--quiet")) return ExitCodes.Success;

            PrintDesktopTable(items);
            return ExitCodes.Success;
        }

        private static int CmdAdd(CliArgs a, bool noInput)
        {
            string? name = a.Get("--name");
            string? path = a.Get("--path");
            if (string.IsNullOrWhiteSpace(name)) return Emit(a, OperationResult.Fail("缺少必填选项 --name", ExitCodes.Usage));
            if (string.IsNullOrWhiteSpace(path)) return Emit(a, OperationResult.Fail("缺少必填选项 --path", ExitCodes.Usage));

            string? wallpaperPath = a.Get("--wallpaper");
            bool enableWallpaper = !string.IsNullOrWhiteSpace(wallpaperPath);
            string style = a.Get("--style") is { Length: > 0 } s ? s : DesktopRepository.DefaultWallpaperStyle;

            if (enableWallpaper && !WallpaperService.IsValidStyle(style))
                return Emit(a, OperationResult.Fail(
                    $"不支持的显示方式：{style}（可选：{string.Join(" / ", WallpaperService.Styles)}）", ExitCodes.Usage));

            // 密码：--password / --password-stdin / 交互输入；未提供则创建普通桌面
            string? password = null;
            if (a.Has("--password") || a.Has("--password-stdin"))
            {
                password = AcquireSecret(a, noInput, "请输入新密码: ");
                if (string.IsNullOrEmpty(password))
                    return Emit(a, OperationResult.Fail("需要密码：请用 --password 提供，或去掉 --no-input 以交互输入", ExitCodes.InputRequired));
            }

            bool isEncrypt = password != null;

            var add = DesktopService.AddDesktop(name, path, enableWallpaper, wallpaperPath, style, isEncrypt,
                isEdit: false, indexToChange: 0, allowMissingPath: false);
            if (!add.Success) return Emit(a, add);

            if (!isEncrypt)
                return Emit(a, OperationResult.Ok($"成功添加桌面: {name} -> {path}", new { name, path, encrypted = false }));

            // 与 GUI 流程一致：先写配置（加密标记随行写入），再执行加密；
            // 加密失败时回滚配置行，避免留下“标记已加密但文件夹仍是明文”的脏配置
            int id = EncryptionService.GetZipId(name);
            try
            {
                EncryptionService.GetZipFile(path!, id, password!).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                DesktopService.DeleteDesktop(name);
                return Emit(a, OperationResult.Fail($"添加失败: {ex.Message}"));
            }

            return Emit(a, OperationResult.Ok(
                $"成功添加桌面: {name} -> {path}\n已加密桌面: {name}（文件夹已加密，切换时需输入密码）",
                new { name, path, encrypted = true }));
        }

        private static int CmdRemove(CliArgs a)
        {
            string? name = a.Get("--name");
            if (string.IsNullOrWhiteSpace(name)) return Emit(a, OperationResult.Fail("缺少必填选项 --name", ExitCodes.Usage));
            return Emit(a, DesktopService.DeleteDesktop(name));
        }

        private static int CmdSwitch(CliArgs a, bool noInput)
        {
            string? name = a.Get("--name");
            if (string.IsNullOrWhiteSpace(name)) return Emit(a, OperationResult.Fail("缺少必填选项 --name", ExitCodes.Usage));

            var target = DesktopService.List()
                .FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));
            if (target == null)
                return Emit(a, OperationResult.Fail($"错误: 未找到名为 \"{name}\" 的桌面", ExitCodes.NotFound));
            if (string.IsNullOrEmpty(target.Path))
                return Emit(a, OperationResult.Fail("错误: 桌面路径为空", ExitCodes.Usage));

            // CLI 每次调用都是新进程，“当前桌面”的会话状态为空，
            // 必须先从实际的 Windows 桌面路径探测，否则不会触发“离开加密桌面自动重新加密”
            var current = DesktopService.DetectCurrentDesktop();
            DesktopSwitchService.SetCurrentDesktop(current?.Name, current?.Path, current?.Encrypted ?? false);

            var req = DesktopSwitchService.GetSwitchRequirements(target);
            if (a.Has("--no-reencrypt"))
                req = req with { LeavingNeedsPassword = false, LeavingName = null, LeavingId = 0 };

            // 1. 离开的加密桌面重新加密所需密码（CLI 每次都是新进程，会话缓存必为空）
            string? leavingPw = null;
            if (req.LeavingNeedsPassword)
            {
                leavingPw = a.Get("--reencrypt-password") ?? EncryptionService.GetSessionPassword(req.LeavingName);
                if (string.IsNullOrEmpty(leavingPw) && !noInput && !Console.IsInputRedirected)
                    leavingPw = ReadPasswordMasked($"请输入桌面“{req.LeavingName}”的密码以重新加密: ");
                // 拿不到密码不阻塞切换，由 Core 在结果中给出警告
            }

            // 2. 目标桌面密码
            string? targetPw = null;
            if (req.TargetNeedsPassword)
            {
                targetPw = AcquireSecret(a, noInput, $"请输入桌面“{target.Name}”的密码: ");
                if (string.IsNullOrEmpty(targetPw))
                    return Emit(a, OperationResult.Fail(
                        "需要密码：请用 --password 提供，或去掉 --no-input 以交互输入", ExitCodes.InputRequired));
            }

            // 3. 与 GUI 共用同一个切换工作流
            var outcome = DesktopSwitchService.SwitchToAsync(target, req, targetPw, leavingPw)
                .GetAwaiter().GetResult();

            if (outcome.BadPassword)
                return Emit(a, OperationResult.Fail("密码错误", ExitCodes.BadPassword));
            if (!outcome.Success)
                return Emit(a, OperationResult.Fail(outcome.Message));

            string message = outcome.Message;
            if (outcome.Warning != null) message += "\n" + outcome.Warning;

            return Emit(a, OperationResult.Ok(message,
                new { switched = target.Name, path = target.Path, warning = outcome.Warning }));
        }

        private static int CmdWallpaper(CliArgs a)
        {
            string? name = a.Get("--name");
            if (string.IsNullOrWhiteSpace(name)) return Emit(a, OperationResult.Fail("缺少必填选项 --name", ExitCodes.Usage));

            if (a.Has("--clear"))
                return Emit(a, DesktopService.ClearWallpaper(name));

            string? wallpaperPath = a.Get("--wallpaper");
            if (string.IsNullOrWhiteSpace(wallpaperPath))
                return Emit(a, OperationResult.Fail("需要 --wallpaper <壁纸路径> 或 --clear", ExitCodes.Usage));

            return Emit(a, DesktopService.SetWallpaper(name, wallpaperPath, a.Get("--style")));
        }

        private static int CmdPassword(CliArgs a, bool noInput)
        {
            string? name = a.Get("--name");
            if (string.IsNullOrWhiteSpace(name)) return Emit(a, OperationResult.Fail("缺少必填选项 --name", ExitCodes.Usage));

            var row = DesktopRepository.Find(name);
            if (row == null)
                return Emit(a, OperationResult.Fail($"错误: 未找到名为 \"{name}\" 的桌面", ExitCodes.NotFound));

            string? folder = DesktopRepository.GetString(row, DesktopRepository.IdxPath);
            if (string.IsNullOrEmpty(folder))
                return Emit(a, OperationResult.Fail("错误: 桌面路径为空", ExitCodes.Usage));

            int id = EncryptionService.GetZipId(name);
            bool hasOld = EncryptionService.HasEncryptedFile(id);

            // ===== 移除加密 =====
            if (a.Has("--remove"))
            {
                if (!hasOld)
                    return Emit(a, OperationResult.Ok($"提示: 桌面 \"{name}\" 尚未加密，无需操作"));

                string? oldPw = a.Get("--old") ?? a.Get("--password");
                if (string.IsNullOrEmpty(oldPw) && !noInput && !Console.IsInputRedirected)
                    oldPw = ReadPasswordMasked("请输入原密码: ");
                if (string.IsNullOrEmpty(oldPw))
                    return Emit(a, OperationResult.Fail("需要原密码：请用 --old 提供", ExitCodes.InputRequired));

                try
                {
                    EncryptionService.RemoveEncryption(folder, id, oldPw).GetAwaiter().GetResult();
                }
                catch (PqDecryptionException)
                {
                    return Emit(a, OperationResult.Fail("错误: 密码错误", ExitCodes.BadPassword));
                }
                catch (Exception ex)
                {
                    return Emit(a, OperationResult.Fail($"移除加密失败: {ex.Message}"));
                }

                DesktopService.SetEncryptedFlag(name, false);
                return Emit(a, OperationResult.Ok($"已移除桌面 \"{name}\" 的加密", new { name, encrypted = false }));
            }

            // ===== 设置 / 修改密码 =====
            string? newPw = a.Get("--new");
            if (string.IsNullOrWhiteSpace(newPw))
                return Emit(a, OperationResult.Fail("需要 --new <新密码> 或 --remove", ExitCodes.Usage));

            string? currentPw = a.Get("--old");
            if (hasOld)
            {
                if (string.IsNullOrEmpty(currentPw) && !noInput && !Console.IsInputRedirected)
                    currentPw = ReadPasswordMasked("请输入原密码: ");
                if (string.IsNullOrEmpty(currentPw))
                    return Emit(a, OperationResult.Fail("需要原密码：请用 --old 提供", ExitCodes.InputRequired));
            }

            try
            {
                if (hasOld)
                {
                    // 验证原密码：文件夹已解密时用实际解密校验，否则真实解密还原
                    if (Directory.Exists(folder))
                        EncryptionService.VerifyPasswordByDecryptAsync(id, currentPw!).GetAwaiter().GetResult();
                    else
                        EncryptionService.UnZipFile(folder, id, currentPw!).GetAwaiter().GetResult();
                }
                // 用新密码加密（原加密包会被覆盖）
                EncryptionService.GetZipFile(folder, id, newPw!).GetAwaiter().GetResult();
            }
            catch (PqDecryptionException)
            {
                return Emit(a, OperationResult.Fail("错误: 原密码错误", ExitCodes.BadPassword));
            }
            catch (Exception ex)
            {
                return Emit(a, OperationResult.Fail($"{(hasOld ? "修改密码" : "设置密码")}失败: {ex.Message}"));
            }

            DesktopService.SetEncryptedFlag(name, true);
            EncryptionService.SetSessionPassword(name, newPw!);

            return Emit(a, hasOld
                ? OperationResult.Ok($"已修改桌面 \"{name}\" 的加密密码", new { name, encrypted = true })
                : OperationResult.Ok($"已加密桌面: {name}（文件夹已加密，切换时需输入密码）", new { name, encrypted = true }));
        }

        private static int CmdSettings(CliArgs a)
        {
            string? color = a.Get("--color");
            string? exitMode = a.Get("--exit-mode");

            bool changed = false;
            if (!string.IsNullOrWhiteSpace(color))
            {
                if (Array.IndexOf(new[] { SettingsService.ColorFollowSystem, SettingsService.ColorLight, SettingsService.ColorDark }, color) < 0)
                    return Emit(a, OperationResult.Fail(
                        $"不支持的颜色模式：{color}（可选：{SettingsService.ColorFollowSystem} / {SettingsService.ColorLight} / {SettingsService.ColorDark}）",
                        ExitCodes.Usage));
                SettingsService.SetNum(SettingsService.KeyColor, SettingsService.GetColorNum(color));
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(exitMode))
            {
                if (Array.IndexOf(new[] { SettingsService.ExitAsk, SettingsService.ExitMinimize, SettingsService.ExitQuit }, exitMode) < 0)
                    return Emit(a, OperationResult.Fail(
                        $"不支持的关闭行为：{exitMode}（可选：{SettingsService.ExitAsk} / {SettingsService.ExitMinimize} / {SettingsService.ExitQuit}）",
                        ExitCodes.Usage));
                SettingsService.SetNum(SettingsService.KeyExitMode, SettingsService.GetExitModeNum(exitMode));
                changed = true;
            }

            int colorNum = SettingsService.GetNum(SettingsService.KeyColor);
            int exitNum = SettingsService.GetNum(SettingsService.KeyExitMode);
            string colorName = SettingsService.GetColorName(colorNum);
            string exitName = SettingsService.GetExitModeName(exitNum);

            if (a.Has("--json"))
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    changed,
                    color = colorName,
                    colorValue = colorNum,
                    exitMode = exitName,
                    exitModeValue = exitNum,
                }, JsonOpts));
                return ExitCodes.Success;
            }

            if (a.Has("--quiet") && changed) return ExitCodes.Success;

            if (changed)
                Console.Out.WriteLine($"设置已保存（颜色模式: {colorName}，关闭行为: {exitName}）");
            else
                Console.Out.WriteLine($"颜色模式: {colorName}\n关闭行为: {exitName}");
            return ExitCodes.Success;
        }

        private static int CmdInstallSkills(CliArgs a) => Emit(a, SkillInstaller.Install());

        private static int CmdHelp(CliArgs a)
        {
            if (a.Has("--json"))
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    name = "MultiDesktop",
                    version = Version,
                    usage = "MultiDesktop <命令> [--选项 值] [开关]",
                    globalOptions = new object[]
                    {
                        new { option = "--json", description = "以 JSON 输出结果（推荐给 AI / 脚本）" },
                        new { option = "--quiet", description = "成功时不输出提示" },
                        new { option = "--no-input", description = "禁止交互式输入，缺少密码时直接失败" },
                        new { option = "--password-stdin", description = "从标准输入读取一个密码" },
                        new { option = "--help, -h", description = "显示帮助" },
                    },
                    commands = CommandDocs,
                }, JsonOpts));
                return ExitCodes.Success;
            }

            Console.Out.WriteLine(HelpText);
            return ExitCodes.Success;
        }

        // ==================================================================
        //  密码输入
        // ==================================================================

        /// <summary>
        /// 获取“主密码”：优先 --password，其次 --password-stdin，再退化为交互式隐藏输入。
        /// 禁止交互时返回 null（由调用方转为 InputRequired 退出码）。
        /// </summary>
        private static string? AcquireSecret(CliArgs a, bool noInput, string prompt)
        {
            var direct = a.Get("--password");
            if (!string.IsNullOrEmpty(direct)) return direct;

            if (a.Has("--password-stdin"))
            {
                var line = Console.In.ReadLine();
                return string.IsNullOrEmpty(line) ? null : line;
            }

            if (noInput || Console.IsInputRedirected) return null;

            return ReadPasswordMasked(prompt);
        }

        /// <summary>交互式读取密码，回显为 *；输入被重定向时退化为按行读取。</summary>
        private static string ReadPasswordMasked(string prompt)
        {
            Console.Error.Write(prompt);

            if (Console.IsInputRedirected)
                return Console.In.ReadLine() ?? "";

            var sb = new StringBuilder();
            while (true)
            {
                ConsoleKeyInfo key;
                try { key = Console.ReadKey(intercept: true); }
                catch (InvalidOperationException) { return Console.In.ReadLine() ?? ""; }

                if (key.Key == ConsoleKey.Enter) { Console.Error.WriteLine(); break; }
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0) { sb.Length--; Console.Error.Write("\b \b"); }
                    continue;
                }
                if (char.IsControl(key.KeyChar)) continue;

                sb.Append(key.KeyChar);
                Console.Error.Write('*');
            }
            return sb.ToString();
        }

        // ==================================================================
        //  列表输出（中日韩字符按双宽对齐）
        // ==================================================================

        private static void PrintDesktopTable(IReadOnlyList<DesktopInfo> items)
        {
            if (items.Count == 0)
            {
                Console.Out.WriteLine("当前没有配置任何桌面。");
                Console.Out.WriteLine("提示: 使用 add --name <名称> --path <路径> 添加桌面");
                return;
            }

            string[] headers = { "名称", "路径", "壁纸", "显示方式", "加密" };
            var rows = items.Select(d => new[]
            {
                d.Name,
                d.Path,
                d.EnableWallpaper ? "是" : "否",
                d.EnableWallpaper && !string.IsNullOrEmpty(d.WallpaperStyle) ? d.WallpaperStyle : "-",
                d.Encrypted ? "是" : "否",
            }).ToList();

            var widths = new int[headers.Length];
            for (int i = 0; i < headers.Length; i++) widths[i] = DisplayWidth(headers[i]);
            foreach (var r in rows)
                for (int i = 0; i < r.Length; i++)
                    widths[i] = Math.Max(widths[i], DisplayWidth(r[i]));

            Console.Out.WriteLine(FormatRow(headers, widths));
            Console.Out.WriteLine(string.Join("  ", widths.Select(w => new string('-', w))));
            foreach (var r in rows) Console.Out.WriteLine(FormatRow(r, widths));
        }

        private static string FormatRow(string[] cells, int[] widths)
            => string.Join("  ", cells.Select((c, i) => PadTo(c, widths[i]))).TrimEnd();

        private static string PadTo(string s, int width)
            => s + new string(' ', Math.Max(0, width - DisplayWidth(s)));

        private static int DisplayWidth(string s) => s.Sum(c => IsWide(c) ? 2 : 1);

        /// <summary>判断是否为东亚宽字符（终端占两列）。</summary>
        private static bool IsWide(char c)
            => c >= 0x1100 && (
                   c <= 0x115F ||
                   c == 0x2329 || c == 0x232A ||
                   (c >= 0x2E80 && c <= 0xA4CF && c != 0x303F) ||
                   (c >= 0xAC00 && c <= 0xD7A3) ||
                   (c >= 0xF900 && c <= 0xFAFF) ||
                   (c >= 0xFE30 && c <= 0xFE6F) ||
                   (c >= 0xFF00 && c <= 0xFF60) ||
                   (c >= 0xFFE0 && c <= 0xFFE6) ||
                   (c >= 0x20000 && c <= 0x2FFFD) ||
                   (c >= 0x30000 && c <= 0x3FFFD));

        // ==================================================================
        //  帮助文档（文本与 JSON 共用同一份数据）
        // ==================================================================

        private sealed record OptionDoc(string Option, string Description);
        private sealed record CommandDoc(string Name, string Summary, string Usage, OptionDoc[] Options, string[] Examples);

        private static readonly CommandDoc[] CommandDocs =
        {
            new("list", "列出所有已配置的桌面",
                "MultiDesktop list [--json] [--xml] [--quiet]",
                new[]
                {
                    new OptionDoc("--json", "以 JSON 输出"),
                    new OptionDoc("--xml", "直接输出 DesktopList.xml 原文"),
                },
                new[] { "MultiDesktop list", "MultiDesktop list --json" }),

            new("add", "添加桌面（可选壁纸与加密）",
                "MultiDesktop add --name <名称> --path <路径> [--wallpaper <壁纸>] [--style <显示方式>] [--password <密码>]",
                new[]
                {
                    new OptionDoc("--name", "桌面名称，唯一，必填"),
                    new OptionDoc("--path", "桌面文件夹路径，必须已存在，必填"),
                    new OptionDoc("--wallpaper", "壁纸图片路径（提供即启用自定义壁纸）"),
                    new OptionDoc("--style", "壁纸显示方式：填充/适应/拉伸/平铺/居中/跨屏，默认填充"),
                    new OptionDoc("--password", "设置加密密码（文件夹会被压缩加密并删除原文件夹）"),
                },
                new[]
                {
                    "MultiDesktop add --name \"工作\" --path \"D:\\WorkDesktop\"",
                    "MultiDesktop add --name \"娱乐\" --path \"E:\\Game\" --wallpaper \"D:\\w.jpg\" --style 拉伸",
                    "MultiDesktop add --name \"私密\" --path \"D:\\Private\" --password 123456",
                }),

            new("remove", "删除桌面配置（不删除文件夹，也不解除加密）",
                "MultiDesktop remove --name <名称>",
                new[] { new OptionDoc("--name", "要删除的桌面名称，必填") },
                new[] { "MultiDesktop remove --name \"工作\"" }),

            new("switch", "切换当前桌面文件夹（核心功能）",
                "MultiDesktop switch --name <名称> [--password <密码>] [--reencrypt-password <密码>] [--no-reencrypt]",
                new[]
                {
                    new OptionDoc("--name", "目标桌面名称，必填"),
                    new OptionDoc("--password", "目标桌面的解锁密码（加密桌面必填）"),
                    new OptionDoc("--reencrypt-password", "离开的加密桌面重新加密所需密码"),
                    new OptionDoc("--no-reencrypt", "不重新加密离开的加密桌面"),
                },
                new[]
                {
                    "MultiDesktop switch --name \"工作\"",
                    "MultiDesktop switch --name \"私密\" --password 123456",
                }),

            new("wallpaper", "设置或取消已有桌面的自定义壁纸",
                "MultiDesktop wallpaper --name <名称> (--wallpaper <壁纸> [--style <显示方式>] | --clear)",
                new[]
                {
                    new OptionDoc("--name", "桌面名称，必填"),
                    new OptionDoc("--wallpaper", "壁纸图片路径"),
                    new OptionDoc("--style", "壁纸显示方式，默认填充"),
                    new OptionDoc("--clear", "取消该桌面的自定义壁纸"),
                },
                new[]
                {
                    "MultiDesktop wallpaper --name \"工作\" --wallpaper \"D:\\w.jpg\" --style 拉伸",
                    "MultiDesktop wallpaper --name \"工作\" --clear",
                }),

            new("password", "设置、修改或移除桌面加密密码",
                "MultiDesktop password --name <名称> (--new <新密码> [--old <原密码>] | --remove --old <原密码>)",
                new[]
                {
                    new OptionDoc("--name", "桌面名称，必填"),
                    new OptionDoc("--new", "新密码（首次设置或修改）"),
                    new OptionDoc("--old", "原密码（修改或移除时必填）"),
                    new OptionDoc("--remove", "移除加密保护"),
                },
                new[]
                {
                    "MultiDesktop password --name \"私密\" --new 654321 --old 123456",
                    "MultiDesktop password --name \"私密\" --remove --old 654321",
                }),

            new("settings", "查看或修改应用设置",
                "MultiDesktop settings [--color <模式>] [--exit-mode <行为>] [--json]",
                new[]
                {
                    new OptionDoc("--color", "颜色模式：跟随系统/浅色/深色"),
                    new OptionDoc("--exit-mode", "关闭行为：询问/最小化到后台/退出程序"),
                },
                new[] { "MultiDesktop settings", "MultiDesktop settings --color 深色" }),

            new("install-skills", "安装 SKILL.md 到技能目录并把程序目录加入 PATH",
                "MultiDesktop install-skills",
                Array.Empty<OptionDoc>(),
                new[] { "MultiDesktop install-skills" }),

            new("help", "显示帮助（--json 输出机器可读的命令表）",
                "MultiDesktop help [--json]",
                new[] { new OptionDoc("--json", "以 JSON 输出命令表") },
                new[] { "MultiDesktop help", "MultiDesktop help --json" }),

            new("version", "显示版本号",
                "MultiDesktop version [--json]",
                new[] { new OptionDoc("--json", "以 JSON 输出") },
                new[] { "MultiDesktop version" }),
        };

        private static string HelpText
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine($"MultiDesktop — Windows 多桌面管理工具  v{Version}");
                sb.AppendLine();
                sb.AppendLine("切换当前用户的 Windows 桌面文件夹，并为每个桌面配置独立壁纸与加密保护。");
                sb.AppendLine();
                sb.AppendLine("用法:");
                sb.AppendLine("  MultiDesktop <命令> [--选项 值] [开关]");
                sb.AppendLine();
                sb.AppendLine("  不带任何参数运行将启动图形界面。");
                sb.AppendLine();
                sb.AppendLine("全局选项:");
                sb.AppendLine("  --json                    以 JSON 输出结果（推荐给 AI / 脚本）");
                sb.AppendLine("  --quiet                   成功时不输出提示");
                sb.AppendLine("  --no-input                禁止交互式输入，缺少密码时直接失败");
                sb.AppendLine("  --password-stdin          从标准输入读取一个密码");
                sb.AppendLine("  --help, -h                显示此帮助");
                sb.AppendLine();
                sb.AppendLine("命令:");

                foreach (var c in CommandDocs)
                {
                    sb.AppendLine();
                    sb.AppendLine($"  {c.Name,-16}{c.Summary}");
                    sb.AppendLine($"    {c.Usage}");
                    foreach (var o in c.Options)
                        sb.AppendLine($"      {o.Option,-22}{o.Description}");
                }

                sb.AppendLine();
                sb.AppendLine("退出码:");
                sb.AppendLine("  0  成功");
                sb.AppendLine("  1  一般错误（IO 失败、加密失败等）");
                sb.AppendLine("  2  参数用法错误");
                sb.AppendLine("  3  桌面不存在");
                sb.AppendLine("  4  密码错误");
                sb.AppendLine("  5  需要密码但禁止交互");
                sb.AppendLine("  6  用户取消");
                sb.AppendLine();
                sb.AppendLine("提示:");
                sb.AppendLine("  - 桌面切换即时生效，无需重启资源管理器。");
                sb.AppendLine("  - 配置文件保存在程序同目录下的 DesktopList.xml。");
                sb.AppendLine("  - 命令行中的密码会进入终端历史与进程参数，建议改用 --password-stdin 或交互输入。");
                sb.AppendLine();
                sb.AppendLine("项目主页: https://github.com/Qibowen2008/MultiDesktop");
                sb.Append("许可证: MIT");
                return sb.ToString();
            }
        }
    }
}
