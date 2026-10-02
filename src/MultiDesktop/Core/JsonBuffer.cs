using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace MultiDesktop.Core
{
    /// <summary>
    /// 零反射的 JSON 输出缓冲。
    /// 不使用 JsonSerializer 的反射式序列化 —— 那在 NativeAOT 下会因
    /// "Reflection-based serialization has been disabled" 抛 InvalidOperationException。
    /// 直接基于 Utf8JsonWriter 手工写字段，AOT/裁剪下安全。
    /// </summary>
    internal sealed class JsonBuffer : IDisposable
    {
        private static readonly JsonWriterOptions Options = new()
        {
            Indented = true,
            // 不转义中文，便于 AI 与人工直接阅读
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private readonly MemoryStream _stream = new();

        public Utf8JsonWriter Writer { get; }

        public JsonBuffer()
        {
            Writer = new Utf8JsonWriter(_stream, Options);
        }

        /// <summary>结束写入并返回 JSON 文本。</summary>
        public string Complete()
        {
            Writer.Flush();
            return Encoding.UTF8.GetString(_stream.ToArray());
        }

        public void Dispose()
        {
            Writer.Dispose();
            _stream.Dispose();
        }
    }
}
