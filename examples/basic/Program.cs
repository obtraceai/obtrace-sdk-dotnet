using Obtrace.Sdk;

var cfg = new ObtraceConfig
{
    ApiKey = Environment.GetEnvironmentVariable("OBTRACE_API_KEY") ?? "test-key",
    ServiceName = "dotnet-example",
    Env = "dev",
};

var client = new ObtraceClient(cfg);
client.Log("info", "dotnet sdk initialized");
client.Metric(SemanticMetrics.RuntimeCpuUtilization, 0.41);
client.Span("checkout.charge", attrs: new Dictionary<string, object?>
{
    ["feature.name"] = "checkout",
    ["payment.provider"] = "stripe",
});
await client.FlushAsync();
