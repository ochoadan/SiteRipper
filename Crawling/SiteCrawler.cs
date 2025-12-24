namespace SiteRipper.Crawling;

using Microsoft.Playwright;
using SiteRipper.Orchestration;
using SiteRipper.Structure;
using SiteRipper.Models;
using SiteRipper.Utilities;

public class SiteCrawler
{
    private readonly IntelligentComponentDetector _detector = new();
    private readonly HashSet<string> _visited = new();
    private readonly Queue<CrawlItem> _queue = new();

    public int MaxPages { get; set; } = 10;
    public int MaxDepth { get; set; } = 1;
    public bool SameDomainOnly { get; set; } = true;
    public int DelayMs { get; set; } = 500;

    public async Task<SiteCrawlResult> CrawlAsync(
        IBrowserContext context,
        string seedUrl,
        Func<IPage, Task<(string html, Dictionary<int, Dictionary<string, string>> styles, Dictionary<int, BoundingBox> boxes)>> pageDataExtractor,
        Action<string, int, int>? onPageStart = null,
        Action<string, int>? onPageComplete = null)
    {
        var result = new SiteCrawlResult
        {
            SeedUrl = seedUrl,
            MaxPages = MaxPages,
            MaxDepth = MaxDepth,
            StartedAt = DateTime.UtcNow
        };

        var seedUri = new Uri(seedUrl);
        var baseDomain = seedUri.Host;

        _queue.Enqueue(new CrawlItem { Url = seedUrl, Depth = 0 });

        while (_queue.Count > 0 && result.PageResults.Count < MaxPages)
        {
            var current = _queue.Dequeue();
            var normalizedUrl = NormalizeUrl(current.Url);

            if (_visited.Contains(normalizedUrl))
                continue;

            _visited.Add(normalizedUrl);

            onPageStart?.Invoke(current.Url, result.PageResults.Count + 1, MaxPages);

            try
            {
                var page = await context.NewPageAsync();
                try
                {
                    await page.SetViewportSizeAsync(1920, 1080);
                    await page.GotoAsync(current.Url, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });
                    await page.WaitForTimeoutAsync(500);

                    // Dismiss popups
                    await PageHelpers.DismissPopups(page);

                    // Trigger lazy loading
                    await PageHelpers.TriggerLazyLoad(page);

                    // Extract page data
                    var (html, styles, boxes) = await pageDataExtractor(page);
                    var title = await page.TitleAsync();

                    // Run analysis
                    var analysisResult = await _detector.AnalyzeAsync(page, html, current.Url, styles, boxes);

                    var pageResult = new PageCrawlResult
                    {
                        Url = current.Url,
                        NormalizedUrl = normalizedUrl,
                        Title = title,
                        Depth = current.Depth,
                        AnalysisResult = analysisResult,
                        CrawledAt = DateTime.UtcNow,
                        Success = true
                    };

                    result.PageResults.Add(pageResult);
                    onPageComplete?.Invoke(current.Url, result.PageResults.Count);

                    // Discover new URLs if within depth limit
                    if (current.Depth < MaxDepth && analysisResult.Structure?.InternalLinks != null)
                    {
                        foreach (var link in analysisResult.Structure.InternalLinks)
                        {
                            var linkUrl = ResolveUrl(link.Url, seedUri);
                            if (string.IsNullOrEmpty(linkUrl)) continue;

                            var linkUri = new Uri(linkUrl);

                            // Filter by domain
                            if (SameDomainOnly && linkUri.Host != baseDomain)
                                continue;

                            var normalizedLink = NormalizeUrl(linkUrl);
                            if (!_visited.Contains(normalizedLink))
                            {
                                _queue.Enqueue(new CrawlItem { Url = linkUrl, Depth = current.Depth + 1 });
                            }
                        }
                    }
                }
                finally
                {
                    await page.CloseAsync();
                }

                // Rate limiting
                if (DelayMs > 0 && _queue.Count > 0)
                    await Task.Delay(DelayMs);
            }
            catch (Exception ex)
            {
                result.PageResults.Add(new PageCrawlResult
                {
                    Url = current.Url,
                    NormalizedUrl = normalizedUrl,
                    Depth = current.Depth,
                    Success = false,
                    Error = ex.Message,
                    CrawledAt = DateTime.UtcNow
                });
            }
        }

        result.CompletedAt = DateTime.UtcNow;
        return result;
    }

    private string NormalizeUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            // Remove trailing slash, fragment, sort query params
            var normalized = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath.TrimEnd('/')}";
            if (!string.IsNullOrEmpty(uri.Query))
            {
                var queryParams = uri.Query.TrimStart('?')
                    .Split('&')
                    .Where(p => !string.IsNullOrEmpty(p))
                    .OrderBy(p => p)
                    .ToList();
                if (queryParams.Count > 0)
                    normalized += "?" + string.Join("&", queryParams);
            }
            return normalized.ToLower();
        }
        catch
        {
            return url.ToLower();
        }
    }

    private string? ResolveUrl(string url, Uri baseUri)
    {
        if (string.IsNullOrEmpty(url)) return null;
        if (url.StartsWith("#")) return null; // Anchor link
        if (url.StartsWith("javascript:")) return null;
        if (url.StartsWith("mailto:")) return null;
        if (url.StartsWith("tel:")) return null;

        try
        {
            if (url.StartsWith("//"))
                return $"https:{url}";
            if (url.StartsWith("/"))
                return new Uri(baseUri, url).ToString();
            if (!url.StartsWith("http"))
                return new Uri(baseUri, url).ToString();
            return url;
        }
        catch
        {
            return null;
        }
    }

    private class CrawlItem
    {
        public string Url { get; set; } = "";
        public int Depth { get; set; }
    }
}

public class SiteCrawlResult
{
    public string SeedUrl { get; set; } = "";
    public int MaxPages { get; set; }
    public int MaxDepth { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<PageCrawlResult> PageResults { get; set; } = new();

    public int SuccessCount => PageResults.Count(p => p.Success);
    public int FailureCount => PageResults.Count(p => !p.Success);
    public TimeSpan Duration => CompletedAt - StartedAt;
}

public class PageCrawlResult
{
    public string Url { get; set; } = "";
    public string NormalizedUrl { get; set; } = "";
    public string Title { get; set; } = "";
    public int Depth { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime CrawledAt { get; set; }
    public ComponentAnalysisResult? AnalysisResult { get; set; }
}
