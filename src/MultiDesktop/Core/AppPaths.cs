namespace MultiDesktop.Core
{
    /// <summary>
    /// 配置文件路径解析。winget 等场景下工作目录(CWD)可能不可写，
    /// 因此配置文件统一放在 exe 所在目录；该目录不可写时回退到 %AppData%\MultiDesktop。
    /// </summary>
    public static class AppPaths
    {
        public static readonly string ConfigDir = ResolveConfigDir();
        public static readonly string AppSettings = Path.Combine(ConfigDir, "AppSettings.xml");
        public static readonly string DesktopList = Path.Combine(ConfigDir, "DesktopList.xml");

        /// <summary>加密压缩包存放目录。</summary>
        public static string ZipsDir => Path.Combine(ConfigDir, "Zips");

        private static string ResolveConfigDir()
        {
            try
            {
                // 探测 exe 目录是否可写（winget portable 安装在用户目录下，可写）
                var probe = Path.Combine(AppContext.BaseDirectory, ".write_probe");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return AppContext.BaseDirectory;
            }
            catch
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MultiDesktop");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }
    }
}
