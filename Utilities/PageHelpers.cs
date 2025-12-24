namespace SiteRipper.Utilities;

using Microsoft.Playwright;

/// <summary>
/// Shared page interaction utilities
/// </summary>
public static class PageHelpers
{
    /// <summary>
    /// Dismiss common popups (cookie banners, modals, etc)
    /// </summary>
    public static async Task DismissPopups(IPage page)
    {
        var selectors = new[] {
            "[class*='cookie'] button", "[class*='consent'] button", "[id*='cookie'] button",
            "[class*='modal'] button[class*='close']", "[class*='popup'] button[class*='close']",
            "button[aria-label*='close']", "button[aria-label*='dismiss']",
            "[class*='banner'] button", ".cc-dismiss", "#onetrust-accept-btn-handler"
        };

        foreach (var sel in selectors)
        {
            try
            {
                var btn = await page.QuerySelectorAsync(sel);
                if (btn != null) await btn.ClickAsync(new() { Timeout = 500 });
            }
            catch { }
        }

        try { await page.Keyboard.PressAsync("Escape"); } catch { }
    }

    /// <summary>
    /// Trigger lazy loading by scrolling through the page
    /// </summary>
    public static async Task TriggerLazyLoad(IPage page, int waitMs = 500)
    {
        await page.EvaluateAsync(@"async () => {
            const h = document.body.scrollHeight;
            for (let y = 0; y < h; y += 500) {
                window.scrollTo(0, y);
                await new Promise(r => setTimeout(r, 100));
            }
            window.scrollTo(0, 0);
        }");
        await page.WaitForTimeoutAsync(waitMs);
    }

    /// <summary>
    /// Convert URL to safe filesystem name
    /// </summary>
    public static string GetSafePageName(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath.Trim('/');

            if (string.IsNullOrEmpty(path))
                return "home";

            var safeName = path
                .Replace("/", "-")
                .Replace("\\", "-")
                .Replace("?", "")
                .Replace("&", "")
                .Replace("=", "-")
                .Replace("%", "")
                .Replace("#", "");

            if (safeName.Length > 50)
                safeName = safeName[..50];

            return safeName;
        }
        catch
        {
            return $"page-{Guid.NewGuid().ToString()[..8]}";
        }
    }
}
