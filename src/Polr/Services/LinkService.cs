using Microsoft.Extensions.Caching.Memory;
using Polr.Data;

namespace Polr.Services;

/// <summary>An error whose message can be shown to the user as-is.</summary>
public sealed class LinkException(string message) : Exception(message);

public sealed class LinkService(LinkRepository links, IMemoryCache cache, PolrOptions options)
{
    readonly string ownAuthority = new Uri(options.BaseUrl).Authority;

    public string ShortUrl(string ending, string secretKey = "") =>
        secretKey == "" ? $"{options.BaseUrl}/{ending}" : $"{options.BaseUrl}/{ending}/{secretKey}";

    /// <summary>Creates a short link and returns its full URL. A plain link to a URL the creator already shortened is reused.</summary>
    public async Task<string> ShortenAsync(string? longUrl, bool secret, string? customEnding, string ip, string creator, bool isApi)
    {
        if (!Rules.TryNormalizeUrl(longUrl, out var url))
            throw new LinkException("Please enter a valid http:// or https:// URL.");
        if (Rules.IsShortenedUrl(url, ownAuthority))
            throw new LinkException("Sorry, but your link already looks like a shortened URL.");

        var custom = string.IsNullOrWhiteSpace(customEnding) ? null : customEnding.Trim();
        if (custom is null && !secret && await links.FindReusableAsync(url, creator) is { } existing)
            return ShortUrl(existing.ShortUrl);

        var secretKey = secret ? Crypto.RandomHex(options.SecretKeyBytes) : "";

        if (custom is not null)
        {
            if (!Rules.IsValidEnding(custom))
                throw new LinkException("Custom endings can only contain letters, numbers, hyphens and underscores.");
            if (Rules.IsReserved(custom) || !await links.TryInsertAsync(custom, url, ip, creator, secretKey, isCustom: true, isApi))
                throw new LinkException("This URL ending is already in use.");
            return ShortUrl(custom, secretKey);
        }

        // The unique index on short_url settles races: when a candidate is taken, try the next one.
        var counter = options.RandomEndings ? 0 : await links.NextIdAsync();
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var ending = options.RandomEndings
                ? Crypto.RandomString(options.RandomEndingLength)
                : Rules.ToBase(counter++, options.EndingBase);

            if (!Rules.IsReserved(ending) && await links.TryInsertAsync(ending, url, ip, creator, secretKey, isCustom: false, isApi))
                return ShortUrl(ending, secretKey);

            if (!options.RandomEndings)
                counter = Math.Max(counter, await links.NextIdAsync());
        }
        throw new LinkException("Could not find a free ending. Please try again.");
    }

    /// <summary>Looks up a link for redirecting, with a short-lived in-memory cache in front of the database.</summary>
    public async Task<Link?> ResolveAsync(string ending)
    {
        var key = $"link:{ending}";
        if (cache.TryGetValue(key, out Link? link))
            return link;

        link = await links.FindAsync(ending);
        if (link is not null)
            cache.Set(key, link, TimeSpan.FromSeconds(options.LinkCacheSeconds));
        return link;
    }

    public async Task<bool> ToggleDisabledAsync(string ending)
    {
        var found = await links.ToggleDisabledAsync(ending);
        Forget(ending);
        return found;
    }

    public async Task<bool> DeleteAsync(string ending)
    {
        var found = await links.DeleteAsync(ending);
        Forget(ending);
        return found;
    }

    public async Task UpdateLongUrlAsync(string ending, string? longUrl)
    {
        if (!Rules.TryNormalizeUrl(longUrl, out var url))
            throw new LinkException("Please enter a valid http:// or https:// URL.");
        await links.UpdateLongUrlAsync(ending, url);
        Forget(ending);
    }

    void Forget(string ending) => cache.Remove($"link:{ending}");
}
