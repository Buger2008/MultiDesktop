namespace MultiDesktop.Core
{
    /// <summary>
    /// 桌面切换与壁纸应用的平台实现。
    ///
    /// 目前只有 Windows 实现（<see cref="WindowsDesktopPlatform"/>）；
    /// macOS 与 Linux 已预留实现位（<see cref="MacDesktopPlatform"/> /
    /// <see cref="LinuxDesktopPlatform"/>），其中的注释写明了各自的落地思路。
    ///
    /// 新增平台只需实现本接口，并在 <see cref="DesktopPlatform.Create"/> 中登记。
    /// </summary>
    public interface IDesktopPlatform
    {
        /// <summary>平台显示名。</summary>
        string Name { get; }

        /// <summary>该平台是否已实现桌面切换。</summary>
        bool IsSupported { get; }

        /// <summary>
        /// 把当前用户的桌面目录切换到 <paramref name="newPath"/>，并按需应用壁纸。
        /// <paramref name="wallpaperPath"/> 为 null 时只切换目录、不改壁纸。
        /// 平台未实现时抛出 <see cref="PlatformNotSupportedException"/>。
        /// </summary>
        void SwitchDesktop(string newPath, string? wallpaperPath, string? wallpaperStyle);

        /// <summary>
        /// 把指定图片设为桌面壁纸（立即生效）。
        /// 平台未实现时抛出 <see cref="PlatformNotSupportedException"/>。
        /// </summary>
        void ApplyWallpaper(string wallpaperPath, string? wallpaperStyle);
    }
}
