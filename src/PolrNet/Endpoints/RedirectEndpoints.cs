using PolrNet.Services;

namespace PolrNet.Endpoints;

public static class RedirectEndpoints
{
    /// <summary>GET /{ending} and /{ending}/{secretKey}: the hot path. One cached lookup, click recorded in the background.</summary>
    public static void MapRedirects(this WebApplication app) => app.MapGet("/{ending}/{secret?}", RedirectAsync);

    static async Task<IResult> RedirectAsync(string ending, string? secret, HttpContext http, LinkService links, ClickRecorder clicks, PolrNetOptions options)
    {
        var link = Rules.IsValidEnding(ending) ? await links.ResolveAsync(ending) : null;

        if (link is null || link.IsDisabled)
        {
            if (options.Redirect404 && !string.IsNullOrEmpty(options.IndexRedirect))
                return Results.Redirect(options.IndexRedirect);
            return link is null ? Results.NotFound() : Results.StatusCode(StatusCodes.Status410Gone);
        }

        if (link.SecretKey != "" && !Crypto.SecureEquals(secret, link.SecretKey))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        clicks.Record(link.Id, http);
        return Results.Redirect(link.LongUrl, permanent: true);
    }
}
