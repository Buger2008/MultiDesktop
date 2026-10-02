namespace MultiDesktop
{
    /// <summary>
    /// 颜色模式中文名 → WinForms SystemColorMode 的映射。
    /// 依赖 WinForms 类型，属于纯 UI 层，因此不放在 Core。
    /// </summary>
    public static class ColorModeMap
    {
        public static readonly Dictionary<string, SystemColorMode> Map = new Dictionary<string, SystemColorMode>
        {
            { "浅色", SystemColorMode.Classic },
            { "跟随系统", SystemColorMode.System },
            { "深色", SystemColorMode.Dark },
        };
    }
}
