namespace MultiDesktop.Core
{
    /// <summary>
    /// 运行平台判定。
    /// 桌面切换与壁纸应用是平台相关功能；Core 的其余部分
    /// （配置读写、加解密、密码用例、设置）与平台无关。
    /// </summary>
    public static class PlatformInfo
    {
        public static bool IsWindows => OperatingSystem.IsWindows();
        public static bool IsMacOS => OperatingSystem.IsMacOS();
        public static bool IsLinux => OperatingSystem.IsLinux();

        /// <summary>平台显示名，用于提示文案。</summary>
        public static string Name =>
            IsWindows ? "Windows" :
            IsMacOS ? "macOS" :
            IsLinux ? "Linux" : "未知平台";

        /// <summary>当前平台是否已实现桌面文件夹切换。</summary>
        public static bool IsDesktopSwitchSupported => IsWindows;

        /// <summary>
        /// 某平台尚未实现桌面切换时的提示。
        /// 平台名由调用方传入（取自拒绝该操作的实现），避免提示与实际实现不符。
        /// </summary>
        public static string UnsupportedDesktopSwitchMessage(string platformName) =>
            $"当前平台（{platformName}）尚未实现桌面文件夹切换，目前仅支持 Windows。";

        /// <summary>某平台尚未实现壁纸设置时的提示。</summary>
        public static string UnsupportedWallpaperMessage(string platformName) =>
            $"当前平台（{platformName}）尚未实现壁纸设置，目前仅支持 Windows。";
    }
}
