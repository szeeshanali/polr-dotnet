using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PolrNet.Data;
using PolrNet.Services;

namespace PolrNet.Endpoints;

/// <summary>
/// The public API (v2). URLs, arguments and responses stay stable so existing client libraries keep working. See docs/API.md.
/// </summary>
public static class ApiV2Endpoints
{
    public static void MapApiV2(this WebApplication app)
    {
        // Used by the shorten form to check a custom ending before submitting.
        app.MapGet("/api/v2/link_avail_check", async ([FromQuery(Name = "link_ending")] string? ending, LinkRepository links) =>
            !Rules.IsValidEnding(ending) ? "invalid"
            : Rules.IsReserved(ending) || await links.FindAsync(ending) is not null ? "unavailable"
            : "available");

        var api = app.MapGroup("/api/v2").AddEndpointFilter(AuthenticateAsync);
        string[] getOrPost = ["GET", "POST"];
        api.MapMethods("action/shorten", getOrPost, ShortenAsync);
        api.MapPost("action/shorten_bulk", ShortenBulkAsync);
        api.MapMethods("action/lookup", getOrPost, LookupAsync);
        api.MapMethods("data/link", getOrPost, LinkDataAsync);
    }

    static async Task<IResult> ShortenAsync(ApiRequest request, LinkService links)
    {
        if (!Rules.TryNormalizeUrl(request["url"], out var url))
            throw MissingParameters();
        var shortUrl = await CreateAsync(links, request, url, request["is_secret"], request["custom_ending"]);
        return Ok(request, "shorten", shortUrl);
    }

    static async Task<IResult> ShortenBulkAsync(ApiRequest request, LinkService links)
    {
        request.ResponseType ??= "json";
        if (request.ResponseType != "json")
            throw new ApiError(401, "JSON_ONLY", "Only JSON-encoded responses are available for this endpoint.");

        // "data" is a JSON string: {"links": [{"url": "...", "is_secret": true, "custom_ending": "..."}]}
        List<(string Url, string? IsSecret, string? CustomEnding)> items = [];
        try
        {
            using var data = JsonDocument.Parse(request["data"] ?? "");
            foreach (var link in data.RootElement.GetProperty("links").EnumerateArray())
            {
                if (!Rules.TryNormalizeUrl(ApiRequest.Text(link, "url"), out var url))
                    throw MissingParameters();
                items.Add((url, ApiRequest.Text(link, "is_secret"), ApiRequest.Text(link, "custom_ending")));
            }
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new ApiError(400, "INVALID_PARAMETERS", "Invalid JSON.");
        }

        var shortened = new List<object>();
        foreach (var (url, isSecret, customEnding) in items)
            shortened.Add(new { long_url = url, short_url = await CreateAsync(links, request, url, isSecret, customEnding) });
        return Ok(request, "shorten_bulk", new { shortened_links = shortened });
    }

    static async Task<IResult> LookupAsync(ApiRequest request, LinkRepository links)
    {
        var ending = request["url_ending"];
        if (!Rules.IsValidEnding(ending))
            throw MissingParameters();

        var link = await links.FindAsync(ending) ?? throw new ApiError(404, "NOT_FOUND", "Link not found.");
        if (link.SecretKey != "" && !Crypto.SecureEquals(request["url_key"], link.SecretKey))
            throw new ApiError(401, "ACCESS_DENIED", "Invalid URL code for secret URL.");

        var result = new { long_url = link.LongUrl, created_at = ApiDate(link.CreatedAt), clicks = link.Clicks, updated_at = ApiDate(link.UpdatedAt) };
        return Ok(request, "lookup", result, plainText: link.LongUrl);
    }

    static async Task<IResult> LinkDataAsync(ApiRequest request, LinkRepository links, ClickRepository clicks, PolrNetOptions options)
    {
        request.ResponseType ??= "json";
        if (request.User.Anonymous)
            throw new ApiError(401, "AUTH_ERROR", "Anonymous access of this API is not permitted.");
        if (request.ResponseType != "json")
            throw new ApiError(401, "JSON_ONLY", "Only JSON-encoded data is available for this endpoint.");

        var ending = request["url_ending"];
        if (!Rules.IsValidEnding(ending) || !TryParseDate(request["left_bound"], out var from) || !TryParseDate(request["right_bound"], out var to))
            throw MissingParameters();

        var link = await links.FindAsync(ending) ?? throw new ApiError(404, "NOT_FOUND", "Link not found.");
        if (link.Creator != request.User.Username && !request.User.IsAdmin)
            throw new ApiError(401, "ACCESS_DENIED", "Unauthorized.");
        if (ClickRepository.CheckRange(from, to, options.AnalyticsMaxDays) is { } error)
            throw new ApiError(400, "ANALYTICS_ERROR", error);

        var type = request["stats_type"];
        object data = type switch
        {
            "day" => await clicks.ByDayAsync(link.Id, from, to),
            "country" => await clicks.ByCountryAsync(link.Id, from, to),
            "referer" => await clicks.ByRefererAsync(link.Id, from, to),
            _ => throw new ApiError(400, "INVALID_ANALYTICS_TYPE", "Invalid analytics type requested."),
        };
        return Ok(request, $"data_link_{type}", new { url_ending = link.ShortUrl, data });
    }

    /// <summary>Finds the API user from "key" (or allows anonymous access), enforces the quota and turns ApiErrors into responses.</summary>
    static async ValueTask<object?> AuthenticateAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var request = context.Arguments.OfType<ApiRequest>().First();
        http.Response.Headers.AccessControlAllowOrigin = "*";
        try
        {
            request.User = await FindUserAsync(request, http.RequestServices);
            return await next(context);
        }
        catch (ApiError e)
        {
            return request.ResponseType == "json"
                ? Results.Json(new { status_code = e.StatusCode, error_code = e.Code, error = e.Message }, statusCode: e.StatusCode)
                : Results.Text($"{e.StatusCode} {e.Message}", "text/plain", statusCode: e.StatusCode);
        }
    }

    static async Task<ApiUser> FindUserAsync(ApiRequest request, IServiceProvider services)
    {
        var options = services.GetRequiredService<PolrNetOptions>();
        ApiUser user;
        if (request["key"] is { } key)
        {
            var found = await services.GetRequiredService<UserRepository>().FindByApiKeyAsync(key)
                ?? throw new ApiError(401, "AUTH_ERROR", "Authentication token invalid.");
            user = new ApiUser(found.Username, found.IsAdmin, Anonymous: false, found.ApiQuota);
        }
        else if (options.AnonymousApi)
        {
            user = new ApiUser($"ANONIP-{request.Ip}", IsAdmin: false, Anonymous: true, options.AnonymousApiQuota);
        }
        else
        {
            throw new ApiError(401, "AUTH_ERROR", "Authentication token required.");
        }

        // Quota = links created through the API in the last minute; negative means unlimited.
        if (user.Quota >= 0 && await services.GetRequiredService<LinkRepository>().CountRecentApiLinksAsync(user.Username) >= user.Quota)
            throw new ApiError(429, "QUOTA_EXCEEDED", "Quota exceeded.");
        return user;
    }

    static async Task<string> CreateAsync(LinkService links, ApiRequest request, string url, string? isSecret, string? customEnding)
    {
        try
        {
            return await links.ShortenAsync(url, isSecret == "true", customEnding, request.Ip, request.User.Username, isApi: true);
        }
        catch (LinkException e)
        {
            throw new ApiError(400, "CREATION_ERROR", e.Message);
        }
    }

    static IResult Ok(ApiRequest request, string action, object result, string? plainText = null) =>
        request.ResponseType == "json"
            ? Results.Json(new { action, result })
            : Results.Text(plainText ?? result.ToString(), "text/plain");

    static ApiError MissingParameters() => new(400, "MISSING_PARAMETERS", "Invalid or missing parameters.");

    // The date object shape that existing API clients expect.
    static object? ApiDate(DateTime? date) => date is null ? null : new
    {
        date = date.Value.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture),
        timezone_type = 3,
        timezone = "UTC",
    };

    // A missing bound means "now".
    static bool TryParseDate(string? value, out DateTime date)
    {
        if (value is null)
        {
            date = DateTime.UtcNow;
            return true;
        }
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
    }
}

/// <summary>API arguments merged from the query string, a form body and a JSON body.</summary>
public sealed class ApiRequest
{
    readonly Dictionary<string, string> values;

    ApiRequest(Dictionary<string, string> values, string ip)
    {
        this.values = values;
        Ip = ip;
        ResponseType = this["response_type"];
    }

    /// <summary>The argument's value, or null when it is missing or empty.</summary>
    public string? this[string name] => values.TryGetValue(name, out var value) && value.Length > 0 ? value : null;

    public string Ip { get; }

    /// <summary>"json" or anything else for plain text. Some endpoints default to "json".</summary>
    public string? ResponseType { get; set; }

    public ApiUser User { get; set; } = null!;

    public static async ValueTask<ApiRequest?> BindAsync(HttpContext http)
    {
        var values = new Dictionary<string, string>();
        foreach (var (key, value) in http.Request.Query)
            values[key] = value.ToString();

        if (http.Request.HasFormContentType)
        {
            foreach (var (key, value) in await http.Request.ReadFormAsync())
                values[key] = value.ToString();
        }
        else if (http.Request.HasJsonContentType())
        {
            try
            {
                using var json = await JsonDocument.ParseAsync(http.Request.Body);
                if (json.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in json.RootElement.EnumerateObject())
                        values[property.Name] = Text(property.Value) ?? "";
                }
            }
            catch (JsonException)
            {
                // Treated like a request without arguments.
            }
        }
        return new ApiRequest(values, http.ClientIp());
    }

    /// <summary>A property of a JSON object as text: strings as-is, other values (true, 42, objects) as raw JSON.</summary>
    public static string? Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) ? Text(value) : null;

    static string? Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Null => null,
        _ => value.GetRawText(),
    };
}

public sealed record ApiUser(string Username, bool IsAdmin, bool Anonymous, int Quota);

public sealed class ApiError(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode => statusCode;
    public string Code => code;
}
