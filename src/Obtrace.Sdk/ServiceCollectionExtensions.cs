using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Obtrace.Sdk;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddObtrace(this IServiceCollection services, ObtraceConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.IngestBaseUrl) || string.IsNullOrWhiteSpace(config.ServiceName))
            throw new ArgumentException("ApiKey, IngestBaseUrl and ServiceName are required.");

        var endpoint = new Uri(config.IngestBaseUrl.TrimEnd('/') + "/otlp");

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(config.ServiceName, serviceVersion: config.ServiceVersion)
            .AddAttributes(BuildResourceAttributes(config));

        services.AddSingleton(config);

        var client = new ObtraceClient(config);
        services.AddSingleton(client);

        services.AddOpenTelemetry()
            .WithTracing(b =>
            {
                b.SetResourceBuilder(resourceBuilder)
                    .AddSource(config.ServiceName)
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddGrpcClientInstrumentation()
                    .AddSqlClientInstrumentation();

                TryAddRedisInstrumentation(b);

                b.AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = OtlpExportProtocol.HttpProtobuf;
                    o.Headers = $"Authorization=Bearer {config.ApiKey}";
                });
            })
            .WithMetrics(b => b
                .SetResourceBuilder(resourceBuilder)
                .AddMeter(config.ServiceName)
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = OtlpExportProtocol.HttpProtobuf;
                    o.Headers = $"Authorization=Bearer {config.ApiKey}";
                }));

        return services;
    }

    public static IServiceCollection AddObtrace(this IServiceCollection services, Action<ObtraceConfig> configure)
    {
        var config = new ObtraceConfig();
        configure(config);
        return services.AddObtrace(config);
    }

    private static void TryAddRedisInstrumentation(TracerProviderBuilder builder)
    {
        try
        {
            var redisAssembly = Assembly.Load("StackExchange.Redis");
            if (redisAssembly != null)
                builder.AddRedisInstrumentation();
        }
        catch
        {
        }
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
}
