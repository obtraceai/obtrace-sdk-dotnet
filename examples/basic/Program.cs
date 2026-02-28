using Obtrace.Sdk;

var cfg = new ObtraceConfig
{
    ApiKey = Environment.GetEnvironmentVariable("OBTRACE_API_KEY") ?? "test-key",
    IngestBaseUrl = Environment.GetEnvironmentVariable("OBTRACE_INGEST_BASE_URL") ?? "https://injet.obtrace.ai",
    ServiceName = "dotnet-example",
    Env = "dev",
};

var client = new ObtraceClient(cfg);
client.Log("info", "dotnet sdk initialized");
client.Metric("example.counter", 1);
client.Span("example.work");
await client.FlushAsync();
