namespace PolrNet;

/// <summary>All settings, bound from the "PolrNet" configuration section (appsettings.json or PolrNet__* environment variables).</summary>
public sealed class PolrNetOptions
{
    /// <summary>Name shown in the interface and in emails.</summary>
    public string AppName { get; set; } = "Polr.NET";

    /// <summary>Public base address used to build short links, e.g. https://sho.rt</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>Optional Bootstrap 5 stylesheet URL (e.g. a Bootswatch theme). Empty uses stock Bootstrap.</summary>
    public string? Stylesheet { get; set; }

    /// <summary>Show the shorten form to visitors who are not logged in.</summary>
    public bool PublicInterface { get; set; } = true;

    /// <summary>Where anonymous visitors of "/" go when the public interface is off. Empty sends them to /login.</summary>
    public string? IndexRedirect { get; set; }

    /// <summary>Send visitors of unknown or disabled links to <see cref="IndexRedirect"/> instead of an error page.</summary>
    public bool Redirect404 { get; set; }

    /// <summary>Only logged in users may shorten links from the web interface.</summary>
    public bool ShortenRequiresLogin { get; set; }

    public bool AllowSignup { get; set; }
    public bool RequireEmailActivation { get; set; }
    public bool PasswordRecovery { get; set; }

    /// <summary>When not empty, only these email domains may sign up.</summary>
    public string[] AllowedEmailDomains { get; set; } = [];

    /// <summary>Give every new user an active API key.</summary>
    public bool AutoApiKey { get; set; }

    /// <summary>Allow API calls without a key, limited by <see cref="AnonymousApiQuota"/> per IP.</summary>
    public bool AnonymousApi { get; set; }
    public int AnonymousApiQuota { get; set; } = 5;

    /// <summary>Random endings (e.g. "aZ3kd") instead of counter based ones (e.g. "5kq").</summary>
    public bool RandomEndings { get; set; }
    public int RandomEndingLength { get; set; } = 5;

    /// <summary>Alphabet size for counter based endings: 32 (lowercase) or 62 (mixed case).</summary>
    public int EndingBase { get; set; } = 32;

    /// <summary>Record every click (country, referrer, user agent) and enable the stats pages.</summary>
    public bool AdvancedAnalytics { get; set; }
    public int AnalyticsMaxDays { get; set; } = 365;

    /// <summary>Path to a MaxMind GeoLite2-Country.mmdb file. Empty disables country lookups.</summary>
    public string? GeoIpDatabasePath { get; set; }

    public int SecretKeyBytes { get; set; } = 2;
    public int ApiKeyBytes { get; set; } = 15;

    /// <summary>How long a resolved link stays in memory. Edits on another instance show up after at most this long.</summary>
    public int LinkCacheSeconds { get; set; } = 60;

    /// <summary>How often buffered clicks are written to the database.</summary>
    public int ClickFlushMilliseconds { get; set; } = 1000;

    /// <summary>Directory for ASP.NET Core data protection keys. Share it between instances behind a load balancer.</summary>
    public string? DataProtectionKeysPath { get; set; }

    public AdminAccount Admin { get; set; } = new();
    public SmtpSettings Smtp { get; set; } = new();
    public RecaptchaSettings Recaptcha { get; set; } = new();

    public void Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException("PolrNet:BaseUrl must be an absolute URL, e.g. https://sho.rt");
        if (EndingBase is not (32 or 62))
            throw new InvalidOperationException("PolrNet:EndingBase must be 32 or 62.");
        BaseUrl = BaseUrl.TrimEnd('/');
    }
}

/// <summary>Created on startup when no user with this name exists. Remove the password from config afterwards.</summary>
public sealed class AdminAccount
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
}

public sealed class SmtpSettings
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "noreply@example.com";
    public string? FromName { get; set; }
    public bool Enabled => !string.IsNullOrWhiteSpace(Host);
}

public sealed class RecaptchaSettings
{
    public string? SiteKey { get; set; }
    public string? SecretKey { get; set; }
    public bool Enabled => !string.IsNullOrWhiteSpace(SiteKey) && !string.IsNullOrWhiteSpace(SecretKey);
}
