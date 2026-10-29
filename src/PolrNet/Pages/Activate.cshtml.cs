using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PolrNet.Data;
using PolrNet.Services;

namespace PolrNet.Pages;

/// <summary>The link from the sign-up email.</summary>
public sealed class ActivateModel(UserRepository users) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string username, string key)
    {
        var user = await users.FindByUsernameAsync(username, activeOnly: false);
        if (user is null || !Crypto.SecureEquals(key, user.RecoveryKey))
        {
            TempData["Error"] = "Username or activation key incorrect.";
            return RedirectToPage("/Login");
        }

        await users.ActivateAsync(user.Id);
        TempData["Success"] = "Account activated. You may now sign in.";
        return RedirectToPage("/Login");
    }
}
