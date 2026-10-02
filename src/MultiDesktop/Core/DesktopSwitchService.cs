namespace MultiDesktop.Core
{
    /// <summary>切换前需要向用户索取哪些密码。</summary>
    public sealed record SwitchRequirements(
        bool TargetNeedsPassword,
        int TargetId,
        bool LeavingNeedsPassword,
        int LeavingId,
        string? LeavingName);

    /// <summary>
    /// 切换结果。BadPassword 表示目标桌面密码错误，调用方可据此重新弹窗询问
    /// （与原 GUI“密码错误会重新弹窗”的行为一致）。
    /// </summary>
    public sealed record SwitchOutcome(
        bool Success,
        string Message,
        string? Warning = null,
        bool BadPassword = false);

    /// <summary>
    /// 桌面文件夹切换与“离开加密桌面自动重新加密”的完整工作流。
    /// 不依赖 WinForms：密码由调用方收集后传入，密码错误以 BadPassword 返回。
    /// GUI 与 CLI 共用本模块，保证两边行为一致。
    ///
    /// 具体的目录切换动作是平台相关的，交由 <see cref="IDesktopPlatform"/> 实现；
    /// 加解密、密码校验、会话缓存等部分与平台无关，在本类中统一处理。
    /// </summary>
    public static class DesktopSwitchService
    {
        // ==================================================================
        //  平台能力
        // ==================================================================

        /// <summary>当前平台是否已实现桌面切换（目前仅 Windows）。</summary>
        public static bool IsPlatformSupported => DesktopPlatform.Current.IsSupported;

        /// <summary>当前平台名称。</summary>
        public static string PlatformName => DesktopPlatform.Current.Name;

        // ==================================================================
        //  当前激活桌面（会话内状态，用于离开加密桌面时自动重新加密）
        // ==================================================================

        public static string? CurrentDesktopName { get; private set; }
        public static string? CurrentDesktopPath { get; private set; }
        public static bool CurrentDesktopEncrypted { get; private set; }

        public static void SetCurrentDesktop(string? name, string? path, bool encrypted)
        {
            CurrentDesktopName = name;
            CurrentDesktopPath = path;
            CurrentDesktopEncrypted = encrypted;
        }

        // ==================================================================
        //  切换需求分析
        // ==================================================================

        /// <summary>
        /// 分析切换到 target 需要哪些密码。调用方据此决定弹窗询问哪几个密码。
        /// 注意：目标就是当前桌面时不触发重新加密，避免把正在使用的桌面加密掉。
        /// </summary>
        public static SwitchRequirements GetSwitchRequirements(DesktopInfo target)
        {
            bool leaving = CurrentDesktopEncrypted
                && !string.IsNullOrEmpty(CurrentDesktopName)
                && !string.IsNullOrEmpty(CurrentDesktopPath)
                && !string.Equals(CurrentDesktopName, target.Name, StringComparison.Ordinal)
                && Directory.Exists(CurrentDesktopPath);

            return new SwitchRequirements(
                TargetNeedsPassword: target.Encrypted,
                TargetId: target.Encrypted ? EncryptionService.GetZipId(target.Name) : 0,
                LeavingNeedsPassword: leaving,
                LeavingId: leaving ? EncryptionService.GetZipId(CurrentDesktopName!) : 0,
                LeavingName: leaving ? CurrentDesktopName : null);
        }

        // ==================================================================
        //  切换工作流
        // ==================================================================

        /// <summary>
        /// 执行切换：解锁目标桌面 → 切换目录与壁纸 → 重新加密离开的加密桌面 → 记录当前桌面。
        /// 密码错误返回 BadPassword（不做任何状态变更，调用方可重新询问）。
        /// 平台未实现时直接返回失败，不会询问密码。
        /// </summary>
        public static async Task<SwitchOutcome> SwitchToAsync(
            DesktopInfo target, SwitchRequirements req, string? targetPassword, string? leavingPassword)
        {
            // 平台能力检查放在最前：不支持的平台上不必先让用户输密码再失败
            if (!IsPlatformSupported)
                return new SwitchOutcome(false, PlatformInfo.UnsupportedDesktopSwitchMessage(PlatformName));

            if (string.IsNullOrEmpty(target.Path))
                return new SwitchOutcome(false, "错误: 桌面路径为空");

            string? wallpaper = target.EnableWallpaper && !string.IsNullOrEmpty(target.WallpaperPath)
                ? target.WallpaperPath : null;

            // ---- 1. 解锁并切换目标桌面 ----
            try
            {
                if (target.Encrypted)
                {
                    string pw = targetPassword ?? "";
                    if (Directory.Exists(target.Path))
                    {
                        // 文件夹已存在（此前已解锁为明文）：先验证密码再直接切换，
                        // 避免用可能过期的压缩包覆盖桌面上的新文件
                        await EncryptionService.VerifyPasswordByDecryptAsync(req.TargetId, pw);
                        ChangeDesktopPath(target.Path, wallpaper, target.WallpaperStyle);
                    }
                    else
                    {
                        // 文件夹不存在：真实解密还原后切换（密码错误会在这里抛 PqDecryptionException）
                        await EncryptionService.UnZipFile(target.Path, req.TargetId, pw);
                        ChangeDesktopPath(target.Path, wallpaper, target.WallpaperStyle);
                    }
                    EncryptionService.SetSessionPassword(target.Name, pw);
                }
                else
                {
                    ChangeDesktopPath(target.Path, wallpaper, target.WallpaperStyle);
                }
            }
            catch (PostQuantum.FileEncryption.PqDecryptionException)
            {
                return new SwitchOutcome(false, "密码错误", BadPassword: true);
            }
            catch (Exception ex)
            {
                return new SwitchOutcome(false, $"切换失败：{ex.Message}");
            }

            // ---- 2. 重新加密离开的加密桌面（此时 explorer 已释放旧桌面文件夹）----
            string? warning = null;
            if (req.LeavingNeedsPassword
                && !string.IsNullOrEmpty(CurrentDesktopPath)
                && Directory.Exists(CurrentDesktopPath))
            {
                string oldName = CurrentDesktopName!;
                string oldPath = CurrentDesktopPath;
                if (string.IsNullOrEmpty(leavingPassword))
                {
                    warning = $"警告：桌面“{oldName}”未能重新加密（缺少密码，文件暂为明文）。";
                }
                else
                {
                    await Task.Delay(800); // 等待 explorer 释放旧桌面文件夹
                    try
                    {
                        await EncryptionService.GetZipFile(oldPath, EncryptionService.GetZipId(oldName), leavingPassword);
                    }
                    catch (Exception ex)
                    {
                        warning = $"警告：桌面“{oldName}”重新加密失败（文件暂为明文）：{ex.Message}";
                    }
                }
            }

            // ---- 3. 记录当前桌面 ----
            SetCurrentDesktop(target.Name, target.Path, target.Encrypted);

            return new SwitchOutcome(true, $"已切换到桌面: {target.Name} -> {target.Path}", warning);
        }

        // ==================================================================
        //  底层切换原语（平台相关，委托给 IDesktopPlatform）
        // ==================================================================

        public static void ChangeDesktopPath(string newPath)
            => ChangeDesktopPath(newPath, null, null);

        /// <summary>
        /// 切换到一个加密桌面：先用密码解密还原文件夹，再切换。
        /// 密码错误时 UnZipFile 会抛出异常，不会执行切换。
        /// </summary>
        public static async Task ChangeDesktopPathWithPassword(
            string newPath, string? wallpaperPath, string? wallpaperStyle, int id, string password)
        {
            await EncryptionService.UnZipFile(newPath, id, password);
            ChangeDesktopPath(newPath, wallpaperPath, wallpaperStyle);
        }

        /// <summary>
        /// 切换桌面文件夹路径，并可选设置壁纸及显示方式。
        /// wallpaperPath 为 null 时仅切换目录，不修改壁纸。
        /// 平台未实现时抛出 <see cref="PlatformNotSupportedException"/>。
        /// </summary>
        public static void ChangeDesktopPath(string newPath, string? wallpaperPath, string? wallpaperStyle)
            => DesktopPlatform.Current.SwitchDesktop(newPath, wallpaperPath, wallpaperStyle);
    }
}
