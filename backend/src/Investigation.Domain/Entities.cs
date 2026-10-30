namespace Investigation.Domain;

public enum UserRole
{
    Investigator = 0,
    Administrator = 1
}

public enum CameraStatus
{
    Unknown = 0,
    Online = 1,
    Offline = 2,
    AccessDenied = 3
}

public static class AuditActions
{
    public const string UserLogin = "UserLogin";
    public const string IncidentSelected = "IncidentSelected";
    public const string CameraSelected = "CameraSelected";
    public const string PlaybackInitialized = "PlaybackInitialized";
    public const string PlaybackAuthorizationFailed = "PlaybackAuthorizationFailed";
    public const string RecordingUnavailable = "RecordingUnavailable";
    public const string IntegrationError = "IntegrationError";
}

public sealed class ApplicationUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class Incident
{
    public Guid Id { get; set; }
    public string ExternalIncidentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExternalCameraId { get; set; } = string.Empty;
    public DateTime EventTimestampUtc { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class Camera
{
    public Guid Id { get; set; }
    public Guid GenetecCameraGuid { get; set; }
    public string CameraName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public CameraStatus Status { get; set; }
    public bool IsMock { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<CameraMapping> Mappings { get; set; } = new List<CameraMapping>();
}

public sealed class CameraMapping
{
    public Guid Id { get; set; }
    public string ExternalCameraId { get; set; } = string.Empty;
    public Guid GenetecCameraId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Camera? Camera { get; set; }
}

public sealed class InvestigationAudit
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? IncidentId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ApplicationUser? User { get; set; }
    public Incident? Incident { get; set; }
}

public sealed class TimestampNormalizationException : Exception
{
    public TimestampNormalizationException(string message) : base(message)
    {
    }
}
