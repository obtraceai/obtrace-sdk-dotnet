# Frameworks

ASP.NET Core middleware baseline:

```csharp
app.Use(async (ctx, next) =>
{
    await new AspNetCoreObtraceMiddleware(next, client).Invoke(ctx);
});
```
