using System.Text.Json;

namespace Investigation.Genetec;

public static class WebSdkJson
{
    public static WebSdkDocument Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("Rsp", out var response) &&
                !document.RootElement.TryGetProperty("rsp", out response))
            {
                return new WebSdkDocument(null, "Response did not contain an Rsp object.", null);
            }

            var status = response.TryGetProperty("Status", out var statusProperty) ? statusProperty.GetString() : null;
            var message = response.TryGetProperty("Message", out var messageProperty) ? messageProperty.GetString() : null;
            JsonElement? result = response.TryGetProperty("Result", out var resultProperty) ? resultProperty.Clone() : null;
            return new WebSdkDocument(status, message, result);
        }
        catch (JsonException exception)
        {
            return new WebSdkDocument(null, exception.Message, null);
        }
    }
}

public sealed record WebSdkDocument(string? Status, string? Message, JsonElement? Result);
