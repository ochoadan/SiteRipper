namespace SiteRipper.Aggregation;

using SiteRipper.Crawling;
using SiteRipper.Orchestration;
using SiteRipper.Models;
using SiteRipper.Detection;
using SiteRipper.Css;
using SiteRipper.Typography;
using SiteRipper.Assets;
using SiteRipper.Seo;
using SiteRipper.ThirdParty;

public class ResultAggregator
{
    private readonly ComponentMatcher _matcher = new();

    public AggregatedSiteResult Aggregate(SiteCrawlResult crawlResult)
    {
        var result = new AggregatedSiteResult
        {
            SeedUrl = crawlResult.SeedUrl,
            AnalyzedAt = crawlResult.StartedAt,
            CrawlDuration = crawlResult.Duration,
            MaxDepth = crawlResult.MaxDepth,
            MaxPages = crawlResult.MaxPages
        };

        var successfulPages = crawlResult.PageResults.Where(p => p.Success && p.AnalysisResult != null).ToList();

        // Build page summaries
        foreach (var page in successfulPages)
        {
            result.Pages.Add(new PageSummary
            {
                Url = page.Url,
                Title = page.Title,
                Depth = page.Depth,
                ComponentCount = page.AnalysisResult!.DetectedComponents.Count,
                AccessibilityScore = page.AnalysisResult.Accessibility?.Score ?? 0,
                CrawledAt = page.CrawledAt
            });
        }

        // Aggregate components across pages
        result.Components = AggregateComponents(successfulPages);

        // Aggregate CSS variables
        result.CssVariables = AggregateCssVariables(successfulPages);

        // Aggregate colors
        result.ColorPalette = AggregateColors(successfulPages);

        // Aggregate typography
        result.Typography = AggregateTypography(successfulPages);

        // Aggregate assets
        result.Assets = AggregateAssets(successfulPages);

        // Aggregate third-party integrations
        result.ThirdParty = AggregateThirdParty(successfulPages);

        // Aggregate keyframes and media queries
        result.Keyframes = AggregateKeyframes(successfulPages);
        result.MediaQueries = AggregateMediaQueries(successfulPages);

        // Calculate summary stats
        result.Summary = new SiteSummary
        {
            TotalPagesAnalyzed = successfulPages.Count,
            TotalPagesFailed = crawlResult.FailureCount,
            TotalUniqueComponents = result.Components.Count,
            TotalCssVariables = result.CssVariables.Count,
            TotalColors = result.ColorPalette?.AllColors.Count ?? 0,
            TotalFontFamilies = result.Typography?.FontFamilies.Count ?? 0,
            TotalAssets = (result.Assets?.Images.Count ?? 0) + (result.Assets?.SvgIcons.Count ?? 0),
            AverageAccessibilityScore = successfulPages
                .Where(p => p.AnalysisResult?.Accessibility != null)
                .Select(p => p.AnalysisResult!.Accessibility!.Score)
                .DefaultIfEmpty(0)
                .Average()
        };

        return result;
    }

    private List<AggregatedComponent> AggregateComponents(List<PageCrawlResult> pages)
    {
        var allComponents = new List<(DetectedComponent component, string pageUrl)>();

        foreach (var page in pages)
        {
            foreach (var component in page.AnalysisResult!.DetectedComponents)
            {
                allComponents.Add((component, page.Url));
            }
        }

        // Group by fingerprint hash
        var componentGroups = new Dictionary<string, AggregatedComponent>();

        foreach (var (component, pageUrl) in allComponents)
        {
            var hash = component.Fingerprint.Hash;

            if (!componentGroups.TryGetValue(hash, out var existing))
            {
                existing = new AggregatedComponent
                {
                    Id = component.Id,
                    Type = component.Type,
                    FingerprintHash = hash,
                    Fingerprint = component.Fingerprint,
                    Representative = component,
                    FoundOnPages = new List<string>(),
                    TotalInstances = 0,
                    Variants = new List<ComponentVariant>()
                };
                componentGroups[hash] = existing;
            }

            if (!existing.FoundOnPages.Contains(pageUrl))
                existing.FoundOnPages.Add(pageUrl);

            existing.TotalInstances++;

            // Track visual variants
            var visualKey = GetVisualKey(component);
            var variant = existing.Variants.FirstOrDefault(v => v.VisualKey == visualKey);
            if (variant == null)
            {
                variant = new ComponentVariant
                {
                    VisualKey = visualKey,
                    VisualProperties = component.VisualProperties,
                    FoundOnPages = new List<string>(),
                    InstanceCount = 0
                };
                existing.Variants.Add(variant);
            }
            if (!variant.FoundOnPages.Contains(pageUrl))
                variant.FoundOnPages.Add(pageUrl);
            variant.InstanceCount++;
        }

        return componentGroups.Values
            .OrderByDescending(c => c.FoundOnPages.Count)
            .ThenByDescending(c => c.TotalInstances)
            .ToList();
    }

    private string GetVisualKey(DetectedComponent component)
    {
        var vp = component.VisualProperties;
        return $"{vp.BackgroundColor}|{Math.Round(vp.Width / 50) * 50}|{Math.Round(vp.ParsedFontSize)}|{vp.HasShadow}|{vp.HasBorder}";
    }

    private List<CssCustomProperty> AggregateCssVariables(List<PageCrawlResult> pages)
    {
        var varDict = new Dictionary<string, CssCustomProperty>();

        foreach (var page in pages)
        {
            foreach (var cssVar in page.AnalysisResult!.CssVariables)
            {
                if (!varDict.ContainsKey(cssVar.Name))
                {
                    varDict[cssVar.Name] = cssVar;
                }
            }
        }

        return varDict.Values.OrderBy(v => v.Name).ToList();
    }

    private ColorPalette? AggregateColors(List<PageCrawlResult> pages)
    {
        var colorDict = new Dictionary<string, ColorUsage>();
        var gradients = new Dictionary<string, GradientDefinition>();

        foreach (var page in pages)
        {
            var palette = page.AnalysisResult?.ColorPalette;
            if (palette == null) continue;

            foreach (var color in palette.AllColors)
            {
                if (!colorDict.TryGetValue(color.Hex, out var existing))
                {
                    existing = new ColorUsage
                    {
                        Color = color.Color,
                        Hex = color.Hex,
                        UsageCount = 0,
                        Category = color.Category
                    };
                    colorDict[color.Hex] = existing;
                }
                existing.UsageCount += color.UsageCount;
            }

            foreach (var gradient in palette.Gradients)
            {
                if (!gradients.ContainsKey(gradient.Value))
                    gradients[gradient.Value] = gradient;
            }
        }

        if (colorDict.Count == 0) return null;

        var allColors = colorDict.Values
            .OrderByDescending(c => c.UsageCount)
            .ToList();

        var bgColors = allColors.Where(c => c.Category.Contains("background")).ToList();
        var textColors = allColors.Where(c => c.Category.Contains("text")).ToList();
        var borderColors = allColors.Where(c => c.Category.Contains("border")).ToList();

        return new ColorPalette
        {
            AllColors = allColors.Take(50).ToList(),
            PrimaryColor = bgColors.FirstOrDefault(),
            SecondaryColor = bgColors.Skip(1).FirstOrDefault(),
            AccentColor = bgColors.Skip(2).FirstOrDefault(),
            TextColors = textColors,
            BackgroundColors = bgColors,
            BorderColors = borderColors,
            Gradients = gradients.Values.ToList()
        };
    }

    private TypographySystem? AggregateTypography(List<PageCrawlResult> pages)
    {
        var fontFamilies = new Dictionary<string, int>();
        var fontWeights = new Dictionary<string, int>();
        var lineHeights = new HashSet<string>();
        var letterSpacings = new HashSet<string>();
        var fontFaces = new Dictionary<string, FontFaceInfo>();

        foreach (var page in pages)
        {
            var typo = page.AnalysisResult?.Typography;
            if (typo == null) continue;

            foreach (var ff in typo.FontFamilies)
            {
                fontFamilies.TryGetValue(ff.Name, out var count);
                fontFamilies[ff.Name] = count + ff.UsageCount;
            }

            foreach (var fw in typo.FontWeights)
            {
                fontWeights.TryGetValue(fw.Weight, out var count);
                fontWeights[fw.Weight] = count + fw.UsageCount;
            }

            foreach (var lh in typo.LineHeights)
                lineHeights.Add(lh);

            foreach (var ls in typo.LetterSpacings)
                letterSpacings.Add(ls);

            foreach (var ff in typo.FontFaces)
            {
                var key = $"{ff.Family}|{ff.Weight}|{ff.Style}";
                if (!fontFaces.ContainsKey(key))
                    fontFaces[key] = ff;
            }
        }

        if (fontFamilies.Count == 0) return null;

        return new TypographySystem
        {
            FontFamilies = fontFamilies
                .OrderByDescending(kv => kv.Value)
                .Select(kv => new FontFamilyInfo { Name = kv.Key, UsageCount = kv.Value })
                .ToList(),
            FontWeights = fontWeights
                .OrderByDescending(kv => kv.Value)
                .Select(kv => new FontWeightInfo { Weight = kv.Key, UsageCount = kv.Value })
                .ToList(),
            LineHeights = lineHeights.OrderBy(lh => lh).ToList(),
            LetterSpacings = letterSpacings.OrderBy(ls => ls).ToList(),
            FontFaces = fontFaces.Values.ToList()
        };
    }

    private ExtractedAssets? AggregateAssets(List<PageCrawlResult> pages)
    {
        var images = new Dictionary<string, ImageAsset>();
        var svgs = new Dictionary<string, SvgIcon>();
        var fonts = new Dictionary<string, FontFile>();
        var scripts = new Dictionary<string, ScriptInfo>();
        var stylesheets = new Dictionary<string, StylesheetInfo>();
        string? favicon = null;

        foreach (var page in pages)
        {
            var assets = page.AnalysisResult?.Assets;
            if (assets == null) continue;

            foreach (var img in assets.Images)
            {
                if (!images.ContainsKey(img.Url))
                    images[img.Url] = img;
            }

            foreach (var svg in assets.SvgIcons)
            {
                var key = svg.ViewBox + "|" + svg.Width + "x" + svg.Height;
                if (!svgs.ContainsKey(key))
                    svgs[key] = svg;
            }

            foreach (var font in assets.FontFiles)
            {
                if (!fonts.ContainsKey(font.Url))
                    fonts[font.Url] = font;
            }

            foreach (var script in assets.Scripts)
            {
                if (!scripts.ContainsKey(script.Url))
                    scripts[script.Url] = script;
            }

            foreach (var ss in assets.Stylesheets)
            {
                if (!stylesheets.ContainsKey(ss.Url))
                    stylesheets[ss.Url] = ss;
            }

            if (favicon == null && !string.IsNullOrEmpty(assets.Favicon))
                favicon = assets.Favicon;
        }

        return new ExtractedAssets
        {
            Images = images.Values.ToList(),
            SvgIcons = svgs.Values.Take(100).ToList(),
            FontFiles = fonts.Values.ToList(),
            Scripts = scripts.Values.ToList(),
            Stylesheets = stylesheets.Values.ToList(),
            Favicon = favicon ?? ""
        };
    }

    private ThirdPartyIntegrations? AggregateThirdParty(List<PageCrawlResult> pages)
    {
        var analytics = new Dictionary<string, AnalyticsService>();
        var marketing = new Dictionary<string, MarketingService>();
        var payment = new Dictionary<string, PaymentService>();
        var allServices = new Dictionary<string, ExternalService>();
        ChatWidgetInfo? chatWidget = null;
        CdnInfo? cdn = null;
        var socialEmbeds = new Dictionary<string, SocialEmbed>();

        foreach (var page in pages)
        {
            var tp = page.AnalysisResult?.ThirdParty;
            if (tp == null) continue;

            foreach (var a in tp.Analytics)
            {
                if (!analytics.ContainsKey(a.Name))
                    analytics[a.Name] = a;
            }

            foreach (var m in tp.Marketing)
            {
                if (!marketing.ContainsKey(m.Name))
                    marketing[m.Name] = m;
            }

            foreach (var p in tp.Payment)
            {
                if (!payment.ContainsKey(p.Name))
                    payment[p.Name] = p;
            }

            foreach (var s in tp.AllServices)
            {
                if (!allServices.ContainsKey(s.Domain))
                    allServices[s.Domain] = s;
            }

            foreach (var e in tp.SocialEmbeds)
            {
                if (!socialEmbeds.ContainsKey(e.Platform))
                    socialEmbeds[e.Platform] = e;
            }

            if (chatWidget == null && tp.ChatWidget != null)
                chatWidget = tp.ChatWidget;

            if (cdn == null && tp.Cdn != null)
                cdn = tp.Cdn;
        }

        return new ThirdPartyIntegrations
        {
            Analytics = analytics.Values.ToList(),
            Marketing = marketing.Values.ToList(),
            Payment = payment.Values.ToList(),
            Cdn = cdn,
            AllServices = allServices.Values.ToList(),
            ChatWidget = chatWidget,
            SocialEmbeds = socialEmbeds.Values.ToList()
        };
    }

    private List<KeyframeAnimation> AggregateKeyframes(List<PageCrawlResult> pages)
    {
        var keyframes = new Dictionary<string, KeyframeAnimation>();

        foreach (var page in pages)
        {
            foreach (var kf in page.AnalysisResult!.Keyframes)
            {
                if (!keyframes.ContainsKey(kf.Name))
                    keyframes[kf.Name] = kf;
            }
        }

        return keyframes.Values.ToList();
    }

    private List<MediaQueryInfo> AggregateMediaQueries(List<PageCrawlResult> pages)
    {
        var queries = new Dictionary<string, MediaQueryInfo>();

        foreach (var page in pages)
        {
            foreach (var mq in page.AnalysisResult!.MediaQueries)
            {
                if (!queries.ContainsKey(mq.Query))
                    queries[mq.Query] = mq;
            }
        }

        return queries.Values.OrderBy(mq => mq.MinWidth ?? mq.MaxWidth ?? 0).ToList();
    }
}

public class AggregatedSiteResult
{
    public string SeedUrl { get; set; } = "";
    public DateTime AnalyzedAt { get; set; }
    public TimeSpan CrawlDuration { get; set; }
    public int MaxDepth { get; set; }
    public int MaxPages { get; set; }

    public List<PageSummary> Pages { get; set; } = new();
    public List<AggregatedComponent> Components { get; set; } = new();
    public List<CssCustomProperty> CssVariables { get; set; } = new();
    public ColorPalette? ColorPalette { get; set; }
    public TypographySystem? Typography { get; set; }
    public ExtractedAssets? Assets { get; set; }
    public ThirdPartyIntegrations? ThirdParty { get; set; }
    public List<KeyframeAnimation> Keyframes { get; set; } = new();
    public List<MediaQueryInfo> MediaQueries { get; set; } = new();
    public SiteSummary Summary { get; set; } = new();
}

public class PageSummary
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public int Depth { get; set; }
    public int ComponentCount { get; set; }
    public int AccessibilityScore { get; set; }
    public DateTime CrawledAt { get; set; }
}

public class AggregatedComponent
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string FingerprintHash { get; set; } = "";
    public StructuralFingerprint Fingerprint { get; set; } = new();
    public DetectedComponent Representative { get; set; } = null!;
    public List<string> FoundOnPages { get; set; } = new();
    public int TotalInstances { get; set; }
    public List<ComponentVariant> Variants { get; set; } = new();

    public int PageCount => FoundOnPages.Count;

    // Convenience properties delegating to Representative (for AI output)
    public string? Text => Representative?.Text;
    public string? Selector => Representative?.Selector;
    public List<string> Classes => Representative?.Classes ?? new();
    public string? SectionId => Representative?.SectionId;
    public string? OuterHtml => Representative?.OuterHtml;
    public VisualProperties VisualProperties => Representative?.VisualProperties ?? new();
    public List<string> ChildIds => Representative?.ChildIds ?? new();
    public List<string> Composition => Representative?.Composition ?? new();
}

public class ComponentVariant
{
    public string VisualKey { get; set; } = "";
    public VisualProperties VisualProperties { get; set; } = new();
    public List<string> FoundOnPages { get; set; } = new();
    public int InstanceCount { get; set; }
}

public class SiteSummary
{
    public int TotalPagesAnalyzed { get; set; }
    public int TotalPagesFailed { get; set; }
    public int TotalUniqueComponents { get; set; }
    public int TotalCssVariables { get; set; }
    public int TotalColors { get; set; }
    public int TotalFontFamilies { get; set; }
    public int TotalAssets { get; set; }
    public double AverageAccessibilityScore { get; set; }
}
