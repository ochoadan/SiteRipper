using System.Text.Json;
using Microsoft.Playwright;
using SiteRipper.Models;
using SiteRipper.Orchestration;
using SiteRipper.Output;
using SiteRipper.Crawling;
using SiteRipper.Aggregation;
using SiteRipper.Utilities;

// ═══════════════════════════════════════════════════════════════════════════════
// SITE RIPPER v6.0 - AI-Ready Component Extraction
// ═══════════════════════════════════════════════════════════════════════════════

await EnsurePlaywrightInstalled();

// CLI args
var url = args.Length > 0 && !args[0].StartsWith("-") ? args[0] : null;
if (url != null && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    url = "https://" + url;

var outputDir = GetArg(args, "-o", "--output") ?? "./output";
var noScreenshots = args.Contains("--no-screenshots");
var maxPages = int.TryParse(GetArg(args, "--max-pages"), out var mp) ? mp : 10;
var maxDepth = int.TryParse(GetArg(args, "--depth"), out var md) ? md : 1;
var delayMs = int.TryParse(GetArg(args, "--delay"), out var dl) ? dl : 500;
var singlePageMode = maxPages == 1 || args.Contains("--single");

if (url == null)
{
    Console.WriteLine("SiteRipper v6.0 - AI-Ready Component Extraction");
    Console.WriteLine();
    Console.WriteLine("Usage: SiteRipper <url> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -o, --output <dir>      Output directory (default: ./output)");
    Console.WriteLine("  --max-pages <n>         Maximum pages to crawl (default: 10)");
    Console.WriteLine("  --depth <n>             Crawl depth (default: 1)");
    Console.WriteLine("                          0 = seed URL only");
    Console.WriteLine("                          1 = seed + direct links");
    Console.WriteLine("                          2 = seed + links + links from those");
    Console.WriteLine("  --delay <ms>            Delay between page loads (default: 500)");
    Console.WriteLine("  --single                Force single-page mode (v3.0 behavior)");
    Console.WriteLine("  --no-screenshots        Skip screenshot capture");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  SiteRipper https://example.com                    # Analyze up to 10 pages");
    Console.WriteLine("  SiteRipper https://example.com --max-pages 5      # Analyze up to 5 pages");
    Console.WriteLine("  SiteRipper https://example.com --depth 2          # Crawl 2 levels deep");
    Console.WriteLine("  SiteRipper https://example.com --single           # Single page only");
    return;
}

Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
Console.WriteLine(" SITE RIPPER v6.0 - AI-Ready Component Extraction");
Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
Console.WriteLine($"Target: {url}");
Console.WriteLine($"Output: {outputDir}");
Console.WriteLine($"Mode: {(singlePageMode ? "Single Page" : $"Multi-Page (max {maxPages} pages, depth {maxDepth})")}");
Console.WriteLine();

Directory.CreateDirectory(outputDir);
Directory.CreateDirectory(Path.Combine(outputDir, "screenshots"));

// Initialize Playwright
Console.WriteLine("Initializing browser...");
var playwright = await Playwright.CreateAsync();
var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });

try
{
    if (singlePageMode)
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // SINGLE PAGE MODE (v3.0 behavior)
        // ═══════════════════════════════════════════════════════════════════════════
        await RunSinglePageAnalysis(context, url, outputDir, noScreenshots);
    }
    else
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // MULTI-PAGE MODE (v4.0)
        // ═══════════════════════════════════════════════════════════════════════════
        await RunMultiPageAnalysis(context, url, outputDir, noScreenshots, maxPages, maxDepth, delayMs);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
finally
{
    await browser.CloseAsync();
}

Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
Console.WriteLine($" Done! Output written to: {outputDir}");
Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");

// ═══════════════════════════════════════════════════════════════════════════════
// SINGLE PAGE MODE
// ═══════════════════════════════════════════════════════════════════════════════

async Task RunSinglePageAnalysis(IBrowserContext ctx, string targetUrl, string outDir, bool skipScreenshots)
{
    var page = await ctx.NewPageAsync();

    Console.WriteLine($"Loading {targetUrl}...");
    await page.GotoAsync(targetUrl, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });
    await page.WaitForTimeoutAsync(1000);

    await PageHelpers.DismissPopups(page);
    await PageHelpers.TriggerLazyLoad(page);

    var html = await page.ContentAsync();
    var title = await page.TitleAsync();
    Console.WriteLine($"Page title: {title}");

    var elCount = await page.EvaluateAsync<int>("() => document.querySelectorAll('*').length");
    Console.WriteLine($"DOM elements: {elCount}");

    if (!skipScreenshots)
    {
        await CaptureResponsiveScreenshots(page, Path.Combine(outDir, "screenshots"));
        await page.SetViewportSizeAsync(1920, 1080);
    }

    Console.WriteLine("Extracting computed styles...");
    var styles = await GetComputedStyles(page);
    Console.WriteLine($"  Got styles for {styles.Count} elements");

    Console.WriteLine("Extracting bounding boxes...");
    var boxes = await GetBoundingBoxes(page);
    Console.WriteLine($"  Got boxes for {boxes.Count} elements");

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
    Console.WriteLine(" INTELLIGENT ANALYSIS");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");

    var detector = new IntelligentComponentDetector();
    var result = await detector.AnalyzeAsync(page, html, targetUrl, styles, boxes);

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
    Console.WriteLine(" GENERATING OUTPUT");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");

    var outputManager = new OutputManager();
    await outputManager.WriteAllAsync(result, outDir);

    await page.CloseAsync();
}

// ═══════════════════════════════════════════════════════════════════════════════
// MULTI-PAGE MODE
// ═══════════════════════════════════════════════════════════════════════════════

async Task RunMultiPageAnalysis(IBrowserContext ctx, string seedUrl, string outDir, bool skipScreenshots,
    int maxPagesLimit, int depthLimit, int delay)
{
    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
    Console.WriteLine(" MULTI-PAGE CRAWL");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");

    var crawler = new SiteCrawler
    {
        MaxPages = maxPagesLimit,
        MaxDepth = depthLimit,
        DelayMs = delay,
        SameDomainOnly = true
    };

    var crawlResult = await crawler.CrawlAsync(
        ctx,
        seedUrl,
        async (page) =>
        {
            var html = await page.ContentAsync();
            var styles = await GetComputedStyles(page);
            var boxes = await GetBoundingBoxes(page);
            return (html, styles, boxes);
        },
        (pageUrl, current, total) =>
        {
            Console.WriteLine($"  [{current}/{total}] Crawling: {pageUrl}");
        },
        (pageUrl, completed) =>
        {
            Console.WriteLine($"         Completed ({completed} pages done)");
        }
    );

    Console.WriteLine();
    Console.WriteLine($"Crawl complete: {crawlResult.SuccessCount} pages analyzed, {crawlResult.FailureCount} failed");
    Console.WriteLine($"Duration: {crawlResult.Duration.TotalSeconds:F1}s");

    // Capture screenshots for all pages
    if (!skipScreenshots && crawlResult.PageResults.Any(p => p.Success))
    {
        Console.WriteLine();
        Console.WriteLine("Capturing screenshots for all pages...");

        var successfulPages = crawlResult.PageResults.Where(p => p.Success).ToList();
        var screenshotPage = await ctx.NewPageAsync();

        for (int i = 0; i < successfulPages.Count; i++)
        {
            var pageResult = successfulPages[i];
            var pageName = PageHelpers.GetSafePageName(pageResult.Url);
            var pageScreenshotDir = Path.Combine(outDir, "pages", pageName, "screenshots");

            Console.WriteLine($"  [{i + 1}/{successfulPages.Count}] {pageName}");

            try
            {
                await screenshotPage.GotoAsync(pageResult.Url, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });
                await CaptureResponsiveScreenshots(screenshotPage, pageScreenshotDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Warning: Could not capture screenshots - {ex.Message}");
            }
        }

        await screenshotPage.CloseAsync();
    }

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
    Console.WriteLine(" AGGREGATING RESULTS");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");

    var aggregator = new ResultAggregator();
    var aggregatedResult = aggregator.Aggregate(crawlResult);

    Console.WriteLine($"  Unique components: {aggregatedResult.Summary.TotalUniqueComponents}");
    Console.WriteLine($"  CSS variables: {aggregatedResult.Summary.TotalCssVariables}");
    Console.WriteLine($"  Colors: {aggregatedResult.Summary.TotalColors}");
    Console.WriteLine($"  Font families: {aggregatedResult.Summary.TotalFontFamilies}");
    Console.WriteLine($"  Avg a11y score: {aggregatedResult.Summary.AverageAccessibilityScore:F0}/100");

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
    Console.WriteLine(" GENERATING OUTPUT");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");

    var outputManager = new OutputManager();
    await outputManager.WriteMultiPageAsync(crawlResult, aggregatedResult, outDir);
}

// ═══════════════════════════════════════════════════════════════════════════════
// HELPER FUNCTIONS
// ═══════════════════════════════════════════════════════════════════════════════

async Task CaptureResponsiveScreenshots(IPage page, string screenshotDir)
{
    Directory.CreateDirectory(screenshotDir);

    var viewports = new (int Width, int Height, string Name)[]
    {
        (375, 812, "mobile"),
        (768, 1024, "tablet"),
        (1440, 900, "desktop"),
        (1920, 1080, "desktop-xl")
    };

    foreach (var (width, height, name) in viewports)
    {
        await page.SetViewportSizeAsync(width, height);
        await page.WaitForTimeoutAsync(300);
        var screenshotPath = Path.Combine(screenshotDir, $"{name}.png");
        await page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
        Console.WriteLine($"  {name}: {screenshotPath}");
    }
}

async Task<Dictionary<int, Dictionary<string, string>>> GetComputedStyles(IPage page)
{
    var json = await page.EvaluateAsync<string>(@"() => {
        const props = [
            // Colors & backgrounds
            'color','background-color','background-image',
            // Typography
            'font-size','font-family','font-weight','line-height','letter-spacing',
            'text-align','text-decoration','text-transform','white-space','vertical-align','text-shadow','text-overflow',
            // Spacing
            'padding','margin','border-radius','border','box-shadow',
            // Layout
            'display','gap','position','z-index',
            'flex-direction','flex-wrap','flex-grow','flex-shrink','flex-basis',
            'justify-content','align-items','align-content','align-self','order',
            'grid-template-columns','grid-template-rows',
            // Size constraints
            'width','height','max-width','min-width','max-height','min-height',
            // Overflow
            'overflow','overflow-x','overflow-y',
            // Position offsets
            'top','left','right','bottom',
            // Animation
            'transition','animation','opacity','transform',
            // Other
            'cursor','visibility','object-fit','aspect-ratio'
        ];
        const result = {};
        const els = document.querySelectorAll('*');
        els.forEach((el, i) => {
            try {
                const cs = getComputedStyle(el);
                const s = {};
                props.forEach(p => {
                    const v = cs.getPropertyValue(p);
                    if (v && v !== 'none' && v !== 'normal' && v !== '0px' &&
                        v !== 'rgba(0, 0, 0, 0)' && v !== 'transparent' && v !== 'auto')
                        s[p] = v;
                });
                if (Object.keys(s).length > 0) result[String(i)] = s;
            } catch {}
        });
        return JSON.stringify(result);
    }");

    if (string.IsNullOrEmpty(json)) return new();
    var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json) ?? new();
    return raw.ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);
}

async Task<Dictionary<int, BoundingBox>> GetBoundingBoxes(IPage page)
{
    var json = await page.EvaluateAsync<string>(@"() => {
        const result = {};
        const els = document.querySelectorAll('*');
        els.forEach((el, i) => {
            const r = el.getBoundingClientRect();
            if (r.width > 0 && r.height > 0)
                result[String(i)] = [r.x, r.y, r.width, r.height];
        });
        return JSON.stringify(result);
    }");

    if (string.IsNullOrEmpty(json)) return new();
    var raw = JsonSerializer.Deserialize<Dictionary<string, double[]>>(json) ?? new();
    return raw.ToDictionary(
        kv => int.Parse(kv.Key),
        kv => new BoundingBox { X = kv.Value[0], Y = kv.Value[1], Width = kv.Value[2], Height = kv.Value[3] }
    );
}

string? GetArg(string[] args, params string[] names)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (names.Contains(args[i])) return args[i + 1];
    return null;
}

Task EnsurePlaywrightInstalled()
{
    var playwrightPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");
    if (Directory.Exists(playwrightPath) &&
        Directory.GetDirectories(playwrightPath).Any(d => Path.GetFileName(d).StartsWith("chromium", StringComparison.OrdinalIgnoreCase)))
        return Task.CompletedTask;

    Console.WriteLine("Installing Playwright Chromium...");
    var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
    if (exitCode != 0) throw new Exception("Failed to install Playwright browsers");
    Console.WriteLine("Done.\n");
    return Task.CompletedTask;
}
