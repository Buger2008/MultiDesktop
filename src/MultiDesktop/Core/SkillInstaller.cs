namespace MultiDesktop.Core
{
    /// <summary>
    /// 安装 SKILL.md 到 WorkBuddy 技能目录，并把 exe 所在目录加入用户 PATH。
    /// 原先写在 frmSet 的按钮点击处理器里，现已独立为可复用模块。
    /// </summary>
    public static class SkillInstaller
    {
        public static OperationResult Install()
        {
            try
            {
                // 1. 安装 SKILL.md 到 WorkBuddy skill 目录
                var skillDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".workbuddy", "skills", "MultiDesktop");
                Directory.CreateDirectory(skillDir);

                var sourceSkillMd = Path.Combine(AppContext.BaseDirectory, "SKILL.md");
                var destSkillMd = Path.Combine(skillDir, "SKILL.md");

                if (!File.Exists(sourceSkillMd))
                    return OperationResult.Fail($"未找到 SKILL.md 文件:\n{sourceSkillMd}", ExitCodes.Failure, title: "安装失败");

                File.Copy(sourceSkillMd, destSkillMd, overwrite: true);

                // 2. 将 exe 所在目录添加到用户 PATH
                var exeDir = AppContext.BaseDirectory.TrimEnd('\\');
                var currentPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";

                var pathAlreadySet = currentPath
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Any(p => string.Equals(p.Trim().TrimEnd('\\'), exeDir, StringComparison.OrdinalIgnoreCase));

                if (!pathAlreadySet)
                {
                    var newPath = currentPath.TrimEnd(';') + ";" + exeDir;
                    Environment.SetEnvironmentVariable("Path", newPath, EnvironmentVariableTarget.User);
                }

                var pathMsg = pathAlreadySet ? "PATH 已存在，无需重复添加" : "PATH 已添加";
                return OperationResult.Ok(
                    $"Skills 安装成功！\n\nSkill 目录: {skillDir}\n{pathMsg}: {exeDir}",
                    new { skillDir, exeDir, pathAlreadySet },
                    title: "安装成功");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"安装失败: {ex.Message}", ExitCodes.Failure, title: "错误");
            }
        }
    }
}
