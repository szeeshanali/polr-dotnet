using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MySql;

namespace PolrNet.Tests;

/// <summary>Runs the real app against a throwaway MySQL container (needs Docker).</summary>
public sealed class PolrNetFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string BaseUrl = "http://polrnet.test";
    public const string AdminPassword = "admin-password";

    readonly MySqlContainer mysql = new MySqlBuilder("mysql:8.4").Build();

    public Task InitializeAsync() => mysql.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await mysql.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:PolrNet", mysql.GetConnectionString());
        builder.UseSetting("PolrNet:BaseUrl", BaseUrl);
        builder.UseSetting("PolrNet:AnonymousApi", "true");
        builder.UseSetting("PolrNet:AnonymousApiQuota", "-1");
        builder.UseSetting("PolrNet:AdvancedAnalytics", "true");
        builder.UseSetting("PolrNet:ClickFlushMilliseconds", "50");
        builder.UseSetting("PolrNet:Admin:Username", "admin");
        builder.UseSetting("PolrNet:Admin:Email", "admin@polrnet.test");
        builder.UseSetting("PolrNet:Admin:Password", AdminPassword);
    }

    public HttpClient CreateClientWithoutRedirects() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}

[CollectionDefinition(nameof(PolrNetCollection))]
public sealed class PolrNetCollection : ICollectionFixture<PolrNetFactory>;
