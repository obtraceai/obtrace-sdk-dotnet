# Getting Started

```csharp
using Obtrace.Sdk;

var cfg = new ObtraceConfig
{
    ApiKey = "<API_KEY>",
    IngestBaseUrl = "https://inject.obtrace.ai",
    ServiceName = "dotnet-api"
};

var client = new ObtraceClient(cfg);
client.Log("info", "started");
await client.FlushAsync();
```
