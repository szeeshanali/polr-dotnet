namespace Polr.Data;

public sealed record Link
{
    public int Id { get; init; }
    public string ShortUrl { get; init; } = "";
    public string LongUrl { get; init; } = "";
    public string Creator { get; init; } = "";
    public int Clicks { get; init; }
    /// <summary>Empty for normal links. Secret links only redirect when this key is the second path segment.</summary>
    public string SecretKey { get; init; } = "";
    public bool IsDisabled { get; init; }
    public bool IsCustom { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record User
{
    public int Id { get; init; }
    public string Username { get; init; } = "";
    /// <summary>bcrypt hash.</summary>
    public string PasswordHash { get; init; } = "";
    public string Email { get; init; } = "";
    public string Role { get; init; } = Roles.Default;
    public bool Active { get; init; }
    /// <summary>One-time key sent by email for account activation and password resets.</summary>
    public string RecoveryKey { get; init; } = "";
    public string? ApiKey { get; init; }
    public bool ApiActive { get; init; }
    /// <summary>Links this user may create through the API per minute. Negative means unlimited.</summary>
    public int ApiQuota { get; init; }
    public DateTime? CreatedAt { get; init; }

    public bool IsAdmin => Role == Roles.Admin;
}

public static class Roles
{
    public const string Admin = "admin";
    public const string Default = "";
}

public sealed record PagedList<T>(IReadOnlyList<T> Items, int Total, int PageNumber, int PageSize)
{
    public int PageCount => Math.Max(1, (Total + PageSize - 1) / PageSize);
}

/// <summary>A click waiting to be written. Country and RefererHost are filled in just before saving.</summary>
public readonly record struct ClickEvent(
    int LinkId, DateTime At, string Ip, string? Referer, string? UserAgent, string? Country = null, string? RefererHost = null);

public sealed record DayStat(string X, long Y);

public sealed record LabelStat(string Label, long Clicks);
