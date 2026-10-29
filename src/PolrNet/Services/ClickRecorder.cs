using System.Net;
using System.Threading.Channels;
using MaxMind.GeoIP2;
using PolrNet.Data;

namespace PolrNet.Services;

/// <summary>
/// Buffers clicks in memory and writes them in batches, so a redirect never waits for the database.
/// If the process crashes, at most <see cref="PolrNetOptions.ClickFlushMilliseconds"/> worth of clicks is lost.
/// </summary>
public sealed class ClickRecorder(ClickRepository clicks, PolrNetOptions options, ILogger<ClickRecorder> logger) : BackgroundService
{
    readonly Channel<ClickEvent> queue = Channel.CreateBounded<ClickEvent>(
        new BoundedChannelOptions(100_000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    readonly DatabaseReader? geoIp = OpenGeoIp(options.GeoIpDatabasePath, logger);

    public void Record(int linkId, HttpContext http) =>
        queue.Writer.TryWrite(new ClickEvent(
            linkId, DateTime.UtcNow, http.ClientIp(), http.Request.Headers.Referer.ToString(), http.Request.Headers.UserAgent.ToString()));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await queue.Reader.WaitToReadAsync(stoppingToken))
            {
                await Task.Delay(options.ClickFlushMilliseconds, stoppingToken); // let a batch build up
                await FlushAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
        await FlushAsync(); // write whatever is left on shutdown
    }

    async Task FlushAsync()
    {
        var batch = new List<ClickEvent>();
        while (queue.Reader.TryRead(out var click))
            batch.Add(options.AdvancedAnalytics ? Enrich(click) : click);
        if (batch.Count == 0)
            return;

        try
        {
            await clicks.SaveAsync(batch, details: options.AdvancedAnalytics);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not save {Count} clicks", batch.Count);
        }
    }

    ClickEvent Enrich(ClickEvent click) => click with
    {
        Country = Country(click.Ip),
        Referer = Truncate(click.Referer),
        RefererHost = Uri.TryCreate(click.Referer, UriKind.Absolute, out var uri) ? Truncate(uri.Host) : null,
        UserAgent = string.IsNullOrEmpty(click.UserAgent) ? null : click.UserAgent,
    };

    string? Country(string ip) =>
        geoIp is not null && IPAddress.TryParse(ip, out var address) && geoIp.TryCountry(address, out var response)
            ? response.Country.IsoCode
            : null;

    static string? Truncate(string? value) => string.IsNullOrEmpty(value) ? null : value.Length <= 255 ? value : value[..255];

    static DatabaseReader? OpenGeoIp(string? path, ILogger logger)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        if (File.Exists(path))
            return new DatabaseReader(path);
        logger.LogWarning("GeoIP database {Path} not found, so countries will not be recorded", path);
        return null;
    }

    public override void Dispose()
    {
        geoIp?.Dispose();
        base.Dispose();
    }
}
