using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Obtrace.Sdk;

public sealed class ObtraceClient : IDisposable
{
    private static int _instanceCount;
    private readonly ObtraceConfig _cfg;
    private readonly OtelSetup _otel;
    private bool _disposed;
    private readonly ConcurrentDictionary<string, Gauge<double>> _gaugeCache = new();

    private volatile bool _initialized;
    public bool Initialized => _initialized;

    public ObtraceClient(ObtraceConfig cfg)
    {
        if (Interlocked.Increment(ref _instanceCount) > 1)
            Console.Error.WriteLine("[obtrace-sdk-dotnet] WARNING: ObtraceClient created more than once. Use a single instance.");

        _cfg = cfg;
        _otel = new OtelSetup(cfg);
        _ = Task.Run(HandshakeAsync);
    }

    private async Task HandshakeAsync()
    {
        var baseUrl = _cfg.IngestBaseUrl.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl)) return;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var body = System.Text.Json.JsonSerializer.Serialize(new
            {
                sdk = "obtrace-sdk-dotnet",
                sdk_version = "1.2.0",
                service_name = _cfg.ServiceName,
                service_version = _cfg.ServiceVersion,
                runtime = "dotnet",
                runtime_version = Environment.Version.ToString(),
            });
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/init");
            req.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _cfg.ApiKey);
            var resp = await client.SendAsync(req);
            if (resp.IsSuccessStatusCode)
            {
                _initialized = true;
                if (_cfg.Debug) Console.WriteLine("[obtrace-sdk-dotnet] init handshake OK");
            }
            else if (_cfg.Debug)
            {
                Console.Error.WriteLine($"[obtrace-sdk-dotnet] init handshake failed: {(int)resp.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            if (_cfg.Debug) Console.Error.WriteLine($"[obtrace-sdk-dotnet] init handshake error: {ex.Message}");
        }
    }

    public ActivitySource ActivitySource => _otel.ActivitySource;
    public Meter Meter => _otel.Meter;

    public void Log(string level, string message, IDictionary<string, object?>? attrs = null)
    {
        using var activity = _otel.ActivitySource.StartActivity("log");
        if (activity is null) return;
        var tags = new ActivityTagsCollection
        {
            { "obtrace.log.level", level },
            { "log.message", message },
        };
        if (attrs is not null)
        {
            foreach (var kv in attrs)
                tags[kv.Key] = kv.Value?.ToString() ?? "";
        }
        activity.AddEvent(new ActivityEvent(message, tags: tags));
    }

    public void Metric(string name, double value, string unit = "1")
    {
        if (_cfg.ValidateSemanticMetrics && _cfg.Debug && !SemanticMetrics.IsSemanticMetric(name))
            Console.Error.WriteLine($"[obtrace-sdk-dotnet] non-canonical metric name: {name}");

        var gauge = _gaugeCache.GetOrAdd(name, n => _otel.Meter.CreateGauge<double>(n, unit));
        gauge.Record(value);
    }

    public Activity? Span(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return _otel.ActivitySource.StartActivity(name, kind);
    }

    public bool Flush(int timeoutMilliseconds = 10000)
    {
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _otel.Dispose();
    }
}
