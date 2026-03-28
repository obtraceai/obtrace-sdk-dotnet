using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Obtrace.Sdk;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddObtrace(this IServiceCollection services, ObtraceConfig config)
    {
        var client = new ObtraceClient(config);
        services.AddSingleton(config);
        services.AddSingleton(client);
        return services;
    }

    public static IServiceCollection AddObtrace(this IServiceCollection services, Action<ObtraceConfig> configure)
    {
        var config = new ObtraceConfig();
        configure(config);
        return services.AddObtrace(config);
    }

    public static IApplicationBuilder UseObtrace(this IApplicationBuilder app)
    {
        app.UseMiddleware<AspNetCoreObtraceMiddleware>();
        return app;
    }
}
