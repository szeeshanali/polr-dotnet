using System.Text;
using Dapper;
using MySqlConnector;

namespace PolrNet.Data;

public sealed class ClickRepository(Db db)
{
    /// <summary>Adds the clicks to each link's counter and, when <paramref name="details"/> is set, stores one row per click.</summary>
    public async Task SaveAsync(IReadOnlyList<ClickEvent> clicks, bool details)
    {
        await using var c = db.Connect();
        await c.OpenAsync();
        await using var tx = await c.BeginTransactionAsync();

        // Ordered by id so concurrent instances lock rows in the same order and cannot deadlock.
        var counts = clicks.GroupBy(x => x.LinkId).OrderBy(g => g.Key).Select(g => new { id = g.Key, n = g.Count() });
        await c.ExecuteAsync("UPDATE links SET clicks = clicks + @n WHERE id = @id", counts, tx);

        if (details)
        {
            foreach (var chunk in clicks.Chunk(500))
                await InsertAsync(c, tx, chunk);
        }

        await tx.CommitAsync();
    }

    // One multi-row INSERT per chunk. IGNORE skips clicks whose link was deleted in the meantime.
    static Task InsertAsync(MySqlConnection c, MySqlTransaction tx, ClickEvent[] clicks)
    {
        var sql = new StringBuilder("INSERT IGNORE INTO clicks (link_id, ip, country, referer, referer_host, user_agent, created_at, updated_at) VALUES ");
        var args = new DynamicParameters();
        for (var i = 0; i < clicks.Length; i++)
        {
            var click = clicks[i];
            sql.Append(i == 0 ? "" : ",").Append($"(@l{i},@i{i},@c{i},@r{i},@h{i},@u{i},@t{i},@t{i})");
            args.Add($"l{i}", click.LinkId);
            args.Add($"i{i}", click.Ip);
            args.Add($"c{i}", click.Country);
            args.Add($"r{i}", click.Referer);
            args.Add($"h{i}", click.RefererHost);
            args.Add($"u{i}", click.UserAgent);
            args.Add($"t{i}", click.At);
        }
        return c.ExecuteAsync(sql.ToString(), args, tx);
    }

    /// <summary>Returns an error message when the date range is not allowed, otherwise null.</summary>
    public static string? CheckRange(DateTime from, DateTime to, int maxDays) =>
        from > to ? "Invalid bounds." : (to - from).Days > maxDays ? "Bounds too broad." : null;

    public Task<List<DayStat>> ByDayAsync(int linkId, DateTime from, DateTime to) => QueryAsync<DayStat>(
        """
        SELECT DATE_FORMAT(created_at, '%Y-%m-%d') AS x, COUNT(*) AS y FROM clicks
        WHERE link_id = @linkId AND created_at BETWEEN @from AND @to
        GROUP BY x ORDER BY x
        """, linkId, from, to);

    public Task<List<LabelStat>> ByCountryAsync(int linkId, DateTime from, DateTime to) => QueryAsync<LabelStat>(
        """
        SELECT country AS label, COUNT(*) AS clicks FROM clicks
        WHERE link_id = @linkId AND created_at BETWEEN @from AND @to AND country IS NOT NULL
        GROUP BY country ORDER BY clicks DESC
        """, linkId, from, to);

    public Task<List<LabelStat>> ByRefererAsync(int linkId, DateTime from, DateTime to) => QueryAsync<LabelStat>(
        """
        SELECT COALESCE(referer_host, 'Direct') AS label, COUNT(*) AS clicks FROM clicks
        WHERE link_id = @linkId AND created_at BETWEEN @from AND @to
        GROUP BY referer_host ORDER BY clicks DESC
        """, linkId, from, to);

    async Task<List<T>> QueryAsync<T>(string sql, int linkId, DateTime from, DateTime to)
    {
        await using var c = db.Connect();
        return (await c.QueryAsync<T>(sql, new { linkId, from, to })).AsList();
    }
}
