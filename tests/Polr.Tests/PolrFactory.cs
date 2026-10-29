using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MySql;

namespace Polr.Tests;

/// <summary>Runs the real app against a throwaway MySQL container (needs Docker).</summary>
public sealed class PolrFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string BaseUrl = "http://polr.test";
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
        builder.UseSetting("ConnectionStrings:Polr", mysql.GetConnectionString());
        builder.UseSetting("Polr:BaseUrl", BaseUrl);
        builder.UseSetting("Polr:AnonymousApi", "true");
        builder.UseSetting("Polr:AnonymousApiQuota", "-1");
        builder.UseSetting("Polr:AdvancedAnalytics", "true");
        builder.UseSetting("Polr:ClickFlushMilliseconds", "50");
        builder.UseSetting("Polr:Admin:Username", "admin");
        builder.UseSetting("Polr:Admin:Email", "admin@polr.test");
        builder.UseSetting("Polr:Admin:Password", AdminPassword);
    }

    public HttpClient CreateClientWithoutRedirects() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}

[CollectionDefinition(nameof(PolrCollection))]
public sealed class PolrCollection : ICollectionFixture<PolrFactory>;
