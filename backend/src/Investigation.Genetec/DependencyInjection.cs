using Investigation.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Investigation.Genetec;

public static class GenetecServiceCollectionExtensions
{
    public static IServiceCollection AddGenetecIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GenetecOptions>(configuration.GetSection(GenetecOptions.SectionName));
        services.Configure<GenetecModeOptions>(configuration.GetSection(GenetecModeOptions.SectionName));
        var mode = configuration["Genetec:Mode"] ?? "Mock";
        var transport = configuration["Genetec:RealTransport"] ?? "WebSdk";

        if (!mode.Equals("Real", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IGenetecService, MockGenetecService>();
            return services;
        }

        if (transport.Equals("Platform", StringComparison.OrdinalIgnoreCase))
        {
#if GENETEC_PLATFORM_SDK
            services.AddSingleton<IGenetecService, PlatformSdkGenetecService>();
#else
            services.AddSingleton<IGenetecService, PlatformSdkUnavailableService>();
#endif
            return services;
        }

        services.AddHttpClient("GenetecWebSdk", (provider, client) => ConfigureClient(provider, client, webSdk: true))
            .ConfigurePrimaryHttpMessageHandler(CreateHandler);
        services.AddHttpClient("GenetecMediaGateway", (provider, client) => ConfigureClient(provider, client, webSdk: false))
            .ConfigurePrimaryHttpMessageHandler(CreateHandler);
        services.AddSingleton<IGenetecService>(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            return new WebSdkGenetecService(
                factory.CreateClient("GenetecWebSdk"),
                factory.CreateClient("GenetecMediaGateway"),
                provider.GetRequiredService<IOptions<GenetecOptions>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WebSdkGenetecService>>());
        });
        return services;
    }

    private static void ConfigureClient(IServiceProvider provider, HttpClient client, bool webSdk)
    {
        var options = provider.GetRequiredService<IOptions<GenetecOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(20);
        if (!webSdk)
        {
            return;
        }

        if (Uri.TryCreate(AppendSlash(options.WebSdk.BaseUrl), UriKind.Absolute, out var baseAddress))
        {
            client.BaseAddress = baseAddress;
        }
    }

    private static HttpMessageHandler CreateHandler(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<GenetecOptions>>().Value;
        var handler = new HttpClientHandler();
        if (options.AllowInvalidCertificates)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return handler;
    }

    private static string AppendSlash(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.EndsWith('/') ? value : value + "/";
}
