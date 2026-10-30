using System.Net.Http.Headers;
using System.Text.Json;
using Investigation.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Investigation.Infrastructure.Incidents;

public sealed class MockIncidentSource : IIncidentSource
{
    private readonly IncidentSourceOptions _options;
    private readonly ILogger<MockIncidentSource> _logger;

    public MockIncidentSource(IOptions<IncidentSourceOptions> options, ILogger<MockIncidentSource> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string Mode => "Mock";

    public async Task<IReadOnlyList<ExternalIncidentDto>> GetIncidentsAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var incidents = await ReadAsync(cancellationToken);
        return incidents.Where(incident => InRange(incident, fromUtc, toUtc)).ToArray();
    }

    public async Task<ComponentCheck> CheckHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var incidents = await ReadAsync(cancellationToken);
            return new ComponentCheck("Mock", $"The seed file contains {incidents.Count} incident records. No external API was contacted. Records with invalid timestamps are rejected during sync.", true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Mock incident seed could not be read.");
            return new ComponentCheck("Unhealthy", exception.Message, true);
        }
    }

    private async Task<IReadOnlyList<ExternalIncidentDto>> ReadAsync(CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(_options.SeedPath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Incident seed file was not found at {path}.", path);
        }

        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("incidents", out var nested))
        {
            root = nested;
        }

        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The incident seed must be a JSON array.");
        }

        var incidents = new List<ExternalIncidentDto>();
        foreach (var item in root.EnumerateArray())
        {
            incidents.Add(new ExternalIncidentDto(
                ReadString(item, "incidentId"),
                ReadString(item, "title"),
                ReadString(item, "description"),
                ReadString(item, "cameraId"),
                ReadString(item, "timestamp"),
                ReadString(item, "eventType"),
                ReadString(item, "location")));
        }

        return incidents;
    }

    private static bool InRange(ExternalIncidentDto incident, DateTime? fromUtc, DateTime? toUtc)
    {
        if (fromUtc is null && toUtc is null)
        {
            return true;
        }

        var timestamp = TimestampNormalizer.ParseUtc(incident.Timestamp);
        return (fromUtc is null || timestamp >= fromUtc) && (toUtc is null || timestamp <= toUtc);
    }

    private static string ReadString(JsonElement item, string name) =>
        item.TryGetProperty(name, out var property) ? property.GetString() ?? string.Empty : string.Empty;
}

public sealed class RestIncidentSource : IIncidentSource
{
    private readonly HttpClient _http;
    private readonly IncidentSourceOptions _options;
    private readonly ILogger<RestIncidentSource> _logger;

    public RestIncidentSource(HttpClient http, IOptions<IncidentSourceOptions> options, ILogger<RestIncidentSource> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public string Mode => "Real";

    public async Task<IReadOnlyList<ExternalIncidentDto>> GetIncidentsAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var from = fromUtc.HasValue ? TimestampNormalizer.FormatUtc(fromUtc.Value) : string.Empty;
        var to = toUtc.HasValue ? TimestampNormalizer.FormatUtc(toUtc.Value) : string.Empty;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"incidents?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");
        AddKey(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Incident API returned {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("incidents", out var nested))
        {
            root = nested;
        }

        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The incident API did not return a JSON array.");
        }

        return root.EnumerateArray().Select(item => new ExternalIncidentDto(
            Get(item, "incidentId"),
            Get(item, "title"),
            Get(item, "description"),
            Get(item, "cameraId"),
            Get(item, "timestamp"),
            Get(item, "eventType"),
            Get(item, "location"))).ToArray();
    }

    public async Task<ComponentCheck> CheckHealthAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return new ComponentCheck("NotConfigured", "IncidentSource:BaseUrl is empty.", false);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _options.HealthPath.TrimStart('/'));
            AddKey(request);
            using var response = await _http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? new ComponentCheck("Healthy", $"External incident API responded {(int)response.StatusCode}.", true)
                : new ComponentCheck("Unhealthy", $"External incident API responded {(int)response.StatusCode}.", true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "External incident API health check failed.");
            return new ComponentCheck("Unhealthy", exception.Message, true);
        }
    }

    private void AddKey(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    private static string Get(JsonElement item, string name) =>
        item.TryGetProperty(name, out var property) ? property.GetString() ?? string.Empty : string.Empty;
}
