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
    /// 窗体间传值用的 static 中介（沿用原方案，不使用委托）。
    /// 桌面数据与全部业务操作已委托给 Core，本类只保留 GUI 侧的状态中转。
    /// </summary>
    public static class DesktopManager
    {
        /// <summary>桌面配置表（唯一来源为 Core 的 DesktopRepository）。</summary>
        public static DataTable DesktopList => DesktopRepository.Table;

        // ===== 窗体间传值 =====
        public static string? t_DesktopName = "";
        public static string? t_DesktopPath = "";
        public static bool IsEdit = false;
        public static int IndexToChange;

        /// <summary>记录当前激活桌面，供 Core 判断是否需要重新加密离开的加密桌面。</summary>
        public static void SetCurrentDesktop(string? name, string? path, bool encrypted)
            => DesktopSwitchService.SetCurrentDesktop(name, path, encrypted);

        /// <summary>安全读取行中的布尔值（兼容旧版 XML 缺少列/值为空的情况）。</summary>
        public static bool GetBool(DataRow row, int index) => DesktopRepository.GetBool(row, index);

        /// <summary>安全读取行中的字符串值。</summary>
        public static string? GetString(DataRow row, int index) => DesktopRepository.GetString(row, index);

        public static void ReSetDesktopManager()
        {
            t_DesktopName = "";
            t_DesktopPath = "";
            IsEdit = false;
        }

        /// <summary>
        /// 添加 / 编辑桌面配置。失败时保持原有的无标题 MessageBox 提示与返回值语义。
        /// </summary>
        public static bool AddDesktop(string DesktopName, string DesktopPath, bool enableWallpaper, string wallpaperPath, string wallpaperStyle, bool IsEncrypt)
        {
            var result = DesktopService.AddDesktop(
                DesktopName, DesktopPath, enableWallpaper, wallpaperPath, wallpaperStyle,
                IsEncrypt, IsEdit, IndexToChange,
                // 编辑一个已加密的桌面时，其明文文件夹已被加密删除，路径不存在属正常
                allowMissingPath: EncryptManager.IsEncrypted);

            if (!result.Success)
            {
                MessageBox.Show(result.Message);
                return false;
            }
            return true;
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
