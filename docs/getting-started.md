# Getting Started

```csharp
using Obtrace.Sdk;

var cfg = new ObtraceConfig
{
    ApiKey = "<API_KEY>",
    IngestBaseUrl = "https://injet.obtrace.ai",
    ServiceName = "dotnet-api"
};

var client = new ObtraceClient(cfg);
client.Log("info", "started");
await client.FlushAsync();
```
