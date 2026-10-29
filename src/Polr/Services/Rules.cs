using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace Polr.Services;

/// <summary>Validation and encoding rules. No I/O, so they are easy to test.</summary>
public static partial class Rules
{
    const string Digits = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const int MaxUrlLength = 65535;

    static readonly string[] KnownShorteners = ["polr.me", "bit.ly", "is.gd", "tiny.cc", "adf.ly", "ur1.ca", "goo.gl", "ow.ly", "j.mp", "t.co"];

    // Single-segment paths used by the app itself, so they can never be link endings.
    static readonly HashSet<string> ReservedEndings = new(StringComparer.OrdinalIgnoreCase)
    {
        "about-polr", "activate", "admin", "api", "css", "error", "health", "index", "js", "login", "logout", "lost_password", "reset_password", "signup",
    };

    [GeneratedRegex("^[A-Za-z0-9_-]{1,255}$")]
    private static partial Regex NameRegex();

    /// <summary>Letters, numbers, hyphens and underscores.</summary>
    public static bool IsValidEnding([NotNullWhen(true)] string? ending) => ending is not null && NameRegex().IsMatch(ending);

    public static bool IsValidUsername([NotNullWhen(true)] string? username) => username is not null && NameRegex().IsMatch(username);

    public static bool IsReserved(string ending) => ReservedEndings.Contains(ending);

    public const string PasswordHint = "Passwords must be at least 8 characters long.";

    public static bool IsValidPassword([NotNullWhen(true)] string? password) => password is { Length: >= 8 };

    public static bool IsValidEmail([NotNullWhen(true)] string? email) =>
        email is not null && MailAddress.TryCreate(email, out var address) && address.Address == email;

    /// <summary>Accepts absolute http and https URLs only. Spaces are encoded as %20.</summary>
    public static bool TryNormalizeUrl(string? input, [NotNullWhen(true)] out string? url)
    {
        url = input?.Trim().Replace(" ", "%20");
        return url is { Length: > 0 and <= MaxUrlLength }
            && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && uri.Host.Length > 0;
    }

    /// <summary>True when the URL points at this instance or a well-known URL shortener.</summary>
    public static bool IsShortenedUrl(string url, string ownAuthority)
    {
        var uri = new Uri(url);
        return uri.Authority.Equals(ownAuthority, StringComparison.OrdinalIgnoreCase)
            || KnownShorteners.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Writes a number with the first <paramref name="numberBase"/> characters of 0-9a-zA-Z.</summary>
    public static string ToBase(long value, int numberBase)
    {
        var result = new StringBuilder();
        do
        {
            result.Insert(0, Digits[(int)(value % numberBase)]);
            value /= numberBase;
        } while (value > 0);
        return result.ToString();
    }

    /// <summary>CRC32 as an unsigned decimal string, matching the long_url_hash values in existing Polr databases.</summary>
    public static string Crc32(string value) =>
        System.IO.Hashing.Crc32.HashToUInt32(Encoding.UTF8.GetBytes(value)).ToString(CultureInfo.InvariantCulture);
}
