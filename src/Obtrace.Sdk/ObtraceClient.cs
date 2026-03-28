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

    public ObtraceClient(ObtraceConfig cfg)
    {
        if (Interlocked.Increment(ref _instanceCount) > 1)
            Console.Error.WriteLine("[obtrace-sdk-dotnet] WARNING: ObtraceClient created more than once. Use a single instance.");

        _cfg = cfg;
        _otel = new OtelSetup(cfg);
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
        try
        {
            _otel.TracerProvider?.Shutdown(timeoutMilliseconds);
        }
        catch { }
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _otel.Dispose();
    }
}
