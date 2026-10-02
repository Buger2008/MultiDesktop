namespace MultiDesktop.Core
{
    /// <summary>
    /// 壁纸相关。
    /// 显示方式的取值与校验与平台无关；真正应用壁纸是平台相关动作，
    /// 统一走 <see cref="IDesktopPlatform.ApplyWallpaper"/>（Windows 实现在
    /// <see cref="WindowsDesktopPlatform"/> 中）。
    /// </summary>
    public static class WallpaperService
    {
        /// <summary>所有合法的壁纸显示方式（顺序与 GUI 下拉框一致）。</summary>
        public static readonly string[] Styles = { "填充", "适应", "拉伸", "平铺", "居中", "跨屏" };

        public static bool IsValidStyle(string? style)
            => !string.IsNullOrEmpty(style) && Array.IndexOf(Styles, style) >= 0;

        /// <summary>
        /// 把壁纸显示方式名称映射为 Windows 注册表 WallpaperStyle / TileWallpaper 值。
        /// 其他平台没有等价开关，映射方式需在各自实现中另行决定。
        /// </summary>
        public static (string style, string tile) GetStyleValues(string? styleName) => styleName switch
        {
            "居中" => ("0", "0"),
            "平铺" => ("0", "1"),
            "拉伸" => ("2", "0"),
            "适应" => ("6", "0"),
            "填充" => ("10", "0"),
            "跨屏" => ("22", "0"),
            _ => ("10", "0"),   // 默认填充
        };

        /// <summary>将指定图片设为桌面壁纸（立即生效）。平台相关，目前仅实现 Windows。</summary>
        public static void Apply(string wallpaperPath, string? wallpaperStyle)
            => DesktopPlatform.Current.ApplyWallpaper(wallpaperPath, wallpaperStyle);
    }
}
