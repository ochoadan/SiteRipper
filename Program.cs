using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using AngleSharp;
using AngleSharp.Dom;

// ═══════════════════════════════════════════════════════════════════════════════
// SITE RIPPER v2 - Clean architecture with AngleSharp + Playwright
// ═══════════════════════════════════════════════════════════════════════════════

await EnsurePlaywrightInstalled();

// CLI args
var url = args.Length > 0 ? args[0] : null;
if (url != null && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    url = "https://" + url;

var outputDir = GetArg(args, "-o", "--output") ?? "./output";
var maxDepth = int.Parse(GetArg(args, "-d", "--depth") ?? "1");
var noScreenshots = args.Contains("--no-screenshots");

if (url == null)
{
    Console.WriteLine("Usage: SiteRipper <url> [-o output] [-d depth] [--no-screenshots]");
    return;
}

Console.WriteLine($"Ripping: {url}");
Console.WriteLine($"Output: {outputDir}\n");

Directory.CreateDirectory(outputDir);
Directory.CreateDirectory(Path.Combine(outputDir, "screenshots"));

// Initialize
var playwright = await Playwright.CreateAsync();
var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
var angleConfig = Configuration.Default;
var angleContext = BrowsingContext.New(angleConfig);

var blueprint = new SiteBlueprint { Meta = new() { Url = url, AnalyzedAt = DateTime.UtcNow } };
var crawled = new HashSet<string>();
var toCrawl = new Queue<(string url, int depth)>();
toCrawl.Enqueue((url, 0));

// Crawl pages
while (toCrawl.Count > 0)
{
    var (pageUrl, depth) = toCrawl.Dequeue();
    if (crawled.Contains(pageUrl)) continue;
    crawled.Add(pageUrl);

    Console.WriteLine($"[{crawled.Count}] {pageUrl}");

    try
    {
        var pageData = await ProcessPage(pageUrl, crawled.Count);
        blueprint.Pages.Add(pageData);

        // Queue internal links
        if (depth < maxDepth)
        {
            foreach (var link in pageData.InternalLinks.Where(l => !crawled.Contains(l)))
                toCrawl.Enqueue((link, depth + 1));
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Error: {ex.Message}");
    }
}

// Aggregate design system from all pages
AggregateDesignSystem(blueprint);

// Write outputs
WriteOutputs(blueprint, outputDir);

await browser.CloseAsync();
Console.WriteLine($"\nDone! Files written to {outputDir}");

// ═══════════════════════════════════════════════════════════════════════════════
// PROCESS PAGE
// ═══════════════════════════════════════════════════════════════════════════════

async Task<PageData> ProcessPage(string pageUrl, int pageNum)
{
    var page = await context.NewPageAsync();
    var pageData = new PageData { Url = pageUrl };

    try
    {
        // Navigate and wait
        await page.GotoAsync(pageUrl, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });
        await page.WaitForTimeoutAsync(1000);

        // Dismiss popups
        await DismissPopups(page);

        // Scroll to trigger lazy load
        await page.EvaluateAsync(@"async () => {
            const h = document.body.scrollHeight;
            for (let y = 0; y < h; y += 500) {
                window.scrollTo(0, y);
                await new Promise(r => setTimeout(r, 100));
            }
            window.scrollTo(0, 0);
        }");
        await page.WaitForTimeoutAsync(500);

        // Get rendered HTML
        var html = await page.ContentAsync();
        pageData.Title = await page.TitleAsync();

        // Test basic JS eval
        var elCount = await page.EvaluateAsync<int>("() => document.querySelectorAll('*').length");
        Console.WriteLine($"  DOM has {elCount} elements");

        // Get computed styles (single batched call)
        Console.WriteLine("  Getting styles...");
        Dictionary<string, Dictionary<string, string>> styles;
        Dictionary<string, BoundingBox> boxes;
        try {
            // Debug: test simple dict return
            var testDict = await page.EvaluateAsync<Dictionary<string, string>>("() => ({ 'a': 'b', 'c': 'd' })");
            Console.WriteLine($"    Test dict has {testDict?.Count ?? -1} items");

            styles = await GetComputedStyles(page);
            Console.WriteLine($"    Got styles for {styles.Count} elements");
        } catch (Exception ex) {
            Console.WriteLine($"    Styles error: {ex.Message}");
            styles = new();
        }

        // Get bounding boxes
        try {
            boxes = await GetBoundingBoxes(page);
            Console.WriteLine($"    Got boxes for {boxes.Count} elements");
        } catch (Exception ex) {
            Console.WriteLine($"    Boxes error: {ex.Message}");
            boxes = new();
        }

        // Screenshot
        if (!noScreenshots)
        {
            var screenshotPath = Path.Combine(outputDir, "screenshots", $"page-{pageNum}.png");
            await page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
            Console.WriteLine($"  Screenshot: {screenshotPath}");
        }

        // Detect frameworks
        pageData.Frameworks = await DetectFrameworks(page);

        // Parse with AngleSharp
        Console.WriteLine("  Parsing HTML...");
        var doc = await angleContext.OpenAsync(req => req.Content(html));

        // Extract elements
        pageData.Elements = ExtractElements(doc, styles, boxes);
        Console.WriteLine($"    Found {pageData.Elements.Count} elements");

        // Detect sections
        pageData.Sections = DetectSections(doc, boxes);
        Console.WriteLine($"    Found {pageData.Sections.Count} sections");

        // Find components
        pageData.Buttons = FindButtons(doc, styles);
        pageData.Cards = FindCards(doc, styles);
        pageData.Forms = FindForms(doc);
        Console.WriteLine($"    Found {pageData.Buttons.Count} button variants, {pageData.Cards.Count} card patterns");

        // Extract assets
        pageData.Assets = ExtractAssets(doc);

        // Find internal links
        var baseUri = new Uri(pageUrl);
        pageData.InternalLinks = doc.QuerySelectorAll("a[href]")
            .Select(a => a.GetAttribute("href"))
            .Where(h => !string.IsNullOrEmpty(h) && !h.StartsWith("#") && !h.StartsWith("javascript:"))
            .Select(h => {
                try { return new Uri(baseUri, h).ToString(); }
                catch { return null; }
            })
            .Where(u => u != null && new Uri(u).Host == baseUri.Host)
            .Distinct()
            .Take(50)
            .ToList()!;
    }
    finally
    {
        await page.CloseAsync();
    }

    return pageData;
}

// ═══════════════════════════════════════════════════════════════════════════════
// PLAYWRIGHT HELPERS
// ═══════════════════════════════════════════════════════════════════════════════

async Task DismissPopups(IPage page)
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

async Task<Dictionary<string, Dictionary<string, string>>> GetComputedStyles(IPage page)
{
    var json = await page.EvaluateAsync<string>(@"() => {
        const props = ['color','background-color','font-size','font-family','font-weight',
            'padding','margin','border-radius','border','box-shadow','display','gap'];
        const result = {};
        const els = document.querySelectorAll('*');
        els.forEach((el, i) => {
            try {
                const cs = getComputedStyle(el);
                const s = {};
                props.forEach(p => {
                    const v = cs.getPropertyValue(p);
                    if (v && v !== 'none' && v !== 'normal' && v !== '0px' &&
                        v !== 'rgba(0, 0, 0, 0)' && v !== 'transparent')
                        s[p] = v;
                });
                if (Object.keys(s).length > 0) result[String(i)] = s;
            } catch {}
        });
        return JSON.stringify(result);
    }");
    if (string.IsNullOrEmpty(json)) return new();
    return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json) ?? new();
}

async Task<Dictionary<string, BoundingBox>> GetBoundingBoxes(IPage page)
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
        kv => kv.Key,
        kv => new BoundingBox { X = kv.Value[0], Y = kv.Value[1], Width = kv.Value[2], Height = kv.Value[3] }
    );
}

async Task<List<string>> DetectFrameworks(IPage page)
{
    return await page.EvaluateAsync<List<string>>(@"() => {
        const f = [];
        if (window.__NEXT_DATA__) f.push('Next.js');
        if (window.__NUXT__) f.push('Nuxt');
        if (document.querySelector('[data-reactroot], #__next, [id*=react]')) f.push('React');
        if (window.angular || document.querySelector('[ng-version]')) f.push('Angular');
        if (window.__VUE__ || document.querySelector('[data-v-]')) f.push('Vue');
        if (document.querySelector('[data-svelte-h]')) f.push('Svelte');
        if (window.Webflow) f.push('Webflow');
        if (document.querySelector('[data-wf-site]')) f.push('Webflow');
        if (window.Shopify) f.push('Shopify');
        if (document.querySelector('meta[name*=wordpress]')) f.push('WordPress');
        return f;
    }") ?? new();
}

// ═══════════════════════════════════════════════════════════════════════════════
// ANGLESHARP EXTRACTION
// ═══════════════════════════════════════════════════════════════════════════════

List<ElementInfo> ExtractElements(IDocument doc, Dictionary<string, Dictionary<string, string>> styles, Dictionary<string, BoundingBox> boxes)
{
    var elements = new List<ElementInfo>();
    var allEls = doc.QuerySelectorAll("*").ToList();
    var index = 0;

    foreach (var el in allEls)
    {
        var tag = el.TagName.ToLower();
        var indexStr = index.ToString();

        if (tag == "script" || tag == "style" || tag == "noscript" || tag == "meta" || tag == "link")
        {
            index++;
            continue;
        }

        if (!boxes.TryGetValue(indexStr, out var box) || box.Width < 5 || box.Height < 5)
        {
            index++;
            continue;
        }

        var info = new ElementInfo
        {
            Tag = tag,
            Id = el.Id,
            Classes = el.ClassList.ToList(),
            Selector = GetSelector(el),
            Text = GetDirectText(el),
            Box = box
        };

        if (styles.TryGetValue(indexStr, out var s))
            info.Styles = s;

        elements.Add(info);
        index++;
    }

    return elements.Take(1000).ToList(); // Cap for sanity
}

string GetSelector(IElement el)
{
    if (!string.IsNullOrEmpty(el.Id)) return $"#{el.Id}";
    var classes = el.ClassList.Where(c => !c.Contains(":")).Take(2).ToList();
    if (classes.Count > 0) return $"{el.TagName.ToLower()}.{string.Join(".", classes)}";
    return el.TagName.ToLower();
}

string GetDirectText(IElement el)
{
    var text = string.Join(" ", el.ChildNodes
        .Where(n => n.NodeType == NodeType.Text)
        .Select(n => n.TextContent.Trim())
        .Where(t => !string.IsNullOrEmpty(t)));
    return text.Length > 150 ? text[..150] + "..." : text;
}

// ═══════════════════════════════════════════════════════════════════════════════
// SECTION DETECTION
// ═══════════════════════════════════════════════════════════════════════════════

List<Section> DetectSections(IDocument doc, Dictionary<string, BoundingBox> boxes)
{
    var sections = new List<Section>();

    // Navigation
    var nav = doc.QuerySelector("nav, header nav, [role='navigation']");
    if (nav != null)
        sections.Add(new Section { Type = "navigation", Selector = GetSelector(nav) });

    // Header with nav
    var header = doc.QuerySelector("header");
    if (header != null && header.QuerySelector("a") != null && nav == null)
        sections.Add(new Section { Type = "navigation", Selector = GetSelector(header) });

    // Hero - first section with h1
    var h1 = doc.QuerySelector("h1");
    if (h1 != null)
    {
        var heroParent = h1.ParentElement;
        while (heroParent != null && heroParent.TagName.ToLower() != "section" && heroParent.TagName.ToLower() != "div")
            heroParent = heroParent.ParentElement;
        if (heroParent != null)
            sections.Add(new Section { Type = "hero", Selector = GetSelector(heroParent), Heading = h1.TextContent.Trim() });
    }

    // Footer
    var footer = doc.QuerySelector("footer, [role='contentinfo']");
    if (footer != null)
        sections.Add(new Section { Type = "footer", Selector = GetSelector(footer) });

    // Detect by class patterns
    var patterns = new[] {
        ("testimonial", "testimonials"), ("review", "testimonials"),
        ("pricing", "pricing"), ("price", "pricing"),
        ("faq", "faq"), ("accordion", "faq"),
        ("feature", "features"), ("benefit", "features"),
        ("stat", "stats"), ("metric", "stats"),
        ("cta", "cta"), ("call-to-action", "cta"),
        ("contact", "contact"), ("about", "about")
    };

    foreach (var (pattern, type) in patterns)
    {
        var el = doc.QuerySelector($"[class*='{pattern}'], [id*='{pattern}']");
        if (el != null && !sections.Any(s => s.Type == type))
            sections.Add(new Section { Type = type, Selector = GetSelector(el) });
    }

    // Feature grid - look for 3+ similar siblings
    foreach (var container in doc.QuerySelectorAll("section, div[class]").Take(50))
    {
        var children = container.Children.Where(c => c.TagName.ToLower() != "script").ToList();
        if (children.Count >= 3)
        {
            var firstTag = children[0].TagName;
            var similar = children.Count(c => c.TagName == firstTag);
            if (similar >= 3 && !sections.Any(s => s.Selector == GetSelector(container)))
            {
                sections.Add(new Section { Type = "grid", Selector = GetSelector(container) });
            }
        }
    }

    return sections;
}

// ═══════════════════════════════════════════════════════════════════════════════
// COMPONENT DETECTION
// ═══════════════════════════════════════════════════════════════════════════════

List<ButtonVariant> FindButtons(IDocument doc, Dictionary<string, Dictionary<string, string>> styles)
{
    var buttons = doc.QuerySelectorAll("button, [role='button'], a[class*='btn'], a[class*='button'], input[type='submit']").ToList();
    var allEls = doc.QuerySelectorAll("*").ToList();
    var variants = new Dictionary<string, ButtonVariant>();

    foreach (var btn in buttons.Take(100))
    {
        var idx = allEls.IndexOf(btn);
        styles.TryGetValue(idx.ToString(), out var s);

        var variant = ClassifyButton(s);
        if (!variants.ContainsKey(variant))
            variants[variant] = new ButtonVariant { Variant = variant, Styles = s ?? new(), Examples = new() };

        variants[variant].Count++;
        if (variants[variant].Examples.Count < 3)
            variants[variant].Examples.Add(new() { Text = btn.TextContent.Trim(), Selector = GetSelector(btn) });
    }

    return variants.Values.ToList();
}

string ClassifyButton(Dictionary<string, string>? styles)
{
    if (styles == null) return "default";

    var bg = styles.GetValueOrDefault("background-color", "");
    var border = styles.GetValueOrDefault("border", "");

    if (bg.Contains("transparent") || bg.Contains("rgba(0, 0, 0, 0)"))
    {
        if (!string.IsNullOrEmpty(border) && !border.Contains("none") && !border.Contains("0px"))
            return "outline";
        return "ghost";
    }

    if (bg.Contains("rgb") && IsReddish(bg))
        return "destructive";

    return "primary";
}

bool IsReddish(string color)
{
    var match = Regex.Match(color, @"rgb\((\d+),\s*(\d+),\s*(\d+)\)");
    if (match.Success)
    {
        var r = int.Parse(match.Groups[1].Value);
        var g = int.Parse(match.Groups[2].Value);
        var b = int.Parse(match.Groups[3].Value);
        return r > 180 && g < 100 && b < 100;
    }
    return false;
}

List<CardPattern> FindCards(IDocument doc, Dictionary<string, Dictionary<string, string>> styles)
{
    var patterns = new List<CardPattern>();
    var candidates = doc.QuerySelectorAll("[class*='card'], article, [class*='item'], [class*='tile']").ToList();

    // Group by structure
    var groups = candidates
        .GroupBy(el => GetStructureHash(el))
        .Where(g => g.Count() >= 2);

    foreach (var group in groups.Take(10))
    {
        var sample = group.First();
        patterns.Add(new CardPattern
        {
            Selector = GetSelector(sample),
            Count = group.Count(),
            HasImage = sample.QuerySelector("img") != null,
            HasHeading = sample.QuerySelector("h2, h3, h4") != null,
            HasButton = sample.QuerySelector("button, a[class*='btn']") != null
        });
    }

    return patterns;
}

string GetStructureHash(IElement el)
{
    var tags = el.Children.Select(c => c.TagName.ToLower()).Take(10);
    return string.Join("-", tags);
}

List<FormInfo> FindForms(IDocument doc)
{
    return doc.QuerySelectorAll("form").Take(20).Select(form => new FormInfo
    {
        Selector = GetSelector(form),
        InputCount = form.QuerySelectorAll("input, textarea, select").Length,
        HasSubmit = form.QuerySelector("button[type='submit'], input[type='submit']") != null
    }).ToList();
}

// ═══════════════════════════════════════════════════════════════════════════════
// ASSETS
// ═══════════════════════════════════════════════════════════════════════════════

AssetInfo ExtractAssets(IDocument doc)
{
    return new AssetInfo
    {
        Images = doc.QuerySelectorAll("img[src]").Take(50)
            .Select(img => new ImageAsset { Src = img.GetAttribute("src") ?? "", Alt = img.GetAttribute("alt") ?? "" }).ToList(),
        Scripts = doc.QuerySelectorAll("script[src]").Take(30)
            .Select(s => s.GetAttribute("src") ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
        Stylesheets = doc.QuerySelectorAll("link[rel='stylesheet']").Take(20)
            .Select(l => l.GetAttribute("href") ?? "").Where(h => !string.IsNullOrEmpty(h)).ToList()
    };
}

// ═══════════════════════════════════════════════════════════════════════════════
// DESIGN SYSTEM AGGREGATION
// ═══════════════════════════════════════════════════════════════════════════════

void AggregateDesignSystem(SiteBlueprint bp)
{
    var colors = new Dictionary<string, int>();
    var fontSizes = new Dictionary<string, int>();
    var fontFamilies = new Dictionary<string, int>();
    var borderRadii = new Dictionary<string, int>();

    foreach (var page in bp.Pages)
    {
        foreach (var el in page.Elements)
        {
            if (el.Styles == null) continue;

            foreach (var (key, val) in el.Styles)
            {
                if (key.Contains("color") && val.StartsWith("rgb"))
                    colors[val] = colors.GetValueOrDefault(val) + 1;
                if (key == "font-size")
                    fontSizes[val] = fontSizes.GetValueOrDefault(val) + 1;
                if (key == "font-family")
                {
                    var family = val.Split(',')[0].Trim().Trim('"', '\'');
                    fontFamilies[family] = fontFamilies.GetValueOrDefault(family) + 1;
                }
                if (key == "border-radius")
                    borderRadii[val] = borderRadii.GetValueOrDefault(val) + 1;
            }
        }
    }

    bp.DesignSystem = new DesignSystem
    {
        Colors = colors.OrderByDescending(c => c.Value).Take(20).Select(c => new ColorUsage { Value = c.Key, Count = c.Value }).ToList(),
        FontSizes = fontSizes.OrderByDescending(f => f.Value).Take(15).Select(f => new FontSize { Value = f.Key, Count = f.Value }).ToList(),
        FontFamilies = fontFamilies.OrderByDescending(f => f.Value).Take(10).Select(f => f.Key).ToList(),
        BorderRadii = borderRadii.OrderByDescending(b => b.Value).Take(10).Select(b => new BorderRadius { Value = b.Key, Count = b.Value }).ToList()
    };
}

// ═══════════════════════════════════════════════════════════════════════════════
// OUTPUT
// ═══════════════════════════════════════════════════════════════════════════════

void WriteOutputs(SiteBlueprint bp, string outDir)
{
    var options = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    // Full blueprint
    File.WriteAllText(Path.Combine(outDir, "blueprint.json"), JsonSerializer.Serialize(bp, options));

    // Design tokens
    File.WriteAllText(Path.Combine(outDir, "design-tokens.json"), JsonSerializer.Serialize(bp.DesignSystem, options));

    // Design tokens CSS
    var css = "/* Design Tokens */\n:root {\n";
    for (int i = 0; i < bp.DesignSystem.Colors.Count; i++)
        css += $"  --color-{i + 1}: {bp.DesignSystem.Colors[i].Value};\n";
    for (int i = 0; i < bp.DesignSystem.FontSizes.Count; i++)
        css += $"  --font-size-{i + 1}: {bp.DesignSystem.FontSizes[i].Value};\n";
    for (int i = 0; i < bp.DesignSystem.BorderRadii.Count; i++)
        css += $"  --radius-{i + 1}: {bp.DesignSystem.BorderRadii[i].Value};\n";
    css += "}\n";
    File.WriteAllText(Path.Combine(outDir, "design-tokens.css"), css);

    // Components summary
    var components = new {
        buttons = bp.Pages.SelectMany(p => p.Buttons).GroupBy(b => b.Variant).Select(g => g.First()).ToList(),
        cards = bp.Pages.SelectMany(p => p.Cards).Take(10).ToList(),
        forms = bp.Pages.SelectMany(p => p.Forms).Take(10).ToList()
    };
    File.WriteAllText(Path.Combine(outDir, "components.json"), JsonSerializer.Serialize(components, options));

    Console.WriteLine("\nFiles created:");
    Console.WriteLine("  - blueprint.json (full data)");
    Console.WriteLine("  - design-tokens.json / .css");
    Console.WriteLine("  - components.json");
}

// ═══════════════════════════════════════════════════════════════════════════════
// UTILITIES
// ═══════════════════════════════════════════════════════════════════════════════

string? GetArg(string[] args, params string[] names)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (names.Contains(args[i])) return args[i + 1];
    return null;
}

async Task EnsurePlaywrightInstalled()
{
    var playwrightPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");
    if (Directory.Exists(playwrightPath) &&
        Directory.GetDirectories(playwrightPath).Any(d => Path.GetFileName(d).StartsWith("chromium", StringComparison.OrdinalIgnoreCase)))
        return;

    Console.WriteLine("Installing Playwright Chromium...");
    var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
    if (exitCode != 0) throw new Exception("Failed to install Playwright browsers");
    Console.WriteLine("Done.\n");
}

// ═══════════════════════════════════════════════════════════════════════════════
// DATA MODELS
// ═══════════════════════════════════════════════════════════════════════════════

class SiteBlueprint
{
    public MetaInfo Meta { get; set; } = new();
    public List<PageData> Pages { get; set; } = new();
    public DesignSystem DesignSystem { get; set; } = new();
}

class MetaInfo
{
    public string Url { get; set; } = "";
    public DateTime AnalyzedAt { get; set; }
}

class PageData
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public List<string> Frameworks { get; set; } = new();
    public List<ElementInfo> Elements { get; set; } = new();
    public List<Section> Sections { get; set; } = new();
    public List<ButtonVariant> Buttons { get; set; } = new();
    public List<CardPattern> Cards { get; set; } = new();
    public List<FormInfo> Forms { get; set; } = new();
    public AssetInfo Assets { get; set; } = new();
    [JsonIgnore] public List<string> InternalLinks { get; set; } = new();
}

class ElementInfo
{
    public string Tag { get; set; } = "";
    public string? Id { get; set; }
    public List<string> Classes { get; set; } = new();
    public string Selector { get; set; } = "";
    public string? Text { get; set; }
    public BoundingBox? Box { get; set; }
    public Dictionary<string, string>? Styles { get; set; }
}

class BoundingBox
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

class Section
{
    public string Type { get; set; } = "";
    public string Selector { get; set; } = "";
    public string? Heading { get; set; }
}

class ButtonVariant
{
    public string Variant { get; set; } = "";
    public int Count { get; set; }
    public Dictionary<string, string> Styles { get; set; } = new();
    public List<ButtonExample> Examples { get; set; } = new();
}

class ButtonExample
{
    public string Text { get; set; } = "";
    public string Selector { get; set; } = "";
}

class CardPattern
{
    public string Selector { get; set; } = "";
    public int Count { get; set; }
    public bool HasImage { get; set; }
    public bool HasHeading { get; set; }
    public bool HasButton { get; set; }
}

class FormInfo
{
    public string Selector { get; set; } = "";
    public int InputCount { get; set; }
    public bool HasSubmit { get; set; }
}

class AssetInfo
{
    public List<ImageAsset> Images { get; set; } = new();
    public List<string> Scripts { get; set; } = new();
    public List<string> Stylesheets { get; set; } = new();
}

class ImageAsset
{
    public string Src { get; set; } = "";
    public string Alt { get; set; } = "";
}

class DesignSystem
{
    public List<ColorUsage> Colors { get; set; } = new();
    public List<FontSize> FontSizes { get; set; } = new();
    public List<string> FontFamilies { get; set; } = new();
    public List<BorderRadius> BorderRadii { get; set; } = new();
}

class ColorUsage
{
    public string Value { get; set; } = "";
    public int Count { get; set; }
}

class FontSize
{
    public string Value { get; set; } = "";
    public int Count { get; set; }
}

class BorderRadius
{
    public string Value { get; set; } = "";
    public int Count { get; set; }
}
