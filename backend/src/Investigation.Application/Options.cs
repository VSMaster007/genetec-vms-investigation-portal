namespace Investigation.Application;

public sealed class PortalOptions
{
    public const string SectionName = "Portal";
    public int MaxPageSize { get; set; } = 100;
}

public sealed class PlaybackOptions
{
    public const string SectionName = "Playback";
    public int DefaultPreEventSeconds { get; set; } = 60;
    public int DefaultPostEventSeconds { get; set; } = 60;
    public int MaxWindowSeconds { get; set; } = 3600;
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "investigation-portal";
    public string Audience { get; set; } = "investigation-portal";
    public string Key { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 480;
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";
    public bool AllowDevPasswords { get; set; }
    public string AdminEmail { get; set; } = "admin@investigation.local";
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminDisplayName { get; set; } = "Portal Administrator";
    public string InvestigatorEmail { get; set; } = "investigator@investigation.local";
    public string InvestigatorPassword { get; set; } = string.Empty;
    public string InvestigatorDisplayName { get; set; } = "Investigator";
    public bool CreateSampleMappings { get; set; }
}

public sealed class IncidentSourceOptions
{
    public const string SectionName = "IncidentSource";
    public string Mode { get; set; } = "Mock";
    public string SeedPath { get; set; } = "seed/incidents.json";
    public string BaseUrl { get; set; } = string.Empty;
    public string HealthPath { get; set; } = "/health";
    public string ApiKey { get; set; } = string.Empty;
}

public static class ExternalCameraIdRules
{
    public static bool IsValid(string value) =>
        value.Length is >= 2 and <= 64 && value.All(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-');
}

public static class PageRules
{
    public static (int Page, int PageSize) Normalize(int page, int pageSize, int maxPageSize)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedSize = pageSize < 1 ? 25 : Math.Min(pageSize, maxPageSize);
        return (normalizedPage, normalizedSize);
    }
}
