using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PolrNet.Data;
using PolrNet.Services;

namespace PolrNet.Pages;

public sealed class SignupModel(UserRepository users, Mailer mailer, Recaptcha recaptcha, PolrNetOptions options) : PageModel
{
    [BindProperty] public string? Username { get; set; }
    [BindProperty] public string? Email { get; set; }
    [BindProperty] public string? Password { get; set; }

    public IActionResult OnGet() => options.AllowSignup ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!options.AllowSignup)
            return NotFound();

        if (await ValidateAsync() is { } error)
        {
            TempData["Error"] = error;
            return Page();
        }

        var active = !options.RequireEmailActivation;
        var apiKey = options.AutoApiKey ? Crypto.RandomHex(options.ApiKeyBytes) : null;
        var user = await users.CreateAsync(Username!, Email!, Password!, HttpContext.ClientIp(), Roles.Default, active, apiKey);

        if (!active)
            await mailer.SendActivationAsync(user);

        TempData["Success"] = active
            ? "Thanks for signing up! You may now sign in."
            : "Thanks for signing up! Please confirm your email to continue.";
        return RedirectToPage("/Login");
    }

    async Task<string?> ValidateAsync()
    {
        if (options.Recaptcha.Enabled && !await recaptcha.VerifyAsync(Request.Form["g-recaptcha-response"], HttpContext.ClientIp()))
            return "You must complete the reCAPTCHA to register.";
        if (!Rules.IsValidUsername(Username))
            return "Usernames can only contain letters, numbers, hyphens and underscores.";
        if (!Rules.IsValidEmail(Email))
            return "Please enter a valid email address.";
        if (!Rules.IsValidPassword(Password))
            return Rules.PasswordHint;

        var domain = Email[(Email.LastIndexOf('@') + 1)..];
        if (options.AllowedEmailDomains.Length > 0 && !options.AllowedEmailDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            return "Sorry, your email's domain is not permitted to create new accounts.";
        if (await users.ExistsAsync(Username, Email))
            return "Sorry, your email or username already exists. Try again.";
        return null;
    }
}
