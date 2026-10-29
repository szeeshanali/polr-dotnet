using Dapper;
using MySqlConnector;

namespace PolrNet.Data;

/// <summary>Opens MySQL connections and applies the schema scripts embedded from Data/Sql.</summary>
public sealed class Db(string connectionString)
{
    // Map snake_case columns (short_url) to PascalCase properties (ShortUrl).
    static Db() => DefaultTypeMap.MatchNamesWithUnderscores = true;

    public MySqlConnection Connect() => new(connectionString);

    /// <summary>Runs every Data/Sql/NNN_name.sql script that has not been applied yet, in order.</summary>
    public async Task MigrateAsync()
    {
        await using var db = Connect();
        await db.OpenAsync();

        // When several instances start together, only one of them migrates at a time.
        await db.ExecuteScalarAsync("SELECT GET_LOCK('polrnet_migrate', 60)");
        try
        {
            await db.ExecuteAsync("CREATE TABLE IF NOT EXISTS polrnet_schema (version INT NOT NULL PRIMARY KEY, applied_at DATETIME NOT NULL)");
            var applied = (await db.QueryAsync<int>("SELECT version FROM polrnet_schema")).ToHashSet();

            var assembly = typeof(Db).Assembly;
            foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql")).Order())
            {
                // "PolrNet.Data.Sql.001_init.sql" -> 1
                var version = int.Parse(resource.Split('.')[^2].Split('_')[0]);
                if (applied.Contains(version))
                    continue;

                using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
                await db.ExecuteAsync(await reader.ReadToEndAsync());
                await db.ExecuteAsync("INSERT INTO polrnet_schema (version, applied_at) VALUES (@version, UTC_TIMESTAMP())", new { version });
            }
        }
        finally
        {
            await db.ExecuteScalarAsync("SELECT RELEASE_LOCK('polrnet_migrate')");
        }
    }
}
