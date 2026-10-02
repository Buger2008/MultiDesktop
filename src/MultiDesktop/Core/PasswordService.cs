using PostQuantum.FileEncryption;

namespace MultiDesktop.Core
{
    /// <summary>密码操作成功后回传给调用方的加密状态。</summary>
    public sealed record PasswordState(bool Encrypted, string? SessionPassword);

    /// <summary>
    /// 桌面加密密码的设置 / 修改 / 移除用例。
    /// 原先这 80 行逻辑写在 frmPassword 的按钮点击处理器里，现独立为可复用模块，
    /// 文案与判定顺序与原实现逐字一致。
    /// </summary>
    public static class PasswordService
    {
        /// <summary>
        /// 依据输入执行对应操作：
        /// 新密码为空 → 移除加密；尚无加密包 → 首次设置；已有加密包 → 修改密码。
        /// </summary>
        public static async Task<OperationResult> ApplyAsync(
            string? desktopName, string? folder, int id,
            string? oldPassword, string? newPassword, string? confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return OperationResult.Fail("缺少桌面信息，请返回重新设置", ExitCodes.Usage, title: "错误");

            if (newPassword != confirmPassword)
                return OperationResult.Fail("两次输入的密码不一致", ExitCodes.Usage, title: "提示", isError: false);

            bool hasOld = EncryptionService.HasEncryptedFile(id);

            try
            {
                // ===== 移除加密 =====
                if (string.IsNullOrEmpty(newPassword))
                {
                    if (!hasOld)
                        return OperationResult.Fail("该桌面尚未加密，无需操作", ExitCodes.Usage, title: "提示", isError: false);

                    if (string.IsNullOrWhiteSpace(oldPassword))
                        return OperationResult.Fail("请输入原密码", ExitCodes.Usage, title: "提示", isError: false);

                    try
                    {
                        await EncryptionService.RemoveEncryption(folder, id, oldPassword);
                    }
                    catch (PqDecryptionException)
                    {
                        return OperationResult.Fail("原密码错误", ExitCodes.BadPassword, title: "错误");
                    }

                    return OperationResult.Ok("桌面已取消加密保护",
                        new PasswordState(Encrypted: false, SessionPassword: null), title: "提示");
                }

                // ===== 首次设置密码（原密码留空）=====
                if (!hasOld)
                {
                    if (!string.IsNullOrWhiteSpace(oldPassword))
                        return OperationResult.Fail("首次设置密码无需输入原密码", ExitCodes.Usage, title: "提示", isError: false);

                    await EncryptionService.GetZipFile(folder, id, newPassword);
                    return OperationResult.Ok("桌面加密成功",
                        new PasswordState(Encrypted: true, SessionPassword: newPassword), title: "提示");
                }

                // ===== 修改密码：验证旧密码，还原文件夹后用新密码重新加密 =====
                if (string.IsNullOrWhiteSpace(oldPassword))
                    return OperationResult.Fail("请输入原密码", ExitCodes.Usage, title: "提示", isError: false);

                try
                {
                    if (Directory.Exists(folder))
                    {
                        // 文件夹已存在（明文）：解密到临时目录校验旧密码，避免旧压缩包覆盖新文件
                        await EncryptionService.VerifyPasswordByDecryptAsync(id, oldPassword);
                    }
                    else
                    {
                        // 文件夹不存在（仍加密）：用旧密码真实解密还原，既是验证也是还原
                        await EncryptionService.UnZipFile(folder, id, oldPassword);
                    }
                }
                catch (PqDecryptionException)
                {
                    return OperationResult.Fail("原密码错误", ExitCodes.BadPassword, title: "错误");
                }

                // 用新密码重新加密（原加密包会被覆盖）
                await EncryptionService.GetZipFile(folder, id, newPassword);
                return OperationResult.Ok("密码修改成功",
                    new PasswordState(Encrypted: true, SessionPassword: newPassword), title: "提示");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"操作失败：{ex.Message}", ExitCodes.Failure, title: "错误");
            }
        }
    }
}
