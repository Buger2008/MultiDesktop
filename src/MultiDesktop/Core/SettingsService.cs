using System.Data;

namespace MultiDesktop.Core
{
    /// <summary>
    /// AppSettings.xml 的唯一读写出口。
    /// 不依赖 WinForms：颜色名与 SystemColorMode 的映射由 UI 层负责。
    /// </summary>
    public static class SettingsService
    {
        public const string KeyColor = "Color";
        public const string KeyExitMode = "ExitMode";

        // 颜色模式（与 GUI 下拉框文案一致）
        public const string ColorFollowSystem = "跟随系统";
        public const string ColorLight = "浅色";
        public const string ColorDark = "深色";

        // 关闭行为（与 GUI 下拉框文案一致）
        public const string ExitAsk = "询问";
        public const string ExitMinimize = "最小化到后台";
        public const string ExitQuit = "退出程序";

        private static DataTable? _table;

        /// <summary>当前设置表（首次访问时自动加载，文件不存在时按默认值创建）。</summary>
        public static DataTable Table => _table ??= Load();

        /// <summary>丢弃内存中的表，下次访问 Table 时重新加载。</summary>
        public static void Reset() => _table = null;

        /// <summary>加载 AppSettings.xml；不存在时创建默认值并落盘。</summary>
        public static DataTable Load()
        {
            var dt = new DataTable();
            if (File.Exists(AppPaths.AppSettings))
            {
                dt.ReadXml(AppPaths.AppSettings);
            }
            else
            {
                dt.Columns.Add("Key", typeof(string));
                dt.Columns.Add("Value", typeof(int));
                dt.TableName = "AppSettings";
                dt.Rows.Add(KeyColor, 0);
                dt.Rows.Add(KeyExitMode, 0);
                dt.WriteXml(AppPaths.AppSettings, XmlWriteMode.WriteSchema);
            }

            dt.PrimaryKey = new[] { dt.Columns["Key"]! };
            return dt;
        }

        /// <summary>写回 AppSettings.xml（唯一的持久化出口）。</summary>
        public static void Save() => Table.WriteXml(AppPaths.AppSettings, XmlWriteMode.WriteSchema);

        /// <summary>读取某项设置的数值；缺失或非法时返回 fallback。</summary>
        public static int GetNum(string key, int fallback = 0)
        {
            var row = Table.Rows.Find(key);
            if (row == null || row["Value"] is DBNull) return fallback;
            try { return Convert.ToInt16(row["Value"]); }
            catch { return fallback; }
        }

        /// <summary>写入某项设置的数值并落盘。</summary>
        public static void SetNum(string key, int value)
        {
            Table.Rows.Find(key)!["Value"] = value;
            Save();
        }

        /// <summary>
        /// 批量更新设置并只落盘一次（供 GUI 设置窗口“保存”与 CLI settings 命令共用）。
        /// 传 null 表示该项不变。
        /// </summary>
        public static void Update(int? colorNum = null, int? exitModeNum = null)
        {
            if (colorNum.HasValue) Table.Rows.Find(KeyColor)!["Value"] = colorNum.Value;
            if (exitModeNum.HasValue) Table.Rows.Find(KeyExitMode)!["Value"] = exitModeNum.Value;
            Save();
        }

        // ===== 颜色模式 =====
        public static string GetColorName(int n) => n switch
        {
            0 => ColorFollowSystem,
            1 => ColorLight,
            2 => ColorDark,
            _ => ColorFollowSystem,
        };

        public static int GetColorNum(string? name) => name switch
        {
            ColorLight => 1,
            ColorDark => 2,
            _ => 0,
        };

        // ===== 关闭行为 =====
        public static string GetExitModeName(int n) => n switch
        {
            0 => ExitAsk,
            1 => ExitMinimize,
            2 => ExitQuit,
            _ => ExitAsk,
        };

        public static int GetExitModeNum(string? name) => name switch
        {
            ExitMinimize => 1,
            ExitQuit => 2,
            _ => 0,
        };
    }
}
