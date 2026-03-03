using Obtrace.Sdk;
using Xunit;

namespace Obtrace.Sdk.Tests;

public class PropagationTests
{
    [Fact]
    public void EnsurePropagationHeaders_UsesProvidedIds()
    {
        var headers = Propagation.EnsurePropagationHeaders(
            traceId: "0123456789abcdef0123456789abcdef",
            spanId: "0123456789abcdef",
            sessionId: "sess-1"
        );

        Assert.Equal("00-0123456789abcdef0123456789abcdef-0123456789abcdef-01", headers["traceparent"]);
        Assert.Equal("sess-1", headers["x-obtrace-session-id"]);
    }

    [Fact]
    public void EnsurePropagationHeaders_PreservesExistingTraceparent()
    {
        var headers = new Dictionary<string, string>
        {
            ["traceparent"] = "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01",
        };
        var result = Propagation.EnsurePropagationHeaders(headers, sessionId: "sess-1");

        Assert.Equal("00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01", result["traceparent"]);
        Assert.Equal("sess-1", result["x-obtrace-session-id"]);
    }
}
