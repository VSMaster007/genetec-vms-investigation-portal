namespace Investigation.Genetec;

public sealed record MockCameraDefinition(
    Guid Guid,
    string Name,
    string Location,
    string ExternalId,
    bool Online,
    bool AccessGranted,
    TimeSpan Retention,
    bool DailyMaintenanceGap);

public static class MockCameraCatalog
{
    public static readonly IReadOnlyList<MockCameraDefinition> Cameras =
    [
        new(Guid.Parse("8f1c2a40-6b1e-4a1a-9c1a-111111111111"), "Main Entrance Camera (mock)", "Main Entrance", "CAM-ENTRANCE-01", true, true, TimeSpan.FromDays(30), true),
        new(Guid.Parse("8f1c2a40-6b1e-4a1a-9c1a-222222222222"), "Lobby Camera (mock)", "Lobby", "CAM-LOBBY-02", true, true, TimeSpan.FromDays(30), false),
        new(Guid.Parse("8f1c2a40-6b1e-4a1a-9c1a-333333333333"), "Parking Camera (mock)", "West Parking", "CAM-PARKING-03", false, true, TimeSpan.FromDays(30), false),
        new(Guid.Parse("8f1c2a40-6b1e-4a1a-9c1a-444444444444"), "Loading Dock Camera (mock)", "Loading Dock", "CAM-LOADING-04", true, true, TimeSpan.FromDays(7), false),
        new(Guid.Parse("8f1c2a40-6b1e-4a1a-9c1a-555555555555"), "Server Room Camera (mock)", "Server Room", "CAM-SERVER-05", true, false, TimeSpan.FromDays(30), false),
        new(Guid.Parse("8f1c2a40-6b1e-4a1a-9c1a-666666666666"), "Perimeter Camera (mock)", "North Perimeter", "CAM-PERIMETER-06", true, true, TimeSpan.FromDays(30), false)
    ];
}
