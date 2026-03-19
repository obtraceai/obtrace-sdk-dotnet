namespace Obtrace.Sdk;

public sealed class ObtraceConfig
{
    public string ApiKey { get; set; } = "";
    public string IngestBaseUrl { get; set; } = "";
    public string? TenantId { get; set; }
    public string? ProjectId { get; set; }
    public string? AppId { get; set; }
    public string Env { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public string ServiceVersion { get; set; } = "1.0.0";
    public int MaxQueueSize { get; set; } = 1000;
    public int RequestTimeoutMs { get; set; } = 5000;
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
    public bool ValidateSemanticMetrics { get; set; }
    public bool Debug { get; set; }
}
