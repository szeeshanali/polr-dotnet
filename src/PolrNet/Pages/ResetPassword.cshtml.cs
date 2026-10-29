using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PolrNet.Data;
using PolrNet.Services;

namespace PolrNet.Pages;

public sealed class ResetPasswordModel(UserRepository users) : PageModel
{
    [BindProperty] public string? NewPassword { get; set; }

    public async Task<IActionResult> OnGetAsync(string username, string key) =>
        await FindUserAsync(username, key) is null ? Invalid() : Page();

    public async Task<IActionResult> OnPostAsync(string username, string key)
    {
        if (await FindUserAsync(username, key) is not { } user)
            return Invalid();

        if (!Rules.IsValidPassword(NewPassword))
        {
            TempData["Error"] = Rules.PasswordHint;
            return Page();
        }

        await users.SetPasswordAsync(user.Id, NewPassword);
        TempData["Success"] = "Password reset. You may now sign in.";
        return RedirectToPage("/Login");
    }

    async Task<User?> FindUserAsync(string username, string key)
    {
        var user = await users.FindByUsernameAsync(username);
        return user is not null && Crypto.SecureEquals(key, user.RecoveryKey) ? user : null;
    }

    IActionResult Invalid()
    {
        TempData["Error"] = "Username or reset key incorrect.";
        return RedirectToPage("/Login");
    }
}
