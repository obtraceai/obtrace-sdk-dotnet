namespace Obtrace.Sdk;

public static class OtlpPayloads
{
    public static object BuildLogsPayload(ObtraceConfig cfg, string level, string message, SDKContext? ctx = null)
    {
        var contextAttrs = new Dictionary<string, object?> { ["obtrace.log.level"] = level };
        if (ctx is not null)
        {
            if (!string.IsNullOrWhiteSpace(ctx.TraceId)) contextAttrs["obtrace.trace_id"] = ctx.TraceId;
            if (!string.IsNullOrWhiteSpace(ctx.SpanId)) contextAttrs["obtrace.span_id"] = ctx.SpanId;
            if (!string.IsNullOrWhiteSpace(ctx.SessionId)) contextAttrs["obtrace.session_id"] = ctx.SessionId;
            foreach (var kv in ctx.Attrs) contextAttrs[$"obtrace.attr.{kv.Key}"] = kv.Value;
        }

        return new
        {
            resourceLogs = new[]
            {
                new
                {
                    resource = new { attributes = ToAttrs(Resource(cfg)) },
                    scopeLogs = new[]
                    {
                        new
                        {
                            scope = new { name = "obtrace-sdk-dotnet", version = "1.0.0" },
                            logRecords = new[]
                            {
                                new
                                {
                                    timeUnixNano = NowUnixNano(),
                                    severityText = level.ToUpperInvariant(),
                                    body = new { stringValue = message },
                                    attributes = ToAttrs(contextAttrs),
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    public static object BuildMetricPayload(ObtraceConfig cfg, string name, double value, string unit = "1", SDKContext? ctx = null)
    {
        return new
        {
            resourceMetrics = new[]
            {
                new
                {
                    resource = new { attributes = ToAttrs(Resource(cfg)) },
                    scopeMetrics = new[]
                    {
                        new
                        {
                            scope = new { name = "obtrace-sdk-dotnet", version = "1.0.0" },
                            metrics = new[]
                            {
                                new
                                {
                                    name,
                                    unit,
                                    gauge = new
                                    {
                                        dataPoints = new[]
                                        {
                                            new
                                            {
                                                timeUnixNano = NowUnixNano(),
                                                asDouble = value,
                                                attributes = ToAttrs(ctx?.Attrs ?? new Dictionary<string, object?>()),
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    public static object BuildSpanPayload(ObtraceConfig cfg, string name, string traceId, string spanId, string startUnixNano, string endUnixNano, int? statusCode = null, string statusMessage = "", IDictionary<string, object?>? attrs = null)
    {
        return new
        {
            resourceSpans = new[]
            {
                new
                {
                    resource = new { attributes = ToAttrs(Resource(cfg)) },
                    scopeSpans = new[]
                    {
                        new
                        {
                            scope = new { name = "obtrace-sdk-dotnet", version = "1.0.0" },
                            spans = new[]
                            {
                                new
                                {
                                    traceId,
                                    spanId,
                                    name,
                                    kind = 3,
                                    startTimeUnixNano = startUnixNano,
                                    endTimeUnixNano = endUnixNano,
                                    attributes = ToAttrs(attrs ?? new Dictionary<string, object?>()),
                                    status = new
                                    {
                                        code = (statusCode is not null && statusCode >= 400) ? 2 : 1,
                                        message = statusMessage,
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    public static string NowUnixNano() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() + "000000";

    private static Dictionary<string, object?> Resource(ObtraceConfig cfg)
    {
        var baseAttrs = new Dictionary<string, object?>
        {
            ["service.name"] = cfg.ServiceName,
            ["service.version"] = cfg.ServiceVersion,
            ["deployment.environment"] = cfg.Env ?? "",
            ["runtime.name"] = ".NET",
        };
        if (!string.IsNullOrWhiteSpace(cfg.TenantId)) baseAttrs["obtrace.tenant_id"] = cfg.TenantId;
        if (!string.IsNullOrWhiteSpace(cfg.ProjectId)) baseAttrs["obtrace.project_id"] = cfg.ProjectId;
        if (!string.IsNullOrWhiteSpace(cfg.AppId)) baseAttrs["obtrace.app_id"] = cfg.AppId;
        if (!string.IsNullOrWhiteSpace(cfg.Env)) baseAttrs["obtrace.env"] = cfg.Env;
        return baseAttrs;
    }

    private static IEnumerable<object> ToAttrs(IDictionary<string, object?> attrs)
    {
        foreach (var kv in attrs)
        {
            object value = kv.Value switch
            {
                bool b => new { boolValue = b },
                sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal => new { doubleValue = Convert.ToDouble(kv.Value) },
                _ => new { stringValue = kv.Value?.ToString() ?? "" }
            };
            yield return new { key = kv.Key, value };
        }
    }
}
