using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PolrNet.Data;
using PolrNet.Services;

namespace PolrNet.Pages.Admin;

/// <summary>The dashboard: your own links for everyone, all links and users for admins. Every action is a plain form post.</summary>
[Authorize]
public sealed class IndexModel(UserRepository users, LinkRepository linkRepository, LinkService links, PolrNetOptions options) : PageModel
{
    const int PageSize = 25;

    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "links";
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public User Me { get; private set; } = null!;
    public PagedList<Link>? Links { get; private set; }
    public PagedList<User>? Users { get; private set; }

    public int PageCount => Links?.PageCount ?? Users?.PageCount ?? 1;

    /// <summary>Added to every form's URL so an action returns to the same tab, search and page.</summary>
    public Dictionary<string, string> State => new() { ["tab"] = Tab, ["q"] = Q ?? "", ["p"] = PageNumber.ToString() };

    public string PageLink(int page) => $"?tab={Tab}&q={Uri.EscapeDataString(Q ?? "")}&p={page}";

    public string ShortUrl(Link link) => links.ShortUrl(link.ShortUrl, link.SecretKey);

    public async Task<IActionResult> OnGetAsync()
    {
        if (await LoadUserAsync() is { } signedOut)
            return signedOut;

        var allowed = Tab is "links" or "settings" or "api" || (Me.IsAdmin && Tab is "all-links" or "users");
        if (!allowed)
            Tab = "links";

        if (Tab == "links")
            Links = await linkRepository.ListAsync(Me.Username, Q, PageNumber, PageSize);
        else if (Tab == "all-links")
            Links = await linkRepository.ListAsync(null, Q, PageNumber, PageSize);
        else if (Tab == "users")
            Users = await users.ListAsync(Q, PageNumber, PageSize);
        return Page();
    }

    public Task<IActionResult> OnPostEditLinkAsync(string ending, string? longUrl) => RunAsync(adminOnly: false, async () =>
    {
        var link = await linkRepository.FindAsync(ending);
        if (link is null)
            return "Link not found.";
        if (link.Creator != Me.Username && !Me.IsAdmin)
            return "You can only edit your own links.";
        await links.UpdateLongUrlAsync(ending, longUrl);
        return null;
    });

    public Task<IActionResult> OnPostToggleLinkAsync(string ending) => RunAsync(adminOnly: true, async () =>
        await links.ToggleDisabledAsync(ending) ? null : "Link not found.");

    public Task<IActionResult> OnPostDeleteLinkAsync(string ending) => RunAsync(adminOnly: true, async () =>
        await links.DeleteAsync(ending) ? null : "Link not found.");

    public Task<IActionResult> OnPostAddUserAsync(string? username, string? email, string? password, string? role) => RunAsync(adminOnly: true, async () =>
    {
        if (!Rules.IsValidUsername(username))
            return "Usernames can only contain letters, numbers, hyphens and underscores.";
        if (!Rules.IsValidEmail(email))
            return "Please enter a valid email address.";
        if (!Rules.IsValidPassword(password))
            return Rules.PasswordHint;
        if (await users.ExistsAsync(username, email))
            return "That username or email is already taken.";

        await users.CreateAsync(username, email, password, HttpContext.ClientIp(), role == Roles.Admin ? Roles.Admin : Roles.Default, active: true, apiKey: null);
        TempData["Success"] = $"Created user {username}.";
        return null;
    });

    public Task<IActionResult> OnPostToggleUserAsync(int id) => RunAsync(adminOnly: true, async () =>
        id == Me.Id ? "You cannot deactivate your own account." : await users.ToggleActiveAsync(id) ? null : "User not found.");

    public Task<IActionResult> OnPostSetRoleAsync(int id, string? role) => RunAsync(adminOnly: true, async () =>
        id == Me.Id ? "You cannot change your own role."
        : await users.SetRoleAsync(id, role == Roles.Admin ? Roles.Admin : Roles.Default) ? null : "User not found.");

    public Task<IActionResult> OnPostDeleteUserAsync(int id) => RunAsync(adminOnly: true, async () =>
        id == Me.Id ? "You cannot delete your own account." : await users.DeleteAsync(id) ? null : "User not found.");

    public Task<IActionResult> OnPostToggleApiAsync(int id) => RunAsync(adminOnly: true, async () =>
        await users.ToggleApiAsync(id) ? null : "User not found.");

    public Task<IActionResult> OnPostSetQuotaAsync(int id, int quota) => RunAsync(adminOnly: true, async () =>
        await users.SetApiQuotaAsync(id, quota) ? null : "User not found.");

    // Users may renew their own key while their API access is on. Admins may renew anyone's.
    public Task<IActionResult> OnPostNewApiKeyAsync(int id) => RunAsync(adminOnly: false, async () =>
    {
        if (!Me.IsAdmin && (id != Me.Id || !Me.ApiActive))
            return "You are not allowed to do that.";
        return await users.SetApiKeyAsync(id, Crypto.RandomHex(options.ApiKeyBytes)) ? null : "User not found.";
    });

    public Task<IActionResult> OnPostChangePasswordAsync(string? currentPassword, string? newPassword) => RunAsync(adminOnly: false, async () =>
    {
        if (!Crypto.VerifyPassword(currentPassword ?? "", Me.PasswordHash))
            return "Current password invalid. Try again.";
        if (!Rules.IsValidPassword(newPassword))
            return Rules.PasswordHint;

        await users.SetPasswordAsync(Me.Id, newPassword);
        TempData["Success"] = "Password changed successfully.";
        return null;
    });

    // Loads the signed-in user, checks permissions, runs the action (which returns an error message or null)
    // and goes back to the same tab, search and page.
    async Task<IActionResult> RunAsync(bool adminOnly, Func<Task<string?>> action)
    {
        if (await LoadUserAsync() is { } signedOut)
            return signedOut;
        if (adminOnly && !Me.IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden);

        try
        {
            if (await action() is { } error)
                TempData["Error"] = error;
        }
        catch (LinkException e)
        {
            TempData["Error"] = e.Message;
        }
        return RedirectToPage(new { tab = Tab, q = Q, p = PageNumber });
    }

    // The role and active flag come from the database on every request, so changes apply immediately.
    async Task<IActionResult?> LoadUserAsync()
    {
        if (await users.FindByUsernameAsync(User.Username()!) is { } me)
        {
            Me = me;
            return null;
        }

        await HttpContext.SignOutAsync();
        TempData["Error"] = "Invalid or disabled account.";
        return RedirectToPage("/Login");
    }
}
