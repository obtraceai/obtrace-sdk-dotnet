using System.Diagnostics;

namespace Obtrace.Sdk;

public sealed class HttpClientObtraceHandler : DelegatingHandler
{
    private readonly ObtraceClient _client;

    public HttpClientObtraceHandler(ObtraceClient client)
        : base(new HttpClientHandler())
    {
        _client = client;
    }

    public HttpClientObtraceHandler(ObtraceClient client, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        _client = client;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var traceId = Propagation.RandomHex(16);
        var spanId = Propagation.RandomHex(8);
        var traceparent = $"00-{traceId}-{spanId}-01";

        request.Headers.TryAddWithoutValidation("traceparent", traceparent);

        var method = request.Method.ToString();
        var url = request.RequestUri?.ToString() ?? "unknown";
        var host = request.RequestUri?.Host ?? "unknown";
        var path = request.RequestUri?.AbsolutePath ?? "/";

        _client.Log("info", $"http.client.request {method} {url}", new SDKContext
        {
            TraceId = traceId,
            SpanId = spanId,
            Attrs = new Dictionary<string, object?>
            {
                ["http.method"] = method,
                ["http.url"] = url,
                ["http.host"] = host,
                ["http.path"] = path,
            }
        });

        var startNano = OtlpPayloads.NowUnixNano();
        var sw = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        Exception? error = null;

        try
        {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response;
        }
        catch (Exception ex)
        {
            error = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            var endNano = OtlpPayloads.NowUnixNano();
            var statusCode = response is not null ? (int)response.StatusCode : 0;

            var attrs = new Dictionary<string, object?>
            {
                ["http.method"] = method,
                ["http.url"] = url,
                ["http.host"] = host,
                ["http.path"] = path,
                ["http.duration_ms"] = sw.Elapsed.TotalMilliseconds,
            };

            if (response is not null)
            {
                attrs["http.status_code"] = statusCode;
            }

            if (error is not null)
            {
                attrs["exception.type"] = error.GetType().FullName;
                attrs["exception.message"] = error.Message;
            }

            _client.Span(
                $"HTTP {method}",
                traceId: traceId,
                spanId: spanId,
                startUnixNano: startNano,
                endUnixNano: endNano,
                statusCode: error is not null ? 500 : statusCode,
                statusMessage: error?.Message ?? "",
                attrs: attrs
            );
        }
    }
}
