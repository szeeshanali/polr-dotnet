using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Polr.Services;

namespace Polr.Pages;

public sealed class IndexModel(LinkService links, PolrOptions options) : PageModel
{
    [BindProperty] public string? LongUrl { get; set; }
    [BindProperty] public string? CustomEnding { get; set; }
    [BindProperty] public bool Secret { get; set; }

    public string? ShortUrl { get; private set; }

    public IActionResult OnGet() => CanUse() ? Page() : SendAway();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!CanUse())
            return SendAway();

        var username = User.Username();
        if (options.ShortenRequiresLogin && username is null)
        {
            TempData["Error"] = "You must be logged in to shorten links.";
            return Page();
        }

        try
        {
            ShortUrl = await links.ShortenAsync(LongUrl, Secret, CustomEnding, HttpContext.ClientIp(), username ?? "", isApi: false);
        }
        catch (LinkException e)
        {
            TempData["Error"] = e.Message;
        }
        return Page();
    }

    bool CanUse() => options.PublicInterface || User.Username() is not null;

    IActionResult SendAway() => string.IsNullOrEmpty(options.IndexRedirect) ? RedirectToPage("/Login") : Redirect(options.IndexRedirect);
}
