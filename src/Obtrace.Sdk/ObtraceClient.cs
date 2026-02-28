using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Obtrace.Sdk;

public sealed class ObtraceClient
{
    private readonly ObtraceConfig _cfg;
    private readonly HttpClient _http;
    private readonly object _lock = new();
    private readonly List<(string Endpoint, object Payload)> _queue = new();

    public ObtraceClient(ObtraceConfig cfg, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(cfg.ApiKey) || string.IsNullOrWhiteSpace(cfg.IngestBaseUrl) || string.IsNullOrWhiteSpace(cfg.ServiceName))
        {
            throw new ArgumentException("ApiKey, IngestBaseUrl and ServiceName are required.");
        }

        _cfg = cfg;
        _http = httpClient ?? new HttpClient();
        _http.Timeout = TimeSpan.FromMilliseconds(_cfg.RequestTimeoutMs);
    }

    public void Log(string level, string message, SDKContext? context = null) =>
        Enqueue("/otlp/v1/logs", OtlpPayloads.BuildLogsPayload(_cfg, level, message, context));

    public void Metric(string name, double value, string unit = "1", SDKContext? context = null) =>
        Enqueue("/otlp/v1/metrics", OtlpPayloads.BuildMetricPayload(_cfg, name, value, unit, context));

    public (string TraceId, string SpanId) Span(string name, string? traceId = null, string? spanId = null, string? startUnixNano = null, string? endUnixNano = null, int? statusCode = null, string statusMessage = "", IDictionary<string, object?>? attrs = null)
    {
        var trace = (traceId is { Length: 32 }) ? traceId : Propagation.RandomHex(16);
        var span = (spanId is { Length: 16 }) ? spanId : Propagation.RandomHex(8);
        var start = startUnixNano ?? OtlpPayloads.NowUnixNano();
        var end = endUnixNano ?? OtlpPayloads.NowUnixNano();
        Enqueue("/otlp/v1/traces", OtlpPayloads.BuildSpanPayload(_cfg, name, trace, span, start, end, statusCode, statusMessage, attrs));
        return (trace, span);
    }

    public Dictionary<string, string> InjectPropagation(IDictionary<string, string>? headers = null, string? traceId = null, string? spanId = null, string? sessionId = null) =>
        Propagation.EnsurePropagationHeaders(headers, traceId, spanId, sessionId);

    public async Task FlushAsync(CancellationToken ct = default)
    {
        List<(string Endpoint, object Payload)> batch;
        lock (_lock)
        {
            batch = new List<(string Endpoint, object Payload)>(_queue);
            _queue.Clear();
        }

        foreach (var item in batch)
        {
            await SendAsync(item.Endpoint, item.Payload, ct).ConfigureAwait(false);
        }
    }

    public Task ShutdownAsync(CancellationToken ct = default) => FlushAsync(ct);

    private void Enqueue(string endpoint, object payload)
    {
        lock (_lock)
        {
            if (_queue.Count >= _cfg.MaxQueueSize) _queue.RemoveAt(0);
            _queue.Add((endpoint, payload));
        }
    }

    private async Task SendAsync(string endpoint, object payload, CancellationToken ct)
    {
        var url = $"{_cfg.IngestBaseUrl.TrimEnd('/')}{endpoint}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.ApiKey);
        foreach (var header in _cfg.DefaultHeaders) req.Headers.TryAddWithoutValidation(header.Key, header.Value);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (_cfg.Debug && ((int)res.StatusCode) >= 300)
            {
                var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                Console.Error.WriteLine($"[obtrace-sdk-dotnet] status={(int)res.StatusCode} endpoint={endpoint} body={body}");
            }
        }
        catch (Exception ex) when (_cfg.Debug)
        {
            Console.Error.WriteLine($"[obtrace-sdk-dotnet] send failed endpoint={endpoint} err={ex.Message}");
        }
    }
}
