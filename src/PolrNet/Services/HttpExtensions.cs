using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PolrNet.Data;

namespace PolrNet.Services;

public static class HttpExtensions
{
    /// <summary>The caller's IP. Behind a reverse proxy, set ASPNETCORE_FORWARDEDHEADERS_ENABLED=true.</summary>
    public static string ClientIp(this HttpContext http)
    {
        var ip = http.Connection.RemoteIpAddress;
        return ip is null ? "" : (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();
    }

    public static string? Username(this ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true ? user.Identity.Name : null;

    public static Task SignInAsync(this HttpContext http, User user)
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.Username)];
        if (user.IsAdmin)
            claims.Add(new(ClaimTypes.Role, Roles.Admin));
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(new ClaimsPrincipal(identity));
    }
}
