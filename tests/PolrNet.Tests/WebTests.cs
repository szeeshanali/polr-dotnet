using System.Net;
using System.Text.RegularExpressions;

namespace PolrNet.Tests;

[Collection(nameof(PolrNetCollection))]
[Trait("Category", "Integration")]
public sealed partial class WebTests(PolrNetFactory factory)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();

    static async Task<string> TokenAsync(HttpClient client, string path) =>
        TokenRegex().Match(await client.GetStringAsync(path)).Groups[1].Value;

    [Fact]
    public async Task Home_page_shows_the_shorten_form()
    {
        var html = await factory.CreateClient().GetStringAsync("/");
        Assert.Contains("Paste a long URL", html);
    }

    [Fact]
    public async Task Shortening_from_the_home_page_shows_the_short_link()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsync("/", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["LongUrl"] = $"https://example.com/{Guid.NewGuid():N}",
            ["__RequestVerificationToken"] = await TokenAsync(client, "/"),
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"value=\"{PolrNetFactory.BaseUrl}/", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_can_sign_in_and_see_the_dashboard()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = "admin",
            ["Password"] = PolrNetFactory.AdminPassword,
            ["__RequestVerificationToken"] = await TokenAsync(client, "/login"),
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dashboard = await client.GetStringAsync("/admin?tab=users");
        Assert.Contains("Signed in as admin", dashboard);
        Assert.Contains("admin@polrnet.test", dashboard);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = "admin",
            ["Password"] = "wrong",
            ["__RequestVerificationToken"] = await TokenAsync(client, "/login"),
        }));
        Assert.Contains("Invalid password", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Dashboard_needs_a_sign_in()
    {
        var response = await factory.CreateClientWithoutRedirects().GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("http://localhost/login", response.Headers.Location!.ToString());
    }
}
