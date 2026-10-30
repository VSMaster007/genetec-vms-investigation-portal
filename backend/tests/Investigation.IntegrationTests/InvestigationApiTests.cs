using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Investigation.IntegrationTests;

public class InvestigationApiTests : IClassFixture<PortalFactory>
{
    private readonly PortalFactory _factory;

    public InvestigationApiTests(PortalFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task External_incident_is_retrieved_from_the_seed_source()
    {
        var client = await ClientAsync();
        var page = await client.GetFromJsonAsync<JsonElement>("/api/incidents?incidentId=INC-2026-001");
        var item = page.GetProperty("items").EnumerateArray().Single();
        Assert.Equal("Unauthorized entrance", item.GetProperty("title").GetString());
        Assert.Equal("CAM-ENTRANCE-01", item.GetProperty("externalCameraId").GetString());
        Assert.Equal("Mapped", item.GetProperty("mappingStatus").GetString());
    }

    [Fact]
    public async Task Camera_mapping_is_created_for_the_mock_catalog()
    {
        var client = await ClientAsync();
        var mappings = await client.GetFromJsonAsync<JsonElement>("/api/cameras/mappings");
        Assert.Contains(mappings.GetProperty("mappings").EnumerateArray(), mapping => mapping.GetProperty("externalCameraId").GetString() == "CAM-ENTRANCE-01");
    }

    [Fact]
    public async Task Missing_camera_mapping_rejects_playback()
    {
        var client = await ClientAsync();
        var incidentId = await IncidentIdAsync(client, "INC-2026-007");
        var response = await client.PostAsJsonAsync("/api/playback/resolve", new { incidentId });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MissingCameraMapping", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Playback_session_initializes_without_claiming_a_genetec_recording()
    {
        var client = await ClientAsync();
        var incidentId = await IncidentIdAsync(client, "INC-2026-001");
        var response = await client.PostAsJsonAsync("/api/playback/session", new { incidentId, preEventSeconds = 60, postEventSeconds = 60 });
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Mock", session.GetProperty("mode").GetString());
        Assert.Equal("None", session.GetProperty("provider").GetString());
        Assert.False(session.GetProperty("isGenetecRecording").GetBoolean());
        Assert.True(session.GetProperty("canPlay").GetBoolean());
        Assert.Equal("Available", session.GetProperty("availability").GetProperty("state").GetString());
        Assert.True(session.GetProperty("availability").GetProperty("isMockArchive").GetBoolean());
        Assert.Equal(JsonValueKind.Null, session.GetProperty("mediaGatewayEndpoint").ValueKind);
    }

    [Fact]
    public async Task Archived_recording_unavailable_is_explicit()
    {
        var client = await ClientAsync();
        var incidentId = await IncidentIdAsync(client, "INC-2026-004");
        var session = await (await client.PostAsJsonAsync("/api/playback/session", new { incidentId })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(session.GetProperty("canPlay").GetBoolean());
        Assert.Equal("RetentionExpired", session.GetProperty("availability").GetProperty("state").GetString());
    }

    [Fact]
    public async Task Recording_gap_is_explicit()
    {
        var client = await ClientAsync();
        var incidentId = await IncidentIdAsync(client, "INC-2026-006");
        var session = await (await client.PostAsJsonAsync("/api/playback/session", new { incidentId })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Gap", session.GetProperty("availability").GetProperty("state").GetString());
        Assert.False(session.GetProperty("canPlay").GetBoolean());
    }

    [Fact]
    public async Task Playback_authorization_is_rejected_for_the_restricted_camera()
    {
        var client = await ClientAsync();
        var incidentId = await IncidentIdAsync(client, "INC-2026-005");
        var response = await client.PostAsJsonAsync("/api/playback/session", new { incidentId });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_playback_is_rejected()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/playback/session", new { incidentId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task System_status_does_not_call_mock_mode_a_live_directory()
    {
        var client = await ClientAsync();
        var status = await client.GetFromJsonAsync<JsonElement>("/api/system/status");
        Assert.Equal("Healthy", status.GetProperty("api").GetProperty("state").GetString());
        Assert.Equal("Healthy", status.GetProperty("database").GetProperty("state").GetString());
        Assert.Equal("Mock", status.GetProperty("genetecDirectory").GetProperty("state").GetString());
        Assert.NotEqual("Healthy", status.GetProperty("mediaGateway").GetProperty("state").GetString());
    }

    [Fact]
    public async Task Selecting_an_incident_writes_an_audit_record()
    {
        var client = await ClientAsync();
        var incidentId = await IncidentIdAsync(client, "INC-2026-002");
        (await client.GetAsync($"/api/incidents/{incidentId}")).EnsureSuccessStatusCode();
        var audit = await client.GetFromJsonAsync<JsonElement>("/api/audit?take=20");
        Assert.Contains(audit.EnumerateArray(), entry => entry.GetProperty("action").GetString() == "IncidentSelected");
    }

    private async Task<HttpClient> ClientAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "investigator@investigation.local",
            password = "Test-Investigator-12345"
        });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return client;
    }

    private static async Task<Guid> IncidentIdAsync(HttpClient client, string externalId)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/incidents/search?incidentId={externalId}");
        return page.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid();
    }
}

public sealed class PortalFactory : WebApplicationFactory<Program>
{
    public PortalFactory()
    {
        var seed = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "database", "seed", "incidents.json"));
        Environment.SetEnvironmentVariable("Database__Provider", "InMemory");
        Environment.SetEnvironmentVariable("Database__Name", "portal-tests");
        Environment.SetEnvironmentVariable("Database__MigrateOnStartup", "false");
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-test-signing-key-32chars");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "investigation-portal");
        Environment.SetEnvironmentVariable("Jwt__Audience", "investigation-portal");
        Environment.SetEnvironmentVariable("Seed__AllowDevPasswords", "true");
        Environment.SetEnvironmentVariable("Seed__AdminPassword", "Test-Admin-12345");
        Environment.SetEnvironmentVariable("Seed__InvestigatorPassword", "Test-Investigator-12345");
        Environment.SetEnvironmentVariable("Seed__CreateSampleMappings", "true");
        Environment.SetEnvironmentVariable("Genetec__Mode", "Mock");
        Environment.SetEnvironmentVariable("IncidentSource__Mode", "Mock");
        Environment.SetEnvironmentVariable("IncidentSource__SeedPath", seed);
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
    }
}
