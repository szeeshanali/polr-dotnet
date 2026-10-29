using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using PolrNet;
using PolrNet.Data;
using PolrNet.Endpoints;
using PolrNet.Services;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Settings are read once the host is built, so environment variables and test overrides always apply.
services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IConfiguration>().GetSection("PolrNet").Get<PolrNetOptions>() ?? new PolrNetOptions();
    options.Validate();
    return options;
});
services.AddSingleton(sp => new Db(sp.GetRequiredService<IConfiguration>().GetConnectionString("PolrNet")
    ?? throw new InvalidOperationException("Set ConnectionStrings:PolrNet (environment variable ConnectionStrings__PolrNet).")));

services.AddSingleton<LinkRepository>();
services.AddSingleton<UserRepository>();
services.AddSingleton<ClickRepository>();
services.AddSingleton<LinkService>();
services.AddSingleton<Mailer>();
services.AddSingleton<ClickRecorder>();
services.AddHostedService(sp => sp.GetRequiredService<ClickRecorder>());
services.AddHttpClient<Recaptcha>();
services.AddMemoryCache();
services.AddRazorPages();
services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(cookie =>
{
    cookie.Cookie.Name = "polrnet_auth";
    cookie.LoginPath = "/login";
    cookie.LogoutPath = "/logout";
});

// Behind a load balancer all instances must share these keys, or sign-ins will not work across instances.
if (builder.Configuration["PolrNet:DataProtectionKeysPath"] is { Length: > 0 } keysPath)
    services.AddDataProtection().SetApplicationName("polrnet").PersistKeysToFileSystem(new DirectoryInfo(keysPath));

var app = builder.Build();

await app.Services.GetRequiredService<Db>().MigrateAsync();
await CreateAdminAsync(app.Services);

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/error");
app.UseStatusCodePagesWithReExecute("/error");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapApiV2();
app.MapRedirects();
app.MapGet("/health", async (Db db) =>
{
    await using var connection = db.Connect();
    await connection.ExecuteScalarAsync<int>("SELECT 1");
    return "OK";
});

app.Run();

// Creates the admin account from PolrNet:Admin on first start.
static async Task CreateAdminAsync(IServiceProvider services)
{
    var options = services.GetRequiredService<PolrNetOptions>();
    if (options.Admin is not { Username: { Length: > 0 } username, Password: { Length: > 0 } password })
        return;

    var users = services.GetRequiredService<UserRepository>();
    if (await users.FindByUsernameAsync(username, activeOnly: false) is not null)
        return;

    await users.CreateAsync(username, options.Admin.Email ?? "", password, "127.0.0.1", Roles.Admin, active: true, apiKey: null);
    services.GetRequiredService<ILogger<PolrNetOptions>>().LogInformation("Created admin account {Username}", username);
}
