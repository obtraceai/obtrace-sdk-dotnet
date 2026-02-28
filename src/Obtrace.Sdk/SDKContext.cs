namespace Obtrace.Sdk;

public sealed class SDKContext
{
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? SessionId { get; set; }
    public Dictionary<string, object?> Attrs { get; set; } = new();
}
