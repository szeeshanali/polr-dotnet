using System.Text.Json;

namespace PolrNet.Services;

public sealed class Recaptcha(HttpClient http, PolrNetOptions options)
{
    public async Task<bool> VerifyAsync(string? response, string ip)
    {
        if (string.IsNullOrEmpty(response))
            return false;

        using var result = await http.PostAsync("https://www.google.com/recaptcha/api/siteverify", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["secret"] = options.Recaptcha.SecretKey!, ["response"] = response, ["remoteip"] = ip }));
        var body = await result.Content.ReadFromJsonAsync<JsonElement>();
        return body.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True;
    }
}
