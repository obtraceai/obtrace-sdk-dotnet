using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Obtrace.Sdk;

public sealed class OtelSetup : IDisposable
{
    private readonly Lazy<TracerProvider> _tracerProvider;
    private readonly Lazy<MeterProvider> _meterProvider;

    public ActivitySource ActivitySource { get; }
    public Meter Meter { get; }

    public TracerProvider TracerProvider => _tracerProvider.Value;
    public MeterProvider MeterProvider => _meterProvider.Value;

    public OtelSetup(ObtraceConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.ApiKey) || string.IsNullOrWhiteSpace(cfg.ServiceName))
            throw new ArgumentException("ApiKey and ServiceName are required.");

        var endpoint = new Uri(cfg.IngestBaseUrl.TrimEnd('/') + "/otlp");

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(cfg.ServiceName, serviceVersion: cfg.ServiceVersion)
            .AddAttributes(BuildResourceAttributes(cfg));

        ActivitySource = new ActivitySource(cfg.ServiceName, cfg.ServiceVersion);
        Meter = new Meter(cfg.ServiceName, cfg.ServiceVersion);

        _tracerProvider = new Lazy<TracerProvider>(() =>
            OpenTelemetry.Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddSource(cfg.ServiceName)
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = OtlpExportProtocol.HttpProtobuf;
                    o.Headers = $"Authorization=Bearer {cfg.ApiKey}";
                })
                .Build()!);

        _meterProvider = new Lazy<MeterProvider>(() =>
            OpenTelemetry.Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddMeter(cfg.ServiceName)
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = OtlpExportProtocol.HttpProtobuf;
                    o.Headers = $"Authorization=Bearer {cfg.ApiKey}";
                })
                .Build()!);
    }

    private static IEnumerable<KeyValuePair<string, object>> BuildResourceAttributes(ObtraceConfig cfg)
    {
        var attrs = new List<KeyValuePair<string, object>>
        {
            new("deployment.environment", cfg.Env ?? ""),
            new("runtime.name", ".NET"),
        };
        if (!string.IsNullOrWhiteSpace(cfg.TenantId)) attrs.Add(new("obtrace.tenant_id", cfg.TenantId));
        if (!string.IsNullOrWhiteSpace(cfg.ProjectId)) attrs.Add(new("obtrace.project_id", cfg.ProjectId));
        if (!string.IsNullOrWhiteSpace(cfg.AppId)) attrs.Add(new("obtrace.app_id", cfg.AppId));
        if (!string.IsNullOrWhiteSpace(cfg.Env)) attrs.Add(new("obtrace.env", cfg.Env));
        return attrs;
    }

    public void Dispose()
    {
        ActivitySource.Dispose();
        Meter.Dispose();
        if (_tracerProvider.IsValueCreated) _tracerProvider.Value.Dispose();
        if (_meterProvider.IsValueCreated) _meterProvider.Value.Dispose();
    }
}
