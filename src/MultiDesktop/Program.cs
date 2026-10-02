using System.Data;
using System.Runtime.InteropServices;
using MultiDesktop.Core;

namespace MultiDesktop
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        /// <summary>关闭窗口时选择“最小化到后台”的标记（由 frmClose 设置，frmMain 读取）。</summary>
        public static bool IsMinWindow = false;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // NativeAOT 下任何未处理异常都会以 0xC0000409（STATUS_STACK_BUFFER_OVERRUN）fail-fast，
            // 这里兜底：写 error.log 并弹窗提示，而不是直接崩溃。
            try
            {
                Run(args);
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppPaths.ConfigDir, "error.log"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\r\n{ex}\r\n");
                }
                catch { }
                MessageBox.Show($"MultiDesktop 启动失败：\n{ex.Message}\n\n详细信息已写入配置目录下的 error.log",
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void Run(string[] args)
        {
            // ========== CLI 模式 ==========
            // 带任何参数即进入命令行模式，全部逻辑由 Core 的 CliRunner 处理。
            if (args.Length > 0)
            {
                AttachConsole(-1);
                Environment.ExitCode = CliRunner.Run(args);
                return;
            }

            // ========== GUI 模式：立刻断开控制台 ==========
            FreeConsole();
            // 检测非交互式会话（WinGet 验证、Session 0、无桌面环境）
            if (!Environment.UserInteractive)
            {
                // 非交互式环境，直接退出，不启动 GUI
                // 返回 0 让 WinGet 验证通过
                return;
            }

            // ========== 初始化设置（首次运行按默认值创建） ==========
            _ = SettingsService.Table;
            ColorModeMap.Map.TryGetValue(
                SettingsService.GetColorName(SettingsService.GetNum(SettingsService.KeyColor)), out var colorMode);
            Application.SetColorMode(colorMode);

            ApplicationConfiguration.Initialize();
            Application.Run(new frmMain());
        }
    }

    /// <summary>
    /// 桌面编辑窗体的 static 中介传值（沿用原方案，不使用委托）。
    /// 数据访问与业务操作已全部迁至 Core，本类只承载窗体间的编辑状态。
    /// </summary>
    public static class DesktopEditState
    {
        public static string? t_DesktopName = "";
        public static string? t_DesktopPath = "";
        public static bool IsEdit = false;
        public static int IndexToChange;

        public static void Reset()
        {
            t_DesktopName = "";
            t_DesktopPath = "";
            IsEdit = false;
        }
    }

    /// <summary>
    /// 窗体间传值的 static 中介（加密相关）。
    /// 加解密本身由 Core 的 EncryptionService 负责，这里只保留传值字段。
    /// </summary>
    public static class EncryptManager
    {
        /// <summary>当前正在编辑的桌面是否已加密（由 frmPassword 设置，frmAddDesktop 读取）。</summary>
        public static bool IsEncrypted = false;

        // ===== 窗体间传值（Program.cs 公共 static class 方案，不使用委托） =====
        public static string? DesktopFolder;   // 正在设置密码的桌面文件夹路径
        public static string? DesktopName;     // 正在设置密码的桌面名称
        public static int DesktopID;           // 该桌面对应的加密 id
        public static string? Password;        // 密码窗体校验通过后回传的密码

        /// <summary>重置本次添加/编辑桌面时产生的加密状态。</summary>
        public static void Reset()
        {
            IsEncrypted = false;
            Password = null;
            DesktopFolder = null;
            DesktopName = null;
            DesktopID = 0;
        }

        /// <summary>由桌面名称生成稳定的正整数 id（用于命名加密压缩包与定位密码记录）。</summary>
        public static int GetZipId(string desktopName) => EncryptionService.GetZipId(desktopName);

        /// <summary>本次会话内已通过验证的密码缓存。</summary>
        public static string? GetSessionPassword(string? name) => EncryptionService.GetSessionPassword(name);
    }
}
