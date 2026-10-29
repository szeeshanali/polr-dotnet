using Dapper;
using Polr.Services;

namespace Polr.Data;

public sealed class UserRepository(Db db)
{
    const string Columns = "id, username, password AS password_hash, email, role, active, recovery_key, api_key, api_active, api_quota, created_at";

    public Task<User?> FindByUsernameAsync(string username, bool activeOnly = true) => FindAsync("username = @value", username, activeOnly);

    public Task<User?> FindByEmailAsync(string email) => FindAsync("email = @value", email, activeOnly: true);

    public Task<User?> FindByIdAsync(int id) => FindAsync("id = @value", id, activeOnly: false);

    public Task<User?> FindByApiKeyAsync(string apiKey) => FindAsync("api_key = @value AND api_active = 1", apiKey, activeOnly: true);

    // condition is always one of the constants above, never user input.
    async Task<User?> FindAsync(string condition, object value, bool activeOnly)
    {
        await using var c = db.Connect();
        var active = activeOnly ? " AND active = 1" : "";
        return await c.QueryFirstOrDefaultAsync<User>($"SELECT {Columns} FROM users WHERE {condition}{active} LIMIT 1", new { value });
    }

    public async Task<bool> ExistsAsync(string username, string email)
    {
        await using var c = db.Connect();
        return await c.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM users WHERE username = @username OR email = @email)", new { username, email });
    }

    /// <summary>Creates a user with a hashed password and a fresh recovery key. A non-null API key also enables API access.</summary>
    public async Task<User> CreateAsync(string username, string email, string password, string ip, string role, bool active, string? apiKey)
    {
        await using var c = db.Connect();
        var id = await c.ExecuteScalarAsync<int>(
            """
            INSERT INTO users (username, password, email, ip, recovery_key, role, active, api_key, api_active, created_at, updated_at)
            VALUES (@username, @hash, @email, @ip, @recoveryKey, @role, @active, @apiKey, @apiActive, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();
            """,
            new
            {
                username, email, ip, role, active, apiKey,
                hash = Crypto.HashPassword(password),
                recoveryKey = Crypto.RandomHex(50),
                apiActive = apiKey is not null,
            });
        return (await FindByIdAsync(id))!;
    }

    public async Task<PagedList<User>> ListAsync(string? search, int page, int pageSize)
    {
        const string Where = "WHERE @like IS NULL OR username LIKE @like OR email LIKE @like";
        page = Math.Max(1, page);
        await using var c = db.Connect();
        using var results = await c.QueryMultipleAsync(
            $"""
            SELECT COUNT(*) FROM users {Where};
            SELECT {Columns} FROM users {Where} ORDER BY id DESC LIMIT @pageSize OFFSET @offset;
            """,
            new { like = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%", pageSize, offset = (page - 1) * pageSize });
        var total = await results.ReadSingleAsync<int>();
        var items = (await results.ReadAsync<User>()).AsList();
        return new PagedList<User>(items, total, page, pageSize);
    }

    /// <summary>Sets a new password and invalidates any outstanding recovery key.</summary>
    public Task SetPasswordAsync(int id, string password) =>
        ExecuteAsync(
            "UPDATE users SET password = @hash, recovery_key = @key, updated_at = UTC_TIMESTAMP() WHERE id = @id",
            new { id, hash = Crypto.HashPassword(password), key = Crypto.RandomHex(50) });

    public async Task<string> NewRecoveryKeyAsync(int id)
    {
        var key = Crypto.RandomHex(50);
        await ExecuteAsync("UPDATE users SET recovery_key = @key, updated_at = UTC_TIMESTAMP() WHERE id = @id", new { id, key });
        return key;
    }

    public Task ActivateAsync(int id) =>
        ExecuteAsync("UPDATE users SET active = 1, recovery_key = @key, updated_at = UTC_TIMESTAMP() WHERE id = @id", new { id, key = Crypto.RandomHex(50) });

    public Task<bool> ToggleActiveAsync(int id) => ExecuteAsync("UPDATE users SET active = NOT active, updated_at = UTC_TIMESTAMP() WHERE id = @id", new { id });

    public Task<bool> SetRoleAsync(int id, string role) => ExecuteAsync("UPDATE users SET role = @role, updated_at = UTC_TIMESTAMP() WHERE id = @id", new { id, role });

    public Task<bool> ToggleApiAsync(int id) => ExecuteAsync("UPDATE users SET api_active = NOT api_active, updated_at = UTC_TIMESTAMP() WHERE id = @id", new { id });

    public Task<bool> SetApiQuotaAsync(int id, int quota) => ExecuteAsync("UPDATE users SET api_quota = @quota, updated_at = UTC_TIMESTAMP() WHERE id = @id", new { id, quota });

    public Task<bool> SetApiKeyAsync(int id, string apiKey) => ExecuteAsync("UPDATE users SET api_key = @apiKey, updated_at = UTC_TIMESTAMP() WHERE id = @id", new { id, apiKey });

    public Task<bool> DeleteAsync(int id) => ExecuteAsync("DELETE FROM users WHERE id = @id", new { id });

    async Task<bool> ExecuteAsync(string sql, object args)
    {
        await using var c = db.Connect();
        return await c.ExecuteAsync(sql, args) > 0;
    }
}
