using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Obtrace.Sdk;

public sealed class ObtraceClient : IDisposable, IAsyncDisposable
{
    private readonly ObtraceConfig _cfg;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly object _lock = new();
    private readonly Queue<(string Endpoint, object Payload)> _queue = new();
    private readonly TextWriter? _originalOut;
    private readonly TextWriter? _originalError;
    private bool _disposed;
    private int _circuitFailures;
    private long _circuitOpenUntil;

    public ObtraceClient(ObtraceConfig cfg, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(cfg.ApiKey) || string.IsNullOrWhiteSpace(cfg.IngestBaseUrl) || string.IsNullOrWhiteSpace(cfg.ServiceName))
        {
            throw new ArgumentException("ApiKey, IngestBaseUrl and ServiceName are required.");
        }

        _cfg = cfg;
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient();
        _http.Timeout = TimeSpan.FromMilliseconds(_cfg.RequestTimeoutMs);

        if (_cfg.AutoCaptureConsole)
        {
            _originalOut = Console.Out;
            _originalError = Console.Error;
            Console.SetOut(new ObtraceTextWriter(_originalOut, this, "info"));
            Console.SetError(new ObtraceTextWriter(_originalError, this, "error"));
        }
    }

    private static string Truncate(string s, int max)
    {
        if (s is null || s.Length <= max) return s!;
        return s[..max] + "...[truncated]";
    }

    public void Log(string level, string message, SDKContext? context = null) =>
        Enqueue("/otlp/v1/logs", OtlpPayloads.BuildLogsPayload(_cfg, level, Truncate(message, 32768), context));

    public void Metric(string name, double value, string unit = "1", SDKContext? context = null) =>
        EnqueueMetric(name, value, unit, context);

    private void EnqueueMetric(string name, double value, string unit, SDKContext? context)
    {
        if (_cfg.ValidateSemanticMetrics && _cfg.Debug && !SemanticMetrics.IsSemanticMetric(name))
        {
            Console.Error.WriteLine($"[obtrace-sdk-dotnet] non-canonical metric name: {name}");
        }

        Enqueue("/otlp/v1/metrics", OtlpPayloads.BuildMetricPayload(_cfg, Truncate(name, 1024), value, unit, context));
    }

    public (string TraceId, string SpanId) Span(string name, string? traceId = null, string? spanId = null, string? startUnixNano = null, string? endUnixNano = null, int? statusCode = null, string statusMessage = "", IDictionary<string, object?>? attrs = null)
    {
        var trace = (traceId is { Length: 32 }) ? traceId : Propagation.RandomHex(16);
        var span = (spanId is { Length: 16 }) ? spanId : Propagation.RandomHex(8);
        var start = startUnixNano ?? OtlpPayloads.NowUnixNano();
        var end = endUnixNano ?? OtlpPayloads.NowUnixNano();
        var truncatedName = Truncate(name, 32768);
        if (attrs != null)
        {
            var copy = new Dictionary<string, object?>(attrs);
            foreach (var key in copy.Keys)
            {
                if (copy[key] is string sv) copy[key] = Truncate(sv, 4096);
            }
            attrs = copy;
        }
        Enqueue("/otlp/v1/traces", OtlpPayloads.BuildSpanPayload(_cfg, truncatedName, trace, span, start, end, statusCode, statusMessage, attrs));
        return (trace, span);
    }

    public Dictionary<string, string> InjectPropagation(IDictionary<string, string>? headers = null, string? traceId = null, string? spanId = null, string? sessionId = null) =>
        Propagation.EnsurePropagationHeaders(headers, traceId, spanId, sessionId);

    public async Task FlushAsync(CancellationToken ct = default)
    {
        (string Endpoint, object Payload)[] batch;
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now < _circuitOpenUntil) return;
            var halfOpen = _circuitFailures >= 5;
            if (halfOpen)
            {
                if (_queue.Count == 0) return;
                var item = _queue.Dequeue();
                batch = new[] { item };
            }
            else
            {
                batch = _queue.ToArray();
                _queue.Clear();
            }
        }

        foreach (var item in batch)
        {
            var success = await SendAsyncWithResult(item.Endpoint, item.Payload, ct).ConfigureAwait(false);
            if (success)
            {
                lock (_lock)
                {
                    if (_circuitFailures > 0 && _cfg.Debug)
                        Console.Error.WriteLine("[obtrace-sdk-dotnet] circuit breaker closed");
                    _circuitFailures = 0;
                    _circuitOpenUntil = 0;
                }
            }
            else
            {
                lock (_lock)
                {
                    _circuitFailures++;
                    if (_circuitFailures >= 5)
                    {
                        _circuitOpenUntil = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 30000;
                        if (_cfg.Debug) Console.Error.WriteLine("[obtrace-sdk-dotnet] circuit breaker opened");
                    }
                }
            }
        }
    }

    public Task ShutdownAsync(CancellationToken ct = default) => FlushAsync(ct);

    private void Enqueue(string endpoint, object payload)
    {
        lock (_lock)
        {
            if (_queue.Count >= _cfg.MaxQueueSize) _queue.Dequeue();
            _queue.Enqueue((endpoint, payload));
        }
    }

    private async Task<bool> SendAsyncWithResult(string endpoint, object payload, CancellationToken ct)
    {
        var url = $"{_cfg.IngestBaseUrl.TrimEnd('/')}{endpoint}";
        var json = JsonSerializer.Serialize(payload);
        const int maxAttempts = 2;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.ApiKey);
                foreach (var header in _cfg.DefaultHeaders) req.Headers.TryAddWithoutValidation(header.Key, header.Value);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
                if (((int)res.StatusCode) >= 300)
                {
                    if (_cfg.Debug)
                    {
                        var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        Console.Error.WriteLine($"[obtrace-sdk-dotnet] status={(int)res.StatusCode} endpoint={endpoint} body={body}");
                    }
                    return false;
                }
                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && attempt < maxAttempts)
            {
                if (_cfg.Debug)
                    Console.Error.WriteLine($"[obtrace-sdk-dotnet] transient failure endpoint={endpoint} attempt={attempt} err={ex.Message}");
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (_cfg.Debug)
                    Console.Error.WriteLine($"[obtrace-sdk-dotnet] send failed endpoint={endpoint} err={ex.Message}");
                return false;
            }
        }
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RestoreConsole();
        if (_ownsHttp) _http.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        RestoreConsole();
        await FlushAsync().ConfigureAwait(false);
        if (_ownsHttp) _http.Dispose();
    }

    private void RestoreConsole()
    {
        if (_originalOut is not null) Console.SetOut(_originalOut);
        if (_originalError is not null) Console.SetError(_originalError);
    }
}
