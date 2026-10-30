using System.Net.Http.Headers;
using System.Text;

namespace Investigation.Genetec;

public sealed class GenetecOptions
{
    public const string SectionName = "Genetec";
    public string Mode { get; set; } = "Mock";
    public string RealTransport { get; set; } = "WebSdk";
    public bool AllowInvalidCertificates { get; set; }
    public MockOptions Mock { get; set; } = new();
    public WebSdkOptions WebSdk { get; set; } = new();
    public MediaGatewayOptions MediaGateway { get; set; } = new();
    public PlatformOptions Platform { get; set; } = new();
}

public sealed class MockOptions
{
    public bool FailConnection { get; set; }
}

public sealed class WebSdkOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
}

public sealed class MediaGatewayOptions
{
    public string ServerEndpoint { get; set; } = string.Empty;
    public string BrowserEndpoint { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SdkCertificate { get; set; } = string.Empty;
}

public sealed class PlatformOptions
{
    public string Directory { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public bool AcceptDirectoryCertificate { get; set; }
}

public static class GenetecCredentials
{
    public static string Basic(string username, string certificate, string password)
    {
        var raw = $"{username};{certificate}:{password}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static AuthenticationHeaderValue Header(string username, string certificate, string password) =>
        new("Basic", Basic(username, certificate, password));
}
