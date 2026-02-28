using Microsoft.AspNetCore.Http;

namespace Obtrace.Sdk;

public sealed class AspNetCoreObtraceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ObtraceClient _client;

    public AspNetCoreObtraceMiddleware(RequestDelegate next, ObtraceClient client)
    {
        _next = next;
        _client = client;
    }

    public async Task Invoke(HttpContext context)
    {
        _client.Log("info", "request.start", new SDKContext
        {
            Attrs = new Dictionary<string, object?>
            {
                ["method"] = context.Request.Method,
                ["path"] = context.Request.Path.ToString()
            }
        });

        await _next(context).ConfigureAwait(false);

        _client.Log("info", "request.finish", new SDKContext
        {
            Attrs = new Dictionary<string, object?>
            {
                ["status_code"] = context.Response.StatusCode
            }
        });
    }
}
