using Investigation.Application;
using Investigation.Domain;
using Investigation.Genetec;
using Investigation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Investigation.Infrastructure;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string Provider { get; set; } = "Postgres";
    public string Name { get; set; } = "investigation";
    public bool MigrateOnStartup { get; set; } = true;
}

public sealed class PortalStartupService : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PortalStartupService> _logger;

    public PortalStartupService(IServiceScopeFactory scopes, ILogger<PortalStartupService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (database.Provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
        else if (database.MigrateOnStartup)
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        await SeedUsersAsync(scope.ServiceProvider, cancellationToken);
        try
        {
            var sync = await scope.ServiceProvider.GetRequiredService<IncidentService>().SyncAsync(null, null, cancellationToken);
            _logger.LogInformation("Incident sync imported {Imported} and rejected {Rejected}.", sync.Imported, sync.Rejected);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Incident sync failed during startup.");
        }

        try
        {
            await scope.ServiceProvider.GetRequiredService<CameraCatalogService>().RefreshAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Camera catalog refresh failed during startup.");
        }

        await SeedMappingsAsync(scope.ServiceProvider, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedUsersAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<IInvestigationStore>();
        if (await store.CountUsersAsync(cancellationToken) > 0)
        {
            return;
        }

        var options = services.GetRequiredService<IOptions<SeedOptions>>().Value;
        var hasher = services.GetRequiredService<IPasswordHasher>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        await AddUserAsync(store, hasher, options.AdminEmail, options.AdminDisplayName, options.AdminPassword, UserRole.Administrator, options, environment, cancellationToken);
        await AddUserAsync(store, hasher, options.InvestigatorEmail, options.InvestigatorDisplayName, options.InvestigatorPassword, UserRole.Investigator, options, environment, cancellationToken);
    }

    private async Task AddUserAsync(
        IInvestigationStore store,
        IPasswordHasher hasher,
        string email,
        string displayName,
        string password,
        UserRole role,
        SeedOptions options,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Skipped seeding {Email} because no password is configured.", email);
            return;
        }

        if (!environment.IsDevelopment() && !options.AllowDevPasswords)
        {
            _logger.LogWarning("Skipped seeding {Email} outside Development.", email);
            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = hasher.Hash(user, password);
        await store.AddUserAsync(user, cancellationToken);
    }

    private async Task SeedMappingsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var options = services.GetRequiredService<IOptions<SeedOptions>>().Value;
        var mode = services.GetRequiredService<IOptions<GenetecModeOptions>>().Value;
        if (!options.CreateSampleMappings || !mode.Mode.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var store = services.GetRequiredService<IInvestigationStore>();
        var existing = await store.ListMappingsAsync(cancellationToken);
        if (existing.Count > 0)
        {
            return;
        }

        foreach (var camera in MockCameraCatalog.Cameras)
        {
            var stored = await store.FindCameraByGenetecGuidAsync(camera.Guid, cancellationToken);
            if (stored is null)
            {
                continue;
            }

            await store.AddMappingAsync(camera.ExternalId, stored.Id, cancellationToken);
        }
    }
}
