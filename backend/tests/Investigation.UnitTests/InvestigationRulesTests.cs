using System.Net;
using System.Text;
using Investigation.Application;
using Investigation.Domain;
using Investigation.Genetec;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Investigation.UnitTests;

public class TimestampAndRecordingTests
{
    [Fact]
    public void Timestamp_with_offset_converts_to_utc()
    {
        var utc = TimestampNormalizer.ParseUtc("2026-10-15T16:35:20+02:00");
        Assert.Equal(new DateTime(2026, 10, 15, 14, 35, 20, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void Timestamp_without_offset_is_rejected()
    {
        var error = Assert.Throws<TimestampNormalizationException>(() => TimestampNormalizer.ParseUtc("2026-10-15T14:35:20"));
        Assert.Contains("offset", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Playback_window_uses_sixty_seconds_by_default()
    {
        var window = PlaybackWindowCalculator.Create(
            new DateTime(2026, 10, 15, 14, 35, 20, DateTimeKind.Utc),
            60,
            60,
            3600);

        Assert.Equal(new DateTime(2026, 10, 15, 14, 34, 20, DateTimeKind.Utc), window.StartUtc);
        Assert.Equal(new DateTime(2026, 10, 15, 14, 36, 20, DateTimeKind.Utc), window.EndUtc);
    }

    [Fact]
    public void Archived_recording_is_available_when_the_event_is_covered()
    {
        var start = new DateTime(2026, 10, 15, 14, 34, 20, DateTimeKind.Utc);
        var end = new DateTime(2026, 10, 15, 14, 36, 20, DateTimeKind.Utc);
        var assessment = RecordingWindowAnalyzer.Assess(
            start,
            new DateTime(2026, 10, 15, 14, 35, 20, DateTimeKind.Utc),
            end,
            new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromDays(30),
            true,
            true,
            true,
            null,
            [new TimeRange(start, end)]);

        Assert.Equal("Available", assessment.State);
        Assert.True(assessment.CanAttemptPlayback);
    }

    [Fact]
    public void Archived_recording_is_unavailable_when_no_sequence_overlaps()
    {
        var start = new DateTime(2026, 10, 15, 14, 34, 20, DateTimeKind.Utc);
        var end = new DateTime(2026, 10, 15, 14, 36, 20, DateTimeKind.Utc);
        var assessment = RecordingWindowAnalyzer.Assess(
            start,
            start.AddSeconds(60),
            end,
            new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromDays(30),
            true,
            true,
            true,
            null,
            Array.Empty<TimeRange>());

        Assert.Equal("Unavailable", assessment.State);
        Assert.False(assessment.CanAttemptPlayback);
    }

    [Fact]
    public void Recording_gap_is_reported_when_the_event_is_inside_a_hole()
    {
        var start = new DateTime(2026, 10, 30, 3, 4, 10, DateTimeKind.Utc);
        var eventTime = new DateTime(2026, 10, 30, 3, 5, 10, DateTimeKind.Utc);
        var end = new DateTime(2026, 10, 30, 3, 6, 10, DateTimeKind.Utc);
        var assessment = RecordingWindowAnalyzer.Assess(
            start,
            eventTime,
            end,
            new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromDays(30),
            true,
            true,
            true,
            null,
            [
                new TimeRange(start, new DateTime(2026, 10, 30, 3, 5, 0, DateTimeKind.Utc)),
                new TimeRange(new DateTime(2026, 10, 30, 3, 5, 30, DateTimeKind.Utc), end)
            ]);

        Assert.Equal("Gap", assessment.State);
        Assert.False(assessment.CoversEvent);
    }

    [Fact]
    public void Retention_expiration_blocks_playback()
    {
        var now = new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);
        var eventTime = new DateTime(2026, 10, 1, 14, 35, 20, DateTimeKind.Utc);
        var assessment = RecordingWindowAnalyzer.Assess(
            eventTime.AddSeconds(-60),
            eventTime,
            eventTime.AddSeconds(60),
            now,
            TimeSpan.FromDays(7),
            true,
            true,
            true,
            null,
            [new TimeRange(eventTime.AddHours(-1), eventTime.AddHours(1))]);

        Assert.Equal("RetentionExpired", assessment.State);
    }
}

public class MockGenetecServiceTests
{
    [Fact]
    public async Task Connection_probe_does_not_report_a_directory_connection()
    {
        var service = Create(fail: false);
        var status = await service.ConnectAsync(CancellationToken.None);
        Assert.Equal("Mock", status.Mode);
        Assert.True(status.Attempted);
        Assert.False(status.DirectoryConnected);
        Assert.False(status.Authenticated);
        Assert.False(status.MediaGatewayReachable);
    }

    [Fact]
    public async Task Configured_failure_is_reported_as_a_failure()
    {
        var service = Create(fail: true);
        var status = await service.GetConnectionStatusAsync(CancellationToken.None);
        Assert.False(status.DirectoryConnected);
        Assert.False(string.IsNullOrWhiteSpace(status.Error));
    }

    [Fact]
    public async Task Camera_discovery_returns_labeled_mock_cameras()
    {
        var service = Create(fail: false);
        var cameras = await service.GetCamerasAsync(CancellationToken.None);
        Assert.Equal(6, cameras.Count);
        Assert.All(cameras, camera => Assert.True(camera.IsMock));
        Assert.Contains(cameras, camera => camera.Name.Contains("Main Entrance", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Playback_token_is_not_issued_in_mock_mode()
    {
        var service = Create(fail: false);
        var token = await service.GetPlaybackTokenAsync(MockCameraCatalog.Cameras[0].Guid, CancellationToken.None);
        Assert.False(token.Succeeded);
        Assert.Null(token.Token);
    }

    private static MockGenetecService Create(bool fail)
    {
        var options = Options.Create(new GenetecOptions
        {
            Mode = "Mock",
            Mock = new MockOptions { FailConnection = fail }
        });
        return new MockGenetecService(options, TimeProvider.System, NullLogger<MockGenetecService>.Instance);
    }
}

public class WebSdkGenetecServiceTests
{
    [Fact]
    public async Task Directory_connection_succeeds_only_after_an_ok_response()
    {
        var service = Create(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("gwp.js", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return Json("{\"Rsp\":{\"Status\":\"Ok\"}}");
        });

        var status = await service.ConnectAsync(CancellationToken.None);
        Assert.True(status.DirectoryConnected);
        Assert.True(status.Authenticated);
        Assert.True(status.MediaGatewayReachable);
        Assert.Equal("WebSdk", status.Transport);
    }

    [Fact]
    public async Task Directory_connection_failure_is_not_reported_as_success()
    {
        var service = Create(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var status = await service.ConnectAsync(CancellationToken.None);
        Assert.False(status.DirectoryConnected);
        Assert.False(status.Authenticated);
        Assert.False(status.MediaGatewayReachable);
    }

    [Fact]
    public async Task Camera_discovery_reads_the_documented_entity_report()
    {
        var cameraId = Guid.Parse("4eaf92b8-2f3c-4d9b-a6d5-3a5b6eebf6a4");
        var service = Create(request =>
        {
            var path = request.RequestUri!.PathAndQuery;
            if (path.Contains("EntityConfiguration", StringComparison.Ordinal))
            {
                return Json($"{{\"Rsp\":{{\"Status\":\"Ok\",\"Result\":[{{\"Guid\":\"{cameraId}\",\"Name\":\"Lobby Camera\"}}]}}}}");
            }

            if (path.Contains("entity", StringComparison.Ordinal))
            {
                return Json("{\"Rsp\":{\"Status\":\"Ok\",\"Result\":{\"Name\":\"Lobby Camera\",\"Description\":\"Tower lobby\",\"IsOnline\":true}}}");
            }

            return Json("{\"Rsp\":{\"Status\":\"Ok\"}}");
        });

        var cameras = await service.GetCamerasAsync(CancellationToken.None);
        var camera = Assert.Single(cameras);
        Assert.Equal(cameraId, camera.Guid);
        Assert.Equal("Lobby Camera", camera.Name);
        Assert.True(camera.IsOnline);
        Assert.False(camera.IsMock);
    }

    [Fact]
    public void Web_player_endpoint_points_at_gwp_js()
    {
        var service = Create(_ => Json("{\"Rsp\":{\"Status\":\"Ok\"}}"));
        var endpoint = service.GetPlaybackEndpoint();
        Assert.Equal("GenetecWebPlayer", endpoint.Provider);
        Assert.Equal("https://gateway.example/media/v2/files/gwp.js", endpoint.GwpScriptUrl);
        Assert.True(endpoint.SupportsBrowserPlayback);
    }

    private static WebSdkGenetecService Create(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHandler(responder);
        var web = new HttpClient(handler) { BaseAddress = new Uri("https://directory.example:4590/WebSdk/") };
        var media = new HttpClient(handler);
        var options = Options.Create(new GenetecOptions
        {
            Mode = "Real",
            RealTransport = "WebSdk",
            WebSdk = new WebSdkOptions
            {
                BaseUrl = "https://directory.example:4590/WebSdk/",
                Username = "apiuser",
                Password = "secret",
                ApplicationId = "certificate"
            },
            MediaGateway = new MediaGatewayOptions
            {
                ServerEndpoint = "https://gateway.example/media",
                BrowserEndpoint = "https://gateway.example/media"
            }
        });
        return new WebSdkGenetecService(web, media, options, TimeProvider.System, NullLogger<WebSdkGenetecService>.Instance);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}
