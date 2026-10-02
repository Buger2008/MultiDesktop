namespace MultiDesktop.Core
{
    /// <summary>按运行平台选择桌面切换实现。</summary>
    internal static class DesktopPlatform
    {
        private static IDesktopPlatform? _current;

        /// <summary>当前平台的实现（首次访问时确定）。</summary>
        public static IDesktopPlatform Current => _current ??= Create();

        /// <summary>按 <see cref="PlatformInfo"/> 创建平台实现。</summary>
        public static IDesktopPlatform Create()
        {
            if (PlatformInfo.IsWindows) return new WindowsDesktopPlatform();
            if (PlatformInfo.IsMacOS) return new MacDesktopPlatform();
            if (PlatformInfo.IsLinux) return new LinuxDesktopPlatform();
            return new UnsupportedDesktopPlatform();
        }
    }

    /// <summary>未知平台的占位实现（桌面切换与壁纸均未实现）。</summary>
    internal sealed class UnsupportedDesktopPlatform : IDesktopPlatform
    {
        public string Name => PlatformInfo.Name;
        public bool IsSupported => false;

        public void SwitchDesktop(string newPath, string? wallpaperPath, string? wallpaperStyle)
            => throw new PlatformNotSupportedException(PlatformInfo.UnsupportedDesktopSwitchMessage(Name));

        public void ApplyWallpaper(string wallpaperPath, string? wallpaperStyle)
            => throw new PlatformNotSupportedException(PlatformInfo.UnsupportedWallpaperMessage(Name));
    }
}
