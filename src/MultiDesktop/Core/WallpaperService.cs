using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MultiDesktop.Core
{
    /// <summary>
    /// 壁纸应用：SystemParametersInfo 立即生效，并同步写入注册表的显示方式。
    /// </summary>
    public static class WallpaperService
    {
        private const uint SPI_SETDESKWALLPAPER = 0x0014;
        private const uint SPIF_UPDATEINIFILE = 0x01;  // 写入注册表（user profile）
        private const uint SPIF_SENDCHANGE = 0x02;     // 广播 WM_SETTINGCHANGE

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(
            uint uAction, uint uParam, string lpvParam, uint fuWinInfo);

        /// <summary>所有合法的壁纸显示方式（顺序与 GUI 下拉框一致）。</summary>
        public static readonly string[] Styles = { "填充", "适应", "拉伸", "平铺", "居中", "跨屏" };

        public static bool IsValidStyle(string? style)
            => !string.IsNullOrEmpty(style) && Array.IndexOf(Styles, style) >= 0;

        /// <summary>
        /// 把壁纸显示方式名称映射为注册表 WallpaperStyle / TileWallpaper 值。
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

        /// <summary>将指定图片设为桌面壁纸（立即生效）。</summary>
        public static void Apply(string wallpaperPath, string? wallpaperStyle)
        {
            var (styleVal, tileVal) = GetStyleValues(wallpaperStyle);

            using (var deskKey = Registry.CurrentUser.OpenSubKey(
                @"Control Panel\Desktop", writable: true))
            {
                deskKey?.SetValue("WallpaperStyle", styleVal, RegistryValueKind.String);
                deskKey?.SetValue("TileWallpaper", tileVal, RegistryValueKind.String);
            }

            // 调用 SystemParametersInfo 立即应用壁纸并广播变更通知
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, wallpaperPath,
                SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }
    }
}
