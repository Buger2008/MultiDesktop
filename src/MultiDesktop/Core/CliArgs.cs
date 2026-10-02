namespace MultiDesktop.Core
{
    /// <summary>
    /// 解析后的命令行参数。
    /// 语法：MultiDesktop &lt;命令&gt; [--选项 值 | --开关] [位置参数...]
    /// </summary>
    public sealed class CliArgs
    {
        /// <summary>子命令名（小写）。</summary>
        public string Command { get; set; } = "";

        /// <summary>具名选项：值为 null 表示该选项是开关（flag）。</summary>
        public Dictionary<string, string?> Options { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>未被识别为选项的裸参数。</summary>
        public List<string> Positionals { get; } = new();

        public bool Has(string name) => Options.ContainsKey(name);

        public string? Get(string name) => Options.TryGetValue(name, out var v) ? v : null;

        /// <summary>取开关型选项是否出现。</summary>
        public bool Flag(string name) => Options.ContainsKey(name);
    }

    /// <summary>
    /// 命令行解析器。规则刻意保持简单、可预测，便于 AI 与 GUI 稳定调用：
    /// 需要取值的选项在 ValueOptions 中登记，其余 --xxx 一律视为开关。
    /// </summary>
    public static class CliParser
    {
        /// <summary>全部子命令（help 输出与校验共用）。</summary>
        public static readonly string[] Commands =
        {
            "list", "add", "remove", "switch", "wallpaper", "password",
            "settings", "install-skills", "help", "version",
        };

        /// <summary>需要跟一个值的选项；未登记的 --xxx 一律当作开关。</summary>
        public static readonly HashSet<string> ValueOptions = new(StringComparer.OrdinalIgnoreCase)
        {
            "--name", "--path", "--wallpaper", "--style",
            "--password", "--old", "--new", "--color", "--exit-mode",
            "--reencrypt-password",
        };

        /// <summary>全局开关。</summary>
        public static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase)
        {
            "--json", "--xml", "--clear", "--remove", "--no-reencrypt",
            "--no-input", "--password-stdin", "--quiet", "--help", "-h", "--version",
        };

        /// <summary>
        /// 解析参数。返回 (args, error)：error 非空表示用法错误。
        /// </summary>
        public static (CliArgs? args, string? error) Parse(string[] argv)
        {
            var result = new CliArgs();
            int start = 0;

            // 第一个非选项 token 作为子命令；--help/--version 可直接出现在首位
            if (argv.Length > 0 && !argv[0].StartsWith('-'))
            {
                result.Command = argv[0].ToLowerInvariant();
                start = 1;
                if (Array.IndexOf(Commands, result.Command) < 0)
                    return (null, $"未知命令: {argv[0]}");
            }
            else if (argv.Length > 0)
            {
                // 形如 MultiDesktop --help / --version
                result.Command = argv[0].Equals("--version", StringComparison.OrdinalIgnoreCase)
                    ? "version" : "help";
                start = 1;
            }
            else
            {
                result.Command = "";
            }

            for (int i = start; i < argv.Length; i++)
            {
                string token = argv[i];

                if (token == "--")
                {
                    // 其后全部视为位置参数
                    for (int k = i + 1; k < argv.Length; k++) result.Positionals.Add(argv[k]);
                    break;
                }

                if (token.StartsWith('-'))
                {
                    if (ValueOptions.Contains(token))
                    {
                        if (i + 1 >= argv.Length)
                            return (null, $"选项 {token} 缺少参数值");
                        result.Options[token] = argv[i + 1];
                        i++;
                    }
                    else
                    {
                        if (!Flags.Contains(token))
                            return (null, $"未知选项: {token}");
                        result.Options[token] = null;
                    }
                }
                else
                {
                    result.Positionals.Add(token);
                }
            }

            return (result, null);
        }
    }
}
