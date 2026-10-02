namespace MultiDesktop.Core
{
    /// <summary>
    /// macOS 实现位（尚未实现）。
    ///
    /// 落地思路（待实现时参考）：
    /// 1. 桌面目录：macOS 没有官方的“桌面目录重定向”接口，Finder 固定显示 ~/Desktop。
    ///    可行做法是把 ~/Desktop 换成指向目标文件夹的符号链接：
    ///    移动原目录到备份位置，再 <c>ln -s &lt;目标目录&gt; ~/Desktop</c>，
    ///    最后 <c>killall Finder</c> 让 Finder 重新读取。
    ///    需要处理原目录已存在、目标与当前相同、以及回滚等边界情况。
    /// 2. 壁纸：<c>osascript -e 'tell application "System Events" to set picture of every
    ///    desktop to "&lt;路径&gt;"'</c>；显示方式（填充/适应等）没有直接对应的开关，需单独映射或忽略。
    /// 3. 权限：写入 ~/Desktop 之外的路径可能触发“完全磁盘访问权限”要求。
    /// </summary>
    internal sealed class MacDesktopPlatform : IDesktopPlatform
    {
        public string Name => "macOS";
        public bool IsSupported => false;

        public void SwitchDesktop(string newPath, string? wallpaperPath, string? wallpaperStyle)
            => throw new PlatformNotSupportedException(PlatformInfo.UnsupportedDesktopSwitchMessage(Name));

        public void ApplyWallpaper(string wallpaperPath, string? wallpaperStyle)
            => throw new PlatformNotSupportedException(PlatformInfo.UnsupportedWallpaperMessage(Name));
    }
}
