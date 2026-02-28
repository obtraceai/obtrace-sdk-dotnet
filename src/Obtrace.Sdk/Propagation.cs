using System.Security.Cryptography;

namespace Obtrace.Sdk;

public static class Propagation
{
    public static string RandomHex(int bytes)
    {
        Span<byte> buffer = stackalloc byte[bytes];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }

    public static Dictionary<string, string> EnsurePropagationHeaders(
        IDictionary<string, string>? headers = null,
        string? traceId = null,
        string? spanId = null,
        string? sessionId = null)
    {
        var outHeaders = headers is null ? new Dictionary<string, string>() : new Dictionary<string, string>(headers);
        if (!outHeaders.ContainsKey("traceparent"))
        {
            outHeaders["traceparent"] = $"00-{(traceId is { Length: 32 } ? traceId : RandomHex(16))}-{(spanId is { Length: 16 } ? spanId : RandomHex(8))}-01";
        }

        if (!string.IsNullOrWhiteSpace(sessionId) && !outHeaders.ContainsKey("x-obtrace-session-id"))
        {
            outHeaders["x-obtrace-session-id"] = sessionId;
        }
        return outHeaders;
    }
}
