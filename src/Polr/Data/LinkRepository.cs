using Dapper;
using MySqlConnector;
using Polr.Services;

namespace Polr.Data;

public sealed class LinkRepository(Db db)
{
    const string Columns = "id, short_url, long_url, creator, clicks, secret_key, is_disabled, is_custom, created_at, updated_at";

    public async Task<Link?> FindAsync(string ending)
    {
        await using var c = db.Connect();
        return await c.QueryFirstOrDefaultAsync<Link>($"SELECT {Columns} FROM links WHERE short_url = @ending", new { ending });
    }

    /// <summary>Finds a plain (not custom, not secret) link to the same URL by the same creator, so it can be reused.</summary>
    public async Task<Link?> FindReusableAsync(string longUrl, string creator)
    {
        await using var c = db.Connect();
        return await c.QueryFirstOrDefaultAsync<Link>(
            $"""
            SELECT {Columns} FROM links
            WHERE long_url_hash = @hash AND long_url = @longUrl AND creator = @creator AND is_custom = 0 AND secret_key = ''
            LIMIT 1
            """,
            new { hash = Rules.Crc32(longUrl), longUrl, creator });
    }

    /// <summary>Inserts a link. Returns false when the ending is already taken.</summary>
    public async Task<bool> TryInsertAsync(string ending, string longUrl, string ip, string creator, string secretKey, bool isCustom, bool isApi)
    {
        await using var c = db.Connect();
        try
        {
            await c.ExecuteAsync(
                """
                INSERT INTO links (short_url, long_url, long_url_hash, ip, creator, secret_key, is_custom, is_api, created_at, updated_at)
                VALUES (@ending, @longUrl, @hash, @ip, @creator, @secretKey, @isCustom, @isApi, UTC_TIMESTAMP(), UTC_TIMESTAMP())
                """,
                new { ending, longUrl, hash = Rules.Crc32(longUrl), ip, creator, secretKey, isCustom, isApi });
            return true;
        }
        catch (MySqlException e) when (e.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
        {
            return false;
        }
    }

    /// <summary>The next auto-increment id; counter based endings are this number in base 32 or 62.</summary>
    public async Task<long> NextIdAsync()
    {
        await using var c = db.Connect();
        return await c.ExecuteScalarAsync<long>("SELECT COALESCE(MAX(id), 0) + 1 FROM links");
    }

    /// <summary>Links created through the API by this creator in the last minute (the API quota).</summary>
    public async Task<int> CountRecentApiLinksAsync(string creator)
    {
        await using var c = db.Connect();
        return await c.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM links WHERE creator = @creator AND is_api = 1 AND created_at >= UTC_TIMESTAMP() - INTERVAL 1 MINUTE",
            new { creator });
    }

    /// <summary>Newest first. Pass a creator to list only their links.</summary>
    public async Task<PagedList<Link>> ListAsync(string? creator, string? search, int page, int pageSize)
    {
        const string Where = "WHERE (@creator IS NULL OR creator = @creator) AND (@like IS NULL OR short_url LIKE @like OR long_url LIKE @like)";
        page = Math.Max(1, page);
        await using var c = db.Connect();
        using var results = await c.QueryMultipleAsync(
            $"""
            SELECT COUNT(*) FROM links {Where};
            SELECT {Columns} FROM links {Where} ORDER BY id DESC LIMIT @pageSize OFFSET @offset;
            """,
            new { creator, like = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%", pageSize, offset = (page - 1) * pageSize });
        var total = await results.ReadSingleAsync<int>();
        var items = (await results.ReadAsync<Link>()).AsList();
        return new PagedList<Link>(items, total, page, pageSize);
    }

    public Task<bool> ToggleDisabledAsync(string ending) =>
        ExecuteAsync("UPDATE links SET is_disabled = NOT is_disabled, updated_at = UTC_TIMESTAMP() WHERE short_url = @ending", new { ending });

    public Task<bool> UpdateLongUrlAsync(string ending, string longUrl) =>
        ExecuteAsync(
            "UPDATE links SET long_url = @longUrl, long_url_hash = @hash, updated_at = UTC_TIMESTAMP() WHERE short_url = @ending",
            new { ending, longUrl, hash = Rules.Crc32(longUrl) });

    public Task<bool> DeleteAsync(string ending) =>
        ExecuteAsync("DELETE FROM links WHERE short_url = @ending", new { ending });

    async Task<bool> ExecuteAsync(string sql, object args)
    {
        await using var c = db.Connect();
        return await c.ExecuteAsync(sql, args) > 0;
    }
}
