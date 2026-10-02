namespace MultiDesktop.Core
{
    /// <summary>
    /// 核心层统一的返回结果。
    /// 核心层不弹任何对话框：提示文案由调用方决定呈现方式
    /// （GUI 用 Title/IsError 弹 MessageBox、CLI 打印文本或输出 JSON）。
    /// </summary>
    public sealed class OperationResult
    {
        /// <summary>操作是否成功。</summary>
        public bool Success { get; private init; }

        /// <summary>面向用户的提示文案（与原 GUI 的 MessageBox 文案逐字一致）。</summary>
        public string Message { get; private init; } = "";

        /// <summary>CLI 退出码。</summary>
        public int ExitCode { get; private init; }

        /// <summary>供 --json 输出的结构化数据（可为 null）。</summary>
        public object? Data { get; private init; }

        /// <summary>GUI 弹窗标题（CLI 忽略）。为空时 GUI 使用无标题的 MessageBox，与原行为一致。</summary>
        public string Title { get; private init; } = "";

        /// <summary>GUI 弹窗是否使用错误图标（CLI 忽略）。</summary>
        public bool IsError { get; private init; }

        public static OperationResult Ok(string message = "", object? data = null, string title = "")
            => new() { Success = true, Message = message, ExitCode = ExitCodes.Success, Data = data, Title = title };

        public static OperationResult Fail(string message, int exitCode = ExitCodes.Failure,
            object? data = null, string title = "", bool isError = true)
            => new() { Success = false, Message = message, ExitCode = exitCode, Data = data, Title = title, IsError = isError };
    }

    /// <summary>
    /// CLI 退出码约定：便于 AI / 脚本稳定判断失败原因，无需解析文案。
    /// </summary>
    public static class ExitCodes
    {
        /// <summary>成功。</summary>
        public const int Success = 0;
        /// <summary>一般错误（IO 失败、加密失败等）。</summary>
        public const int Failure = 1;
        /// <summary>参数用法错误（缺少参数、取值非法、未知命令）。</summary>
        public const int Usage = 2;
        /// <summary>目标桌面不存在。</summary>
        public const int NotFound = 3;
        /// <summary>密码错误。</summary>
        public const int BadPassword = 4;
        /// <summary>需要密码，但当前禁止交互式输入（--no-input / 非终端）。</summary>
        public const int InputRequired = 5;
        /// <summary>用户主动取消。</summary>
        public const int Cancelled = 6;
    }
}
