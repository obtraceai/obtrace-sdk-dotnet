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
    private readonly TracerProvider _tracerProvider;
    private readonly MeterProvider _meterProvider;

    public ActivitySource ActivitySource { get; }
    public Meter Meter { get; }

    public OtelSetup(ObtraceConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.ApiKey) || string.IsNullOrWhiteSpace(cfg.IngestBaseUrl) || string.IsNullOrWhiteSpace(cfg.ServiceName))
            throw new ArgumentException("ApiKey, IngestBaseUrl and ServiceName are required.");

        var endpoint = new Uri(cfg.IngestBaseUrl.TrimEnd('/') + "/otlp");

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(cfg.ServiceName, serviceVersion: cfg.ServiceVersion)
            .AddAttributes(BuildResourceAttributes(cfg));

        ActivitySource = new ActivitySource(cfg.ServiceName, cfg.ServiceVersion);
        Meter = new Meter(cfg.ServiceName, cfg.ServiceVersion);

        _tracerProvider = OpenTelemetry.Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(cfg.ServiceName)
            .AddOtlpExporter(o =>
            {
                o.Endpoint = endpoint;
                o.Protocol = OtlpExportProtocol.HttpProtobuf;
                o.Headers = $"Authorization=Bearer {cfg.ApiKey}";
            })
            .Build()!;

        _meterProvider = OpenTelemetry.Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(cfg.ServiceName)
            .AddOtlpExporter(o =>
            {
                o.Endpoint = endpoint;
                o.Protocol = OtlpExportProtocol.HttpProtobuf;
                o.Headers = $"Authorization=Bearer {cfg.ApiKey}";
            })
            .Build()!;
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
        _tracerProvider.Dispose();
        _meterProvider.Dispose();
    }
}
