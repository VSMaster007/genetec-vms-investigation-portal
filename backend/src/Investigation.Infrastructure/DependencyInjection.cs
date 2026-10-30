using Investigation.Application;
using Investigation.Domain;
using Investigation.Infrastructure.Incidents;
using Investigation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Investigation.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PortalOptions>(configuration.GetSection(PortalOptions.SectionName));
        services.Configure<PlaybackOptions>(configuration.GetSection(PlaybackOptions.SectionName));
        services.Configure<DemoMediaOptions>(configuration.GetSection(DemoMediaOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.Configure<IncidentSourceOptions>(configuration.GetSection(IncidentSourceOptions.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);

        var database = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        services.AddDbContext<AppDbContext>(options =>
        {
            if (database.Provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                options.UseInMemoryDatabase(string.IsNullOrWhiteSpace(database.Name) ? "investigation" : database.Name);
            }
            else
            {
                options.UseNpgsql(configuration.GetConnectionString("Default"));
            }
        });

        services.AddScoped<IInvestigationStore, InvestigationStore>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        if ((configuration["IncidentSource:Mode"] ?? "Mock").Equals("Real", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IIncidentSource, RestIncidentSource>((provider, client) =>
            {
                var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<IncidentSourceOptions>>().Value;
                if (Uri.TryCreate(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseAddress))
                {
                    client.BaseAddress = baseAddress;
                }

                client.Timeout = TimeSpan.FromSeconds(20);
            });
        }
        else
        {
            services.AddSingleton<IIncidentSource, MockIncidentSource>();
        }

        services.AddScoped<IncidentService>();
        services.AddScoped<CameraCatalogService>();
        services.AddScoped<PlaybackService>();
        services.AddScoped<AuthService>();
        services.AddScoped<AuditQueryService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<SystemStatusService>();
        services.AddHostedService<PortalStartupService>();
        return services;
    }
}

public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<ApplicationUser> _hasher = new();

    public string Hash(ApplicationUser user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(ApplicationUser user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
