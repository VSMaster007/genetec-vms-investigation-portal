using Investigation.Domain;
using Microsoft.Extensions.Logging;

namespace Investigation.Application;

public sealed class AuthService
{
    private readonly IInvestigationStore _store;
    private readonly IPasswordHasher _passwords;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IInvestigationStore store, IPasswordHasher passwords, ILogger<AuthService> logger)
    {
        _store = store;
        _passwords = passwords;
        _logger = logger;
    }

    public async Task<OperationResult<AuthenticatedUser>> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalized = email.Trim();
        var user = await _store.FindUserByEmailAsync(normalized, cancellationToken);
        if (user is null || !_passwords.Verify(user, password))
        {
            _logger.LogWarning("Sign-in failed for {Email}.", normalized);
            return OperationResult<AuthenticatedUser>.Fail("InvalidCredentials", "Email or password is incorrect.", 401);
        }

        await _store.AddAuditAsync(
            new AuditEntry(user.Id, null, AuditActions.UserLogin, $"User {user.Email} signed in."),
            cancellationToken);

        return OperationResult<AuthenticatedUser>.Ok(new AuthenticatedUser(user.Id, user.Email, user.DisplayName, user.Role.ToString()));
    }
}

public sealed class AuditQueryService
{
    private readonly IInvestigationStore _store;

    public AuditQueryService(IInvestigationStore store)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<AuditDto>> ListAsync(Guid? restrictToUserId, int take, CancellationToken cancellationToken)
    {
        var records = await _store.ListAuditAsync(restrictToUserId, Math.Clamp(take, 1, 200), cancellationToken);
        return records.Select(ToDto).ToArray();
    }

    public static AuditDto ToDto(InvestigationAudit audit) => new(
        audit.Id,
        audit.UserId,
        audit.User?.Email,
        audit.IncidentId,
        audit.Incident?.ExternalIncidentId,
        audit.Action,
        audit.Details,
        audit.CreatedAt);
}

public sealed class DashboardService
{
    private readonly IInvestigationStore _store;
    private readonly IGenetecService _genetec;
    private readonly IncidentService _incidents;
    private readonly TimeProvider _time;

    public DashboardService(IInvestigationStore store, IGenetecService genetec, IncidentService incidents, TimeProvider time)
    {
        _store = store;
        _genetec = genetec;
        _incidents = incidents;
        _time = time;
    }

    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var startOfDay = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var total = await _store.CountIncidentsAsync(null, null, cancellationToken);
        var today = await _store.CountIncidentsAsync(startOfDay, startOfDay.AddDays(1), cancellationToken);
        var cameras = await _store.ListCamerasAsync(null, includeMock: true, cancellationToken);
        var mappings = await _store.ListMappingsAsync(cancellationToken);
        var status = await _genetec.GetConnectionStatusAsync(cancellationToken);
        var catalogMode = status.Mode;
        var relevant = cameras.Where(camera => catalogMode == "Mock" ? camera.IsMock : !camera.IsMock).ToArray();
        var recentPage = await _store.SearchIncidentsAsync(new IncidentSearchCriteria(null, null, null, null, null, null, 1, 6), cancellationToken);
        var recent = new List<IncidentListItemDto>();
        foreach (var incident in recentPage.Items)
        {
            recent.Add(await _incidents.ToListItemAsync(incident, includeRecording: false, cancellationToken));
        }

        var audits = await _store.ListAuditAsync(null, 8, cancellationToken);
        return new DashboardDto(
            total,
            today,
            relevant.Length,
            relevant.Count(camera => camera.Status == CameraStatus.Online),
            mappings.Count,
            catalogMode,
            status,
            recent,
            audits.Select(AuditQueryService.ToDto).ToArray());
    }
}

public sealed class SystemStatusService
{
    private readonly IInvestigationStore _store;
    private readonly IGenetecService _genetec;
    private readonly IncidentService _incidents;
    private readonly TimeProvider _time;

    public SystemStatusService(
        IInvestigationStore store,
        IGenetecService genetec,
        IncidentService incidents,
        TimeProvider time)
    {
        _store = store;
        _genetec = genetec;
        _incidents = incidents;
        _time = time;
    }

    public async Task<SystemStatusDto> GetAsync(CancellationToken cancellationToken)
    {
        var checkedAt = TimestampNormalizer.FormatUtc(_time.GetUtcNow().UtcDateTime);
        var databaseOk = false;
        string databaseDetail;
        try
        {
            databaseOk = await _store.CanConnectAsync(cancellationToken);
            databaseDetail = databaseOk
                ? $"PostgreSQL connection verified at {checkedAt}."
                : "The database did not accept a connection.";
        }
        catch (Exception exception)
        {
            databaseDetail = $"Database check failed: {exception.Message}";
        }

        var genetec = await _genetec.GetConnectionStatusAsync(cancellationToken);
        var external = await _incidents.CheckSourceHealthAsync(cancellationToken);

        return new SystemStatusDto(
            new ComponentCheck("Healthy", "The investigation API responded.", true),
            new ComponentCheck(databaseOk ? "Healthy" : "Unhealthy", databaseDetail, true),
            DescribeDirectory(genetec),
            DescribeAuthentication(genetec),
            DescribeMediaGateway(genetec),
            external,
            genetec.Mode,
            genetec.Transport,
            genetec.SdkAssembliesPresent);
    }

    private static ComponentCheck DescribeDirectory(GenetecConnectionStatus status)
    {
        if (!status.Attempted)
        {
            return new ComponentCheck("NotConfigured", "The Directory has not been contacted yet.", false);
        }

        if (status.Mode == "Mock")
        {
            return new ComponentCheck("Mock", status.DirectoryDetail ?? "Mock adapter is active.", true);
        }

        return status.DirectoryConnected
            ? new ComponentCheck("Healthy", status.DirectoryDetail ?? "Directory connection verified.", true)
            : new ComponentCheck("Unhealthy", status.Error ?? status.DirectoryDetail ?? "Directory connection failed.", true);
    }

    private static ComponentCheck DescribeAuthentication(GenetecConnectionStatus status)
    {
        if (status.Mode == "Mock")
        {
            return new ComponentCheck("Mock", "SDK authentication is not performed in mock mode.", true);
        }

        if (!status.Attempted)
        {
            return new ComponentCheck("NotConfigured", "Authentication has not been attempted.", false);
        }

        return status.Authenticated
            ? new ComponentCheck("Healthy", $"SDK authentication succeeded. Certificate status: {status.CertificateStatus}.", true)
            : new ComponentCheck("Unhealthy", status.Error ?? $"SDK authentication failed. Certificate status: {status.CertificateStatus}.", true);
    }

    private static ComponentCheck DescribeMediaGateway(GenetecConnectionStatus status)
    {
        if (status.Mode == "Mock")
        {
            return new ComponentCheck("Mock", status.MediaGatewayDetail ?? "Mock mode does not connect to a Media Gateway.", status.MediaGatewayAttempted);
        }

        if (!status.MediaGatewayAttempted)
        {
            return new ComponentCheck("NotConfigured", status.MediaGatewayDetail ?? "Media Gateway has not been checked.", false);
        }

        return status.MediaGatewayReachable
            ? new ComponentCheck("Healthy", status.MediaGatewayDetail ?? "Media Gateway responded.", true)
            : new ComponentCheck("Unhealthy", status.MediaGatewayDetail ?? "Media Gateway did not respond.", true);
    }
}
