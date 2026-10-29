using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using PolrNet.Data;

namespace PolrNet.Tests;

[Collection(nameof(PolrNetCollection))]
[Trait("Category", "Integration")]
public sealed class ApiTests(PolrNetFactory factory)
{
    readonly HttpClient client = factory.CreateClientWithoutRedirects();

    static string NewUrl() => $"https://example.com/{Guid.NewGuid():N}";

    static string Ending(string shortUrl) => shortUrl[(PolrNetFactory.BaseUrl.Length + 1)..];

    async Task<string> ShortenAsync(string query)
    {
        var response = await client.GetAsync($"/api/v2/action/shorten?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Shorten_returns_plain_text_by_default()
    {
        var shortUrl = await ShortenAsync($"url={Uri.EscapeDataString(NewUrl())}");
        Assert.StartsWith(PolrNetFactory.BaseUrl + "/", shortUrl);
    }

    [Fact]
    public async Task New_links_get_random_endings() =>
        Assert.Matches("^[A-Za-z0-9]{6}$", Ending(await ShortenAsync($"url={Uri.EscapeDataString(NewUrl())}")));

    [Fact]
    public async Task Sequential_endings_can_be_turned_back_on()
    {
        using var sequential = factory.WithWebHostBuilder(host => host.UseSetting("PolrNet:RandomEndings", "false"));
        var shortUrl = await sequential.CreateClient().GetStringAsync($"/api/v2/action/shorten?url={Uri.EscapeDataString(NewUrl())}");
        Assert.Matches("^[0-9a-v]+$", Ending(shortUrl));
    }

    [Fact]
    public async Task Shorten_returns_json_when_asked()
    {
        var response = await client.PostAsync("/api/v2/action/shorten",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["url"] = NewUrl(), ["response_type"] = "json" }));
        var json = await JsonAsync(response);

        Assert.Equal("shorten", json.GetProperty("action").GetString());
        Assert.StartsWith(PolrNetFactory.BaseUrl, json.GetProperty("result").GetString());
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Shortening_the_same_url_twice_reuses_the_link()
    {
        var url = Uri.EscapeDataString(NewUrl());
        Assert.Equal(await ShortenAsync($"url={url}"), await ShortenAsync($"url={url}"));
    }

    [Fact]
    public async Task Custom_ending_in_use_is_a_creation_error()
    {
        var ending = $"custom-{Guid.NewGuid():N}"[..20];
        await ShortenAsync($"url={Uri.EscapeDataString(NewUrl())}&custom_ending={ending}");

        var response = await client.GetAsync($"/api/v2/action/shorten?url={Uri.EscapeDataString(NewUrl())}&custom_ending={ending}&response_type=json");
        var json = await JsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("CREATION_ERROR", json.GetProperty("error_code").GetString());
        Assert.Equal("This URL ending is already in use.", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Invalid_url_is_rejected()
    {
        var response = await client.GetAsync("/api/v2/action/shorten?url=javascript:alert(1)");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("400 Invalid or missing parameters.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_api_key_is_rejected()
    {
        var response = await client.GetAsync($"/api/v2/action/shorten?key=nope&url={Uri.EscapeDataString(NewUrl())}&response_type=json");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("AUTH_ERROR", (await JsonAsync(response)).GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task Redirect_sends_301_and_counts_the_click()
    {
        var longUrl = NewUrl();
        var ending = Ending(await ShortenAsync($"url={Uri.EscapeDataString(longUrl)}"));

        var response = await client.GetAsync($"/{ending}");
        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal(longUrl, response.Headers.Location!.ToString());

        // Clicks are written in the background; wait for the next flush.
        for (var i = 0; i < 50; i++)
        {
            var lookup = await JsonAsync(await client.GetAsync($"/api/v2/action/lookup?url_ending={ending}&response_type=json"));
            if (lookup.GetProperty("result").GetProperty("clicks").GetInt32() == 1)
                return;
            await Task.Delay(100);
        }
        Assert.Fail("The click was not counted.");
    }

    [Fact]
    public async Task Secret_links_need_their_key()
    {
        var shortUrl = await ShortenAsync($"url={Uri.EscapeDataString(NewUrl())}&is_secret=true");
        var parts = Ending(shortUrl).Split('/');

        Assert.Equal(2, parts.Length);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/{parts[0]}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/{parts[0]}/wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.MovedPermanently, (await client.GetAsync($"/{parts[0]}/{parts[1]}")).StatusCode);
    }

    [Fact]
    public async Task Unknown_link_is_404()
    {
        var response = await client.GetAsync("/does-not-exist-anywhere");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Not found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Disabled_link_is_410()
    {
        var ending = Ending(await ShortenAsync($"url={Uri.EscapeDataString(NewUrl())}"));
        await factory.Services.GetRequiredService<LinkRepository>().ToggleDisabledAsync(ending);

        Assert.Equal(HttpStatusCode.Gone, (await client.GetAsync($"/{ending}")).StatusCode);
    }

    [Fact]
    public async Task Lookup_returns_the_long_url()
    {
        var longUrl = NewUrl();
        var ending = Ending(await ShortenAsync($"url={Uri.EscapeDataString(longUrl)}"));

        var response = await client.GetAsync($"/api/v2/action/lookup?url_ending={ending}");
        Assert.Equal(longUrl, await response.Content.ReadAsStringAsync());

        var json = await JsonAsync(await client.GetAsync($"/api/v2/action/lookup?url_ending={ending}&response_type=json"));
        var result = json.GetProperty("result");
        Assert.Equal(longUrl, result.GetProperty("long_url").GetString());
        Assert.Equal("UTC", result.GetProperty("created_at").GetProperty("timezone").GetString());
    }

    [Fact]
    public async Task Bulk_shorten_accepts_a_json_string()
    {
        var data = JsonSerializer.Serialize(new { links = new object[] { new { url = NewUrl() }, new { url = NewUrl(), is_secret = true } } });
        var response = await client.PostAsync("/api/v2/action/shorten_bulk",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = data }));
        var links = (await JsonAsync(response)).GetProperty("result").GetProperty("shortened_links");

        Assert.Equal(2, links.GetArrayLength());
        Assert.Equal(2, Ending(links[1].GetProperty("short_url").GetString()!).Split('/').Length);
    }

    [Fact]
    public async Task Json_body_arguments_are_accepted()
    {
        var response = await client.PostAsync("/api/v2/action/shorten",
            new StringContent(JsonSerializer.Serialize(new { url = NewUrl(), response_type = "json" }), System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("shorten", (await JsonAsync(response)).GetProperty("action").GetString());
    }

    [Fact]
    public async Task Analytics_need_an_api_key()
    {
        var response = await client.GetAsync("/api/v2/data/link?url_ending=abc&stats_type=day");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analytics_and_quota_work_for_api_users()
    {
        var users = factory.Services.GetRequiredService<UserRepository>();
        var name = $"api{Guid.NewGuid():N}"[..16];
        var key = $"key{Guid.NewGuid():N}";
        var user = await users.CreateAsync(name, $"{name}@polrnet.test", "password123", "127.0.0.1", Roles.Default, active: true, apiKey: key);

        var ending = Ending(await ShortenAsync($"key={key}&url={Uri.EscapeDataString(NewUrl())}"));
        await client.GetAsync($"/{ending}");

        foreach (var type in new[] { "day", "country", "referer" })
        {
            var response = await client.GetAsync($"/api/v2/data/link?key={key}&url_ending={ending}&stats_type={type}&left_bound={DateTime.UtcNow.AddDays(-7):yyyy-MM-dd}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal($"data_link_{type}", (await JsonAsync(response)).GetProperty("action").GetString());
        }

        // With a quota of one link per minute, the link created above uses it up (the quota applies to every API call).
        await users.SetApiQuotaAsync(user.Id, 1);
        var limited = await client.GetAsync($"/api/v2/action/shorten?key={key}&url={Uri.EscapeDataString(NewUrl())}");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Link_availability_check()
    {
        Assert.Equal("invalid", await client.GetStringAsync("/api/v2/link_avail_check?link_ending=a%20b"));
        Assert.Equal("unavailable", await client.GetStringAsync("/api/v2/link_avail_check?link_ending=admin"));
        Assert.Equal("available", await client.GetStringAsync($"/api/v2/link_avail_check?link_ending=free{Guid.NewGuid():N}"[..60]));
    }

    [Fact]
    public async Task Health_check_reaches_the_database() =>
        Assert.Equal("OK", await client.GetStringAsync("/health"));
}
