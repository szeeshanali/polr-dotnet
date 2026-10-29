using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Polr.Data;
using Polr.Services;

namespace Polr.Pages;

public sealed class LostPasswordModel(UserRepository users, Mailer mailer, PolrOptions options) : PageModel
{
    [BindProperty] public string? Email { get; set; }

    public IActionResult OnGet() => options.PasswordRecovery ? Page() : Disabled();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!options.PasswordRecovery)
            return Disabled();

        if (Email is not null && await users.FindByEmailAsync(Email) is { } user)
            await mailer.SendPasswordResetAsync(user, await users.NewRecoveryKeyAsync(user.Id));

        // The same answer either way, so this form cannot be used to find out who has an account.
        TempData["Success"] = "If that email belongs to an account, a reset link is on its way.";
        return RedirectToPage("/Login");
    }

    IActionResult Disabled()
    {
        TempData["Error"] = "Password recovery is disabled.";
        return RedirectToPage("/Login");
    }
}
