using Investigation.Application;
using Investigation.Domain;
using Microsoft.EntityFrameworkCore;

namespace Investigation.Infrastructure.Persistence;

public sealed class InvestigationStore : IInvestigationStore
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public InvestigationStore(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        _db.Database.CanConnectAsync(cancellationToken);

    public Task<int> CountUsersAsync(CancellationToken cancellationToken) =>
        _db.Users.CountAsync(cancellationToken);

    public Task<ApplicationUser?> FindUserByEmailAsync(string email, CancellationToken cancellationToken) =>
        _db.Users.FirstOrDefaultAsync(user => user.Email == email.ToLowerInvariant(), cancellationToken);

    public async Task AddUserAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.Email = user.Email.ToLowerInvariant();
        user.CreatedAt = Utc(user.CreatedAt);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<Incident?> FindIncidentAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Incidents.AsNoTracking().FirstOrDefaultAsync(incident => incident.Id == id, cancellationToken);

    public async Task<PagedResult<Incident>> SearchIncidentsAsync(IncidentSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var query = _db.Incidents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(criteria.IncidentId))
        {
            var term = criteria.IncidentId.Trim().ToLower();
            query = query.Where(incident => incident.ExternalIncidentId.ToLower().Contains(term) || incident.Title.ToLower().Contains(term));
        }

        if (criteria.FromUtc is not null)
        {
            query = query.Where(incident => incident.EventTimestampUtc >= criteria.FromUtc);
        }

        if (criteria.ToUtc is not null)
        {
            query = query.Where(incident => incident.EventTimestampUtc <= criteria.ToUtc);
        }

        if (!string.IsNullOrWhiteSpace(criteria.EventType))
        {
            var eventType = criteria.EventType.Trim().ToLower();
            query = query.Where(incident => incident.EventType.ToLower() == eventType);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Location))
        {
            var location = criteria.Location.Trim().ToLower();
            query = query.Where(incident => incident.Location.ToLower().Contains(location));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Camera))
        {
            var camera = criteria.Camera.Trim().ToLower();
            var externalIds = await _db.Cameras.AsNoTracking()
                .Where(item => item.CameraName.ToLower().Contains(camera))
                .SelectMany(item => item.Mappings.Select(mapping => mapping.ExternalCameraId))
                .ToListAsync(cancellationToken);
            query = query.Where(incident => incident.ExternalCameraId.ToLower().Contains(camera) || externalIds.Contains(incident.ExternalCameraId));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(incident => incident.EventTimestampUtc)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<Incident>(items, criteria.Page, criteria.PageSize, total);
    }

    public async Task<int> UpsertIncidentsAsync(IReadOnlyList<NormalizedIncident> incidents, CancellationToken cancellationToken)
    {
        if (incidents.Count == 0)
        {
            return 0;
        }

        var ids = incidents.Select(incident => incident.ExternalIncidentId).ToArray();
        var existing = await _db.Incidents.Where(incident => ids.Contains(incident.ExternalIncidentId)).ToListAsync(cancellationToken);
        var byId = existing.ToDictionary(incident => incident.ExternalIncidentId, StringComparer.OrdinalIgnoreCase);
        var now = _time.GetUtcNow().UtcDateTime;
        foreach (var incoming in incidents)
        {
            if (!byId.TryGetValue(incoming.ExternalIncidentId, out var current))
            {
                _db.Incidents.Add(new Incident
                {
                    Id = Guid.NewGuid(),
                    ExternalIncidentId = incoming.ExternalIncidentId,
                    Title = incoming.Title,
                    Description = incoming.Description,
                    ExternalCameraId = incoming.ExternalCameraId,
                    EventTimestampUtc = Utc(incoming.EventTimestampUtc),
                    EventType = incoming.EventType,
                    Location = incoming.Location,
                    CreatedAt = now
                });
                continue;
            }

            current.Title = incoming.Title;
            current.Description = incoming.Description;
            current.ExternalCameraId = incoming.ExternalCameraId;
            current.EventTimestampUtc = Utc(incoming.EventTimestampUtc);
            current.EventType = incoming.EventType;
            current.Location = incoming.Location;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return incidents.Count;
    }

    public Task<int> CountIncidentsAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var query = _db.Incidents.AsNoTracking().AsQueryable();
        if (fromUtc is not null)
        {
            query = query.Where(incident => incident.EventTimestampUtc >= fromUtc);
        }

        if (toUtc is not null)
        {
            query = query.Where(incident => incident.EventTimestampUtc < toUtc);
        }

        return query.CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListUnmappedExternalIdsAsync(CancellationToken cancellationToken)
    {
        var mapped = _db.CameraMappings.Select(mapping => mapping.ExternalCameraId);
        return await _db.Incidents.AsNoTracking()
            .Select(incident => incident.ExternalCameraId)
            .Distinct()
            .Where(externalId => !mapped.Contains(externalId))
            .OrderBy(externalId => externalId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListEventTypesAsync(CancellationToken cancellationToken) =>
        await _db.Incidents.AsNoTracking().Select(incident => incident.EventType).Distinct().OrderBy(value => value).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ListLocationsAsync(CancellationToken cancellationToken) =>
        await _db.Incidents.AsNoTracking().Select(incident => incident.Location).Distinct().OrderBy(value => value).ToListAsync(cancellationToken);

    public Task<Camera?> FindCameraAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Cameras.AsNoTracking().FirstOrDefaultAsync(camera => camera.Id == id, cancellationToken);

    public Task<Camera?> FindCameraByGenetecGuidAsync(Guid genetecGuid, CancellationToken cancellationToken) =>
        _db.Cameras.AsNoTracking().FirstOrDefaultAsync(camera => camera.GenetecCameraGuid == genetecGuid, cancellationToken);

    public async Task<IReadOnlyList<Camera>> ListCamerasAsync(string? search, bool includeMock, CancellationToken cancellationToken)
    {
        var query = _db.Cameras.AsNoTracking().AsQueryable();
        if (!includeMock)
        {
            query = query.Where(camera => !camera.IsMock);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(camera =>
                camera.CameraName.ToLower().Contains(term) ||
                camera.Location.ToLower().Contains(term) ||
                camera.GenetecCameraGuid.ToString().ToLower().Contains(term));
        }

        return await query.OrderBy(camera => camera.CameraName).ToListAsync(cancellationToken);
    }

    public async Task UpsertDiscoveredCamerasAsync(IReadOnlyList<DiscoveredCamera> cameras, CancellationToken cancellationToken)
    {
        var guids = cameras.Select(camera => camera.GenetecGuid).ToArray();
        var existing = await _db.Cameras.Where(camera => guids.Contains(camera.GenetecCameraGuid)).ToListAsync(cancellationToken);
        var byGuid = existing.ToDictionary(camera => camera.GenetecCameraGuid);
        var now = _time.GetUtcNow().UtcDateTime;
        foreach (var discovered in cameras)
        {
            var status = !discovered.AccessGranted
                ? CameraStatus.AccessDenied
                : discovered.IsOnline switch
                {
                    true => CameraStatus.Online,
                    false => CameraStatus.Offline,
                    _ => CameraStatus.Unknown
                };

            if (!byGuid.TryGetValue(discovered.GenetecGuid, out var current))
            {
                _db.Cameras.Add(new Camera
                {
                    Id = Guid.NewGuid(),
                    GenetecCameraGuid = discovered.GenetecGuid,
                    CameraName = discovered.Name,
                    Location = discovered.Description ?? string.Empty,
                    Status = status,
                    IsMock = discovered.IsMock,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                continue;
            }

            current.CameraName = discovered.Name;
            if (!string.IsNullOrWhiteSpace(discovered.Description))
            {
                current.Location = discovered.Description;
            }

            current.Status = status;
            current.IsMock = discovered.IsMock;
            current.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CameraMapping>> ListMappingsAsync(CancellationToken cancellationToken) =>
        await _db.CameraMappings.AsNoTracking().Include(mapping => mapping.Camera).OrderBy(mapping => mapping.ExternalCameraId).ToListAsync(cancellationToken);

    public Task<CameraMapping?> FindMappingAsync(Guid id, CancellationToken cancellationToken) =>
        _db.CameraMappings.AsNoTracking().Include(mapping => mapping.Camera).FirstOrDefaultAsync(mapping => mapping.Id == id, cancellationToken);

    public Task<CameraMapping?> FindMappingByExternalIdAsync(string externalCameraId, CancellationToken cancellationToken) =>
        _db.CameraMappings.AsNoTracking().Include(mapping => mapping.Camera).FirstOrDefaultAsync(mapping => mapping.ExternalCameraId == externalCameraId, cancellationToken);

    public async Task<CameraMapping> AddMappingAsync(string externalCameraId, Guid cameraId, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var mapping = new CameraMapping
        {
            Id = Guid.NewGuid(),
            ExternalCameraId = externalCameraId,
            GenetecCameraId = cameraId,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.CameraMappings.Add(mapping);
        await _db.SaveChangesAsync(cancellationToken);
        return mapping;
    }

    public async Task<CameraMapping?> UpdateMappingAsync(Guid id, string externalCameraId, Guid cameraId, CancellationToken cancellationToken)
    {
        var mapping = await _db.CameraMappings.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (mapping is null)
        {
            return null;
        }

        mapping.ExternalCameraId = externalCameraId;
        mapping.GenetecCameraId = cameraId;
        mapping.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
        return mapping;
    }

    public async Task<bool> DeleteMappingAsync(Guid id, CancellationToken cancellationToken)
    {
        var mapping = await _db.CameraMappings.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (mapping is null)
        {
            return false;
        }

        _db.CameraMappings.Remove(mapping);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task AddAuditAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        _db.Audits.Add(new InvestigationAudit
        {
            Id = Guid.NewGuid(),
            UserId = entry.UserId,
            IncidentId = entry.IncidentId,
            Action = entry.Action,
            Details = entry.Details.Length > 1000 ? entry.Details[..1000] : entry.Details,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasRecentAuditAsync(Guid? userId, Guid? incidentId, string action, TimeSpan window, CancellationToken cancellationToken)
    {
        var since = _time.GetUtcNow().UtcDateTime - window;
        return _db.Audits.AnyAsync(
            audit => audit.UserId == userId && audit.IncidentId == incidentId && audit.Action == action && audit.CreatedAt >= since,
            cancellationToken);
    }

    public async Task<IReadOnlyList<InvestigationAudit>> ListAuditAsync(Guid? userId, int take, CancellationToken cancellationToken)
    {
        var query = _db.Audits.AsNoTracking().Include(audit => audit.User).Include(audit => audit.Incident).AsQueryable();
        if (userId is not null)
        {
            query = query.Where(audit => audit.UserId == userId);
        }

        return await query.OrderByDescending(audit => audit.CreatedAt).Take(take).ToListAsync(cancellationToken);
    }

    public Task<int> CountRecentAuditAsync(Guid? userId, DateTime sinceUtc, CancellationToken cancellationToken)
    {
        var query = _db.Audits.AsNoTracking().Where(audit => audit.CreatedAt >= sinceUtc);
        if (userId is not null)
        {
            query = query.Where(audit => audit.UserId == userId);
        }

        return query.CountAsync(cancellationToken);
    }

    private static DateTime Utc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc);
}
