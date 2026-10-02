using System.Data;

namespace MultiDesktop.Core
{
    /// <summary>
    /// DesktopList.xml 的唯一读写出口。
    /// 统一了原先 frmMain 构造函数与 Program.InitDesktopListForCli 两份重复的建表/读表逻辑。
    /// 加载时统一建立主键（桌面名称）：重名被拒绝，避免同名桌面共用同一个加密包 id 而互相覆盖。
    /// </summary>
    public static class DesktopRepository
    {
        // ===== 列名常量：避免各处硬编码中文字符串 =====
        public const string ColName = "桌面名称";
        public const string ColPath = "桌面路径";
        public const string ColEnableWallpaper = "是否开启自定义壁纸";
        public const string ColWallpaperPath = "自定义壁纸地址";
        public const string ColWallpaperStyle = "壁纸显示方式";
        public const string ColEncrypted = "是否加密";

        // ===== 列索引：与列顺序一致，保持与旧代码相同的按索引访问语义 =====
        public const int IdxName = 0;
        public const int IdxPath = 1;
        public const int IdxEnableWallpaper = 2;
        public const int IdxWallpaperPath = 3;
        public const int IdxWallpaperStyle = 4;
        public const int IdxEncrypted = 5;

        /// <summary>壁纸显示方式默认值。</summary>
        public const string DefaultWallpaperStyle = "填充";

        private static DataTable? _table;

        /// <summary>当前桌面配置表（首次访问时自动从磁盘加载）。</summary>
        public static DataTable Table => _table ??= Load();

        /// <summary>丢弃内存中的表，下次访问 Table 时重新加载。</summary>
        public static void Reset() => _table = null;

        /// <summary>从 DesktopList.xml 加载；文件不存在时建立空表。</summary>
        public static DataTable Load()
        {
            var dt = new DataTable();
            if (File.Exists(AppPaths.DesktopList))
            {
                dt.ReadXml(AppPaths.DesktopList);
                EnsureColumns(dt);
            }
            else
            {
                dt.TableName = "DesktopList";
                AddAllColumns(dt);
            }

            try
            {
                dt.PrimaryKey = new[] { dt.Columns[ColName]! };
            }
            catch (Exception ex) when (ex is ConstraintException or ArgumentException)
            {
                // 旧版 GUI 允许重名，配置文件里可能已经存在重复名称
                throw new InvalidOperationException(
                    $"DesktopList.xml 中存在重复的桌面名称：{AppPaths.DesktopList}\n" +
                    "重复名称会让多个桌面共用同一个加密包而互相覆盖，请先手工修正该文件后重试。", ex);
            }
            return dt;
        }

        /// <summary>写回 DesktopList.xml（唯一的持久化出口）。</summary>
        public static void Save() => Table.WriteXml(AppPaths.DesktopList, XmlWriteMode.WriteSchema);

        private static void AddAllColumns(DataTable dt)
        {
            dt.Columns.Add(ColName, typeof(string));
            dt.Columns.Add(ColPath, typeof(string));
            dt.Columns.Add(ColEnableWallpaper, typeof(bool));
            dt.Columns.Add(ColWallpaperPath, typeof(string));
            dt.Columns.Add(ColWallpaperStyle, typeof(string));
            dt.Columns.Add(ColEncrypted, typeof(bool));
        }

        /// <summary>确保 DataTable 包含所有列（兼容旧版 DesktopList.xml 缺少新列的情况）。</summary>
        public static void EnsureColumns(DataTable dt)
        {
            if (!dt.Columns.Contains(ColEnableWallpaper))
                dt.Columns.Add(ColEnableWallpaper, typeof(bool));
            if (!dt.Columns.Contains(ColWallpaperPath))
                dt.Columns.Add(ColWallpaperPath, typeof(string));
            if (!dt.Columns.Contains(ColWallpaperStyle))
                dt.Columns.Add(ColWallpaperStyle, typeof(string));
            if (!dt.Columns.Contains(ColEncrypted))
            {
                dt.Columns.Add(ColEncrypted, typeof(bool));
                // 旧版 XML 没有该列，读取后已有行会是 DBNull，统一补 false
                foreach (DataRow r in dt.Rows)
                    if (r[ColEncrypted] is DBNull)
                        r[ColEncrypted] = false;
            }
        }

        /// <summary>按名称查找行；不存在返回 null。</summary>
        public static DataRow? Find(string name) => Table.Rows.Find(name);

        public static bool Exists(string name) => Find(name) != null;

        /// <summary>安全读取行中的布尔值（兼容旧版 XML 缺少列/值为空的情况）。</summary>
        public static bool GetBool(DataRow row, int index)
            => row.ItemArray.Length > index && row[index] is not DBNull && Convert.ToBoolean(row[index]);

        /// <summary>安全读取行中的字符串值。</summary>
        public static string? GetString(DataRow row, int index)
            => row.ItemArray.Length > index && row[index] is not DBNull ? row[index]?.ToString() : null;
    }
}
