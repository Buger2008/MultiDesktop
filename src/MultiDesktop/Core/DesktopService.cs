using System.Data;

namespace MultiDesktop.Core
{
    /// <summary>单个桌面配置的只读视图（用于 CLI 列表 / JSON 输出）。</summary>
    public sealed record DesktopInfo(
        string Name,
        string Path,
        bool EnableWallpaper,
        string WallpaperPath,
        string WallpaperStyle,
        bool Encrypted);

    /// <summary>
    /// 桌面配置的增删改查用例。不依赖 WinForms：
    /// 失败以 OperationResult 返回，文案与原 GUI 的 MessageBox 逐字一致。
    /// </summary>
    public static class DesktopService
    {
        /// <summary>
        /// 添加或编辑桌面配置。
        /// allowMissingPath：路径可以不存在（用于“编辑一个已加密的桌面”，其明文文件夹已被加密删除）。
        /// </summary>
        public static OperationResult AddDesktop(
            string? name, string? path, bool enableWallpaper, string? wallpaperPath,
            string? wallpaperStyle, bool isEncrypt, bool isEdit, int indexToChange, bool allowMissingPath)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
                return OperationResult.Fail("请完整填写信息", ExitCodes.Usage);

            if (!Directory.Exists(path) && !allowMissingPath)
                return OperationResult.Fail("你输入的桌面路径不存在", ExitCodes.Usage);

            if (enableWallpaper && !File.Exists(wallpaperPath))
                return OperationResult.Fail("壁纸文件不存在，请检查路径", ExitCodes.Usage);

            var dt = DesktopRepository.Table;
            try
            {
                if (!isEdit)
                {
                    dt.Rows.Add(name, path, enableWallpaper, wallpaperPath ?? "",
                        wallpaperStyle ?? DesktopRepository.DefaultWallpaperStyle, isEncrypt);
                }
                else
                {
                    if (indexToChange < 0 || indexToChange >= dt.Rows.Count)
                        return OperationResult.Fail("要编辑的桌面已不存在，请刷新后重试", ExitCodes.NotFound);

                    var row = dt.Rows[indexToChange];
                    row[DesktopRepository.IdxName] = name;
                    row[DesktopRepository.IdxPath] = path;
                    row[DesktopRepository.IdxEnableWallpaper] = enableWallpaper;
                    row[DesktopRepository.IdxWallpaperPath] = wallpaperPath ?? "";
                    row[DesktopRepository.IdxWallpaperStyle] = wallpaperStyle ?? DesktopRepository.DefaultWallpaperStyle;
                    row[DesktopRepository.IdxEncrypted] = isEncrypt;
                }
            }
            catch (ConstraintException)
            {
                return OperationResult.Fail($"桌面名称已存在：{name}", ExitCodes.Usage);
            }

            DesktopRepository.Save();
            return OperationResult.Ok(isEdit ? $"已保存桌面：{name}" : $"已添加桌面：{name}");
        }

        /// <summary>按名称删除桌面配置（不删除文件夹，也不解除加密）。</summary>
        public static OperationResult DeleteDesktop(string name)
        {
            var row = DesktopRepository.Find(name);
            if (row == null)
                return OperationResult.Fail($"错误: 未找到名为 \"{name}\" 的桌面", ExitCodes.NotFound);

            row.Delete();
            DesktopRepository.Table.AcceptChanges();
            DesktopRepository.Save();
            return OperationResult.Ok($"已删除桌面: {name}");
        }

        /// <summary>按行索引批量删除（供 GUI 多选删除使用，索引为 DataTable 行索引）。</summary>
        public static OperationResult DeleteByRowIndexes(IEnumerable<int> rowIndexes)
        {
            var dt = DesktopRepository.Table;
            var ordered = rowIndexes.Distinct().OrderByDescending(i => i).ToList();
            if (ordered.Count == 0)
                return OperationResult.Ok("未选择任何桌面");

            var names = new List<string>();
            foreach (int i in ordered)
            {
                if (i < 0 || i >= dt.Rows.Count) continue;
                names.Add(DesktopRepository.GetString(dt.Rows[i], DesktopRepository.IdxName) ?? "");
                dt.Rows[i].Delete();
            }
            dt.AcceptChanges();
            DesktopRepository.Save();
            return OperationResult.Ok($"已删除 {names.Count} 个桌面: {string.Join("、", names)}");
        }

        /// <summary>列出全部桌面配置。</summary>
        public static IReadOnlyList<DesktopInfo> List()
        {
            var result = new List<DesktopInfo>();
            foreach (DataRow r in DesktopRepository.Table.Rows)
            {
                if (r.RowState == DataRowState.Deleted) continue;
                result.Add(new DesktopInfo(
                    DesktopRepository.GetString(r, DesktopRepository.IdxName) ?? "",
                    DesktopRepository.GetString(r, DesktopRepository.IdxPath) ?? "",
                    DesktopRepository.GetBool(r, DesktopRepository.IdxEnableWallpaper),
                    DesktopRepository.GetString(r, DesktopRepository.IdxWallpaperPath) ?? "",
                    DesktopRepository.GetString(r, DesktopRepository.IdxWallpaperStyle) ?? "",
                    DesktopRepository.GetBool(r, DesktopRepository.IdxEncrypted)));
            }
            return result;
        }

        /// <summary>把 DataTable 行转换为只读视图（供 GUI 绑定与 Core 服务共用）。</summary>
        public static DesktopInfo? FromRow(DataRow? r)
        {
            if (r == null || r.RowState == DataRowState.Deleted) return null;
            return new DesktopInfo(
                DesktopRepository.GetString(r, DesktopRepository.IdxName) ?? "",
                DesktopRepository.GetString(r, DesktopRepository.IdxPath) ?? "",
                DesktopRepository.GetBool(r, DesktopRepository.IdxEnableWallpaper),
                DesktopRepository.GetString(r, DesktopRepository.IdxWallpaperPath) ?? "",
                DesktopRepository.GetString(r, DesktopRepository.IdxWallpaperStyle) ?? "",
                DesktopRepository.GetBool(r, DesktopRepository.IdxEncrypted));
        }

        /// <summary>按行索引取出桌面视图；索引非法返回 null。</summary>
        public static DesktopInfo? GetByRowIndex(int rowIndex)
        {
            var dt = DesktopRepository.Table;
            if (rowIndex < 0 || rowIndex >= dt.Rows.Count) return null;
            return FromRow(dt.Rows[rowIndex]);
        }

        /// <summary>
        /// 找出当前 Windows 桌面目录对应的已配置桌面（用于离开加密桌面时自动重新加密）。
        /// 未匹配到返回 null。
        /// </summary>
        public static DesktopInfo? DetectCurrentDesktop()
        {
            string current;
            try { current = Environment.GetFolderPath(Environment.SpecialFolder.Desktop); }
            catch { return null; }

            foreach (var d in List())
            {
                if (string.IsNullOrEmpty(d.Path)) continue;
                try
                {
                    if (string.Equals(Path.GetFullPath(d.Path).TrimEnd('\\'),
                                      Path.GetFullPath(current).TrimEnd('\\'),
                                      StringComparison.OrdinalIgnoreCase))
                        return d;
                }
                catch
                {
                    // 路径非法（含无效字符等）：跳过该项，不影响其余桌面
                }
            }
            return null;
        }

        /// <summary>为已有桌面设置自定义壁纸（只写配置，切换时生效）。</summary>
        public static OperationResult SetWallpaper(string name, string? wallpaperPath, string? wallpaperStyle)
        {
            var row = DesktopRepository.Find(name);
            if (row == null)
                return OperationResult.Fail($"错误: 未找到名为 \"{name}\" 的桌面", ExitCodes.NotFound);

            if (string.IsNullOrWhiteSpace(wallpaperPath))
                return OperationResult.Fail("壁纸路径不能为空", ExitCodes.Usage);

            if (!File.Exists(wallpaperPath))
                return OperationResult.Fail("壁纸文件不存在，请检查路径", ExitCodes.Usage);

            string style = string.IsNullOrWhiteSpace(wallpaperStyle)
                ? DesktopRepository.DefaultWallpaperStyle
                : wallpaperStyle;

            if (!WallpaperService.IsValidStyle(style))
                return OperationResult.Fail(
                    $"不支持的显示方式：{style}（可选：{string.Join(" / ", WallpaperService.Styles)}）", ExitCodes.Usage);

            row[DesktopRepository.IdxEnableWallpaper] = true;
            row[DesktopRepository.IdxWallpaperPath] = wallpaperPath;
            row[DesktopRepository.IdxWallpaperStyle] = style;
            DesktopRepository.Save();
            return OperationResult.Ok($"已为桌面 \"{name}\" 设置壁纸: {wallpaperPath}（显示方式: {style}）");
        }

        /// <summary>取消某个桌面的自定义壁纸（只写配置，不修改当前系统壁纸）。</summary>
        public static OperationResult ClearWallpaper(string name)
        {
            var row = DesktopRepository.Find(name);
            if (row == null)
                return OperationResult.Fail($"错误: 未找到名为 \"{name}\" 的桌面", ExitCodes.NotFound);

            row[DesktopRepository.IdxEnableWallpaper] = false;
            row[DesktopRepository.IdxWallpaperPath] = "";
            DesktopRepository.Save();
            return OperationResult.Ok($"已取消桌面 \"{name}\" 的自定义壁纸");
        }

        /// <summary>更新某个桌面的“是否加密”标记并落盘。</summary>
        public static OperationResult SetEncryptedFlag(string name, bool encrypted)
        {
            var row = DesktopRepository.Find(name);
            if (row == null)
                return OperationResult.Fail($"错误: 未找到名为 \"{name}\" 的桌面", ExitCodes.NotFound);

            row[DesktopRepository.IdxEncrypted] = encrypted;
            DesktopRepository.Save();
            return OperationResult.Ok();
        }
    }
}
