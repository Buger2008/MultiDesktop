namespace MultiDesktop.Core
{
    /// <summary>
    /// Linux 实现位（尚未实现）。
    ///
    /// 落地思路（待实现时参考）：
    /// 1. 桌面目录：走 XDG 用户目录规范 ——
    ///    <c>xdg-user-dirs-update --set DESKTOP &lt;路径&gt;</c>（写入 ~/.config/user-dirs.dirs），
    ///    再执行 <c>xdg-user-dirs-gtk-update</c> 同步 GTK 书签。
    ///    注意 xdg-user-dirs 未必安装，且桌面环境是否尊重该配置取决于实现。
    /// 2. 壁纸：各桌面环境命令不同，需要按 DE 分支 ——
    ///    GNOME: <c>gsettings set org.gnome.desktop.background picture-uri file://&lt;路径&gt;</c>
    ///           （深色模式还要设 picture-uri-dark）；
    ///    KDE:   <c>plasma-apply-wallpaperimage &lt;路径&gt;</c>；
    ///    XFCE:  <c>xfconf-query -c xfce4-desktop -p ... -s &lt;路径&gt;</c>。
    ///    Wayland 与 X11 的行为也有差异。
    /// 3. 探测方式：优先读 <c>$XDG_CURRENT_DESKTOP</c> 判断当前桌面环境。
    /// </summary>
    internal sealed class LinuxDesktopPlatform : IDesktopPlatform
    {
        public string Name => "Linux";
        public bool IsSupported => false;

        public void SwitchDesktop(string newPath, string? wallpaperPath, string? wallpaperStyle)
            => throw new PlatformNotSupportedException(PlatformInfo.UnsupportedDesktopSwitchMessage(Name));

        public void ApplyWallpaper(string wallpaperPath, string? wallpaperStyle)
            => throw new PlatformNotSupportedException(PlatformInfo.UnsupportedWallpaperMessage(Name));
    }
}
