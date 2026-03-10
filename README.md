# obtrace-sdk-dotnet

C#/.NET backend SDK for Obtrace telemetry transport and instrumentation.

## Scope
- OTLP logs/traces/metrics transport
- Context propagation
- ASP.NET Core middleware baseline

## Design Principle
SDK is thin/dumb.
- No business logic authority in client SDK.
- Policy and product logic are server-side.

## Install

```bash
dotnet add package Obtrace.Sdk
```

Current workspace build:

```bash
dotnet build src/Obtrace.Sdk/Obtrace.Sdk.csproj
```

## Build

```bash
dotnet build src/Obtrace.Sdk/Obtrace.Sdk.csproj
```

## Configuration

Required:
- `ApiKey`
- `IngestBaseUrl`
- `ServiceName`

Recommended:
- `TenantId`
- `ProjectId`
- `AppId`
- `Env`
- `ServiceVersion`

## Quickstart

```csharp
var cfg = new ObtraceConfig
{
    ApiKey = "<API_KEY>",
    IngestBaseUrl = "https://inject.obtrace.ai",
    ServiceName = "dotnet-api"
};

var client = new ObtraceClient(cfg);
client.Log("info", "started");
client.Metric(SemanticMetrics.RuntimeCpuUtilization, 0.41);
client.Span("checkout.charge", attrs: new Dictionary<string, object?>
{
    ["feature.name"] = "checkout",
    ["payment.provider"] = "stripe",
});
await client.FlushAsync();
```

## Canonical metrics and custom spans

- Use `SemanticMetrics.*` for globally normalized metric names.
- Custom spans use `client.Span(..., attrs: ...)`.
- Keep free-form metric names only for application-specific signals outside the shared catalog.

## Frameworks

- ASP.NET Core middleware baseline: `AspNetCoreObtraceMiddleware`
- Reference docs:
  - `docs/frameworks.md`

## Production Hardening

1. Store API keys in secure configuration providers (not appsettings checked into VCS).
2. Separate keys for each environment.
3. Flush on graceful shutdown to minimize dropped events.
4. Validate telemetry and propagation in release smoke tests.

## Troubleshooting

- Missing data: verify `IngestBaseUrl`, auth key, and network egress.
- Missing correlation: inspect propagation headers in outbound requests.
- Debug transport in non-prod by setting `Debug = true`.

## Documentation
- Docs index: `docs/index.md`
- LLM context file: `llm.txt`
- MCP metadata: `mcp.json`

## Reference
