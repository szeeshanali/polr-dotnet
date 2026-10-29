using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PolrNet.Data;
using PolrNet.Services;

namespace PolrNet.Pages;

public sealed class LoginModel(UserRepository users) : PageModel
{
    [BindProperty] public string? Username { get; set; }
    [BindProperty] public string? Password { get; set; }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        var user = Username is null ? null : await users.FindByUsernameAsync(Username);
        if (user is null || !Crypto.VerifyPassword(Password ?? "", user.PasswordHash))
        {
            TempData["Error"] = "Invalid password or inactivated account. Try again.";
            return Page();
        }

        await HttpContext.SignInAsync(user);
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
