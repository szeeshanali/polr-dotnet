using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Polr.Data;
using Polr.Services;

namespace Polr.Pages.Admin;

[Authorize]
public sealed class StatsModel(LinkRepository links, ClickRepository clicks, UserRepository users, PolrOptions options) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }

    public Link Link { get; private set; } = null!;
    public List<DayStat> Days { get; private set; } = [];
    public List<LabelStat> Countries { get; private set; } = [];
    public List<LabelStat> Referers { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string ending)
    {
        if (!options.AdvancedAnalytics)
            return Fail("Turn on advanced analytics to see link stats.");
        if (await links.FindAsync(ending) is not { } link)
            return Fail("Cannot show stats for a link that does not exist.");
        var me = await users.FindByUsernameAsync(User.Username()!);
        if (link.Creator != me?.Username && me?.IsAdmin != true)
            return Fail("You do not have permission to view stats for this link.");
        Link = link;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = From ??= today.AddDays(-30);
        var to = To ??= today;
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.ToDateTime(new TimeOnly(23, 59, 59));

        var error = to > today ? "The end date cannot be in the future." : ClickRepository.CheckRange(start, end, options.AnalyticsMaxDays);
        if (error is not null)
        {
            TempData["Error"] = error;
            return Page();
        }

        // One point per day, including days without clicks, so the chart shows the real shape.
        var perDay = (await clicks.ByDayAsync(link.Id, start, end)).ToDictionary(d => d.X, d => d.Y);
        Days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
            .Select(i => from.AddDays(i).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Select(day => new DayStat(day, perDay.GetValueOrDefault(day)))
            .ToList();
        Countries = await clicks.ByCountryAsync(link.Id, start, end);
        Referers = await clicks.ByRefererAsync(link.Id, start, end);
        return Page();
    }

    public static string CountryName(string isoCode)
    {
        try
        {
            return new RegionInfo(isoCode).EnglishName;
        }
        catch (ArgumentException)
        {
            return isoCode;
        }
    }

    IActionResult Fail(string message)
    {
        TempData["Error"] = message;
        return RedirectToPage("/Admin/Index");
    }
}
