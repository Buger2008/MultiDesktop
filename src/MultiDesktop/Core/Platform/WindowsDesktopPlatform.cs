using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MultiDesktop.Core
{
    /// <summary>
    /// Windows 实现。
    /// 桌面切换：优先 <c>SHSetKnownFolderPath</c>（无感切换，不重启 explorer），
    /// 失败则回退到「改注册表 + 重启 explorer」。
    /// 壁纸：<c>SystemParametersInfo(SPI_SETDESKWALLPAPER)</c>，显示方式写入注册表。
    /// </summary>
    internal sealed class WindowsDesktopPlatform : IDesktopPlatform
    {
        public string Name => "Windows";
        public bool IsSupported => true;

        // ===== 桌面文件夹切换 =====

        // 桌面文件夹的 Known Folder GUID
        private static readonly Guid FOLDERID_Desktop =
            new("{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}");

        private const uint KF_FLAG_NO_FLAGS = 0x00000000;  // 写注册表 + 立即通知 Shell 刷新

        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_FLUSH = 0x1000;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHSetKnownFolderPath(
            ref Guid rfid, uint dwFlags, IntPtr hToken, string pszPath);

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(
            uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        // ===== 壁纸 =====

        private const uint SPI_SETDESKWALLPAPER = 0x0014;
        private const uint SPIF_UPDATEINIFILE = 0x01;  // 写入注册表（user profile）
        private const uint SPIF_SENDCHANGE = 0x02;     // 广播 WM_SETTINGCHANGE

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(
            uint uAction, uint uParam, string lpvParam, uint fuWinInfo);

        // ==================================================================

        public void SwitchDesktop(string newPath, string? wallpaperPath, string? wallpaperStyle)
        {
            // 确保目标目录存在
            if (!Directory.Exists(newPath))
                Directory.CreateDirectory(newPath);

            // 设置了壁纸路径则立即应用
            if (!string.IsNullOrEmpty(wallpaperPath) && File.Exists(wallpaperPath))
                ApplyWallpaper(wallpaperPath, wallpaperStyle);

            // 方案一（优先）：SHSetKnownFolderPath —— 无感切换
            var guid = FOLDERID_Desktop;
            int hr = SHSetKnownFolderPath(ref guid, KF_FLAG_NO_FLAGS, IntPtr.Zero, newPath);

            if (hr == 0) // S_OK
            {
                // 双重保险：再发一次 Shell 刷新通知
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
                return;
            }

            // 方案二（回退）：改注册表 + 重启 explorer
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders", true))
            {
                if (key == null)
                    throw new Exception("无法打开注册表项");
                key.SetValue("Desktop", newPath, RegistryValueKind.ExpandString);
            }

            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders", true))
            {
                if (key != null)
                    key.SetValue("Desktop", newPath, RegistryValueKind.ExpandString);
            }
            foreach (Process process in Process.GetProcessesByName("explorer"))
            {
                process.Kill();
            }
            System.Threading.Thread.Sleep(1000);
            Process.Start("explorer.exe");
        }

        public void ApplyWallpaper(string wallpaperPath, string? wallpaperStyle)
        {
            var (styleVal, tileVal) = WallpaperService.GetStyleValues(wallpaperStyle);

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
