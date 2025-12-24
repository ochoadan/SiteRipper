namespace SiteRipper.Orchestration;

using Microsoft.Playwright;
using AngleSharp;
using AngleSharp.Dom;
using SiteRipper.Models;
using SiteRipper.Detection;
using SiteRipper.Clustering;
using SiteRipper.Hierarchy;
using SiteRipper.Layout;
using SiteRipper.Variants;
using SiteRipper.Css;
using SiteRipper.Typography;
using SiteRipper.Assets;
using SiteRipper.Seo;
using SiteRipper.Structure;
using SiteRipper.Accessibility;
using SiteRipper.ThirdParty;

public class IntelligentComponentDetector
{
    private readonly ComponentTypeDetector _componentDetector = new();
    private readonly VisualClusteringEngine _clusteringEngine = new();
    private readonly RepeatedPatternDetector _patternDetector = new();
    private readonly ComponentHierarchyBuilder _hierarchyBuilder = new();
    private readonly LayoutDetector _layoutDetector = new();
    private readonly SpacingSystemDetector _spacingDetector = new();
    private readonly SizeVariantDetector _sizeVariantDetector = new();
    private readonly ColorVariantDetector _colorVariantDetector = new();
    private readonly StateDetector _stateDetector = new();
    private readonly CssDownloader _cssDownloader = new();
    private readonly CssVariableExtractor _cssVarExtractor = new();
    private readonly TypographyExtractor _typographyExtractor = new();
    private readonly AssetExtractor _assetExtractor = new();
    private readonly MetaExtractor _metaExtractor = new();
    private readonly ColorPaletteExtractor _colorPaletteExtractor = new();
    private readonly NavigationExtractor _navigationExtractor = new();
    private readonly A11yAuditor _a11yAuditor = new();
    private readonly IntegrationDetector _integrationDetector = new();

    public async Task<ComponentAnalysisResult> AnalyzeAsync(
        IPage page,
        string html,
        string url,
        Dictionary<int, Dictionary<string, string>> styles,
        Dictionary<int, BoundingBox> boxes)
    {
        var result = new ComponentAnalysisResult
        {
            Url = url,
            AnalyzedAt = DateTime.UtcNow
        };

        Console.WriteLine("  Parsing HTML...");
        var angleConfig = Configuration.Default;
        var angleContext = BrowsingContext.New(angleConfig);
        var doc = await angleContext.OpenAsync(req => req.Content(html));

        // 1. Detect components by structure
        Console.WriteLine("  Detecting components...");
        result.DetectedComponents = _componentDetector.DetectComponents(doc.Body!, styles, boxes);
        Console.WriteLine($"    Found {result.DetectedComponents.Count} components");

        if (result.DetectedComponents.Count == 0)
        {
            Console.WriteLine("    No components detected, skipping further analysis");
            return result;
        }

        // 2. Visual clustering
        Console.WriteLine("  Clustering visually similar elements...");
        var clusterables = result.DetectedComponents
            .Select(c => new ClusterableElement
            {
                Component = c,
                FeatureVector = VisualFeatureVector.FromComponent(c),
                Fingerprint = c.Fingerprint
            })
            .ToList();

        result.VisualClusters = _clusteringEngine.Cluster(clusterables);
        Console.WriteLine($"    Found {result.VisualClusters.Count} clusters");

        // 3. Detect repeated patterns
        Console.WriteLine("  Detecting repeated patterns...");
        result.RepeatedPatterns = _patternDetector.DetectPatterns(result.DetectedComponents);
        Console.WriteLine($"    Found {result.RepeatedPatterns.Count} patterns");

        // 4. Build component hierarchy
        Console.WriteLine("  Building component hierarchy...");
        result.HierarchyTree = _hierarchyBuilder.BuildTree(doc.Body!, result.DetectedComponents);

        // 5. Detect spacing system
        Console.WriteLine("  Analyzing spacing system...");
        result.SpacingSystem = _spacingDetector.DetectSpacingSystem(result.DetectedComponents);
        Console.WriteLine($"    Base unit: {result.SpacingSystem.BaseUnit}px, Scale: [{string.Join(", ", result.SpacingSystem.Scale.Take(5))}...]");

        // 6. Detect variants for each component type
        Console.WriteLine("  Detecting component variants...");
        var componentGroups = result.DetectedComponents.GroupBy(c => c.Type);

        foreach (var group in componentGroups)
        {
            var components = group.ToList();
            var variants = new ComponentVariants
            {
                ComponentType = group.Key,
                SizeVariants = _sizeVariantDetector.DetectSizeVariants(components),
                ColorVariants = _colorVariantDetector.DetectColorVariants(components)
            };

            // Detect states for one sample component (expensive operation)
            if (components.Count > 0)
            {
                try
                {
                    variants.StateVariants = await _stateDetector.DetectStates(page, components.First());
                }
                catch
                {
                    variants.StateVariants = new() { new() { State = ComponentState.Default, IsDetected = true } };
                }
            }

            result.ComponentVariants.Add(variants);
        }

        Console.WriteLine($"    Analyzed {result.ComponentVariants.Count} component types");

        // 7. Extract CSS variables from page
        Console.WriteLine("  Extracting CSS variables...");
        result.CssVariables = await _cssVarExtractor.ExtractFromPageAsync(page);
        Console.WriteLine($"    Found {result.CssVariables.Count} CSS custom properties");

        // 7b. Extract keyframes and media queries from browser DOM (CSS-in-JS support)
        Console.WriteLine("  Extracting keyframes and media queries from browser...");
        var (browserKeyframes, browserMediaQueries) = await _cssVarExtractor.ExtractRulesFromPageAsync(page);
        result.Keyframes.AddRange(browserKeyframes);
        foreach (var mq in browserMediaQueries)
        {
            if (!result.MediaQueries.Any(m => m.Query == mq.Query))
                result.MediaQueries.Add(mq);
        }
        Console.WriteLine($"    Found {browserKeyframes.Count} keyframes, {browserMediaQueries.Count} media queries from browser");

        // 8. Download and parse CSS files
        Console.WriteLine("  Downloading CSS files...");
        var stylesheetUrls = ExtractStylesheetUrls(doc);
        var baseUri = new Uri(url);
        var cssFiles = await _cssDownloader.DownloadAllAsync(stylesheetUrls, baseUri);
        Console.WriteLine($"    Downloaded {cssFiles.Count} CSS files");

        foreach (var cssFile in cssFiles)
        {
            result.CssSourceFiles.Add(cssFile.FileName);

            // Extract additional CSS variables
            var fileVars = _cssVarExtractor.ExtractFromCssContent(cssFile.Content, cssFile.FileName);
            foreach (var v in fileVars)
            {
                if (!result.CssVariables.Any(cv => cv.Name == v.Name))
                    result.CssVariables.Add(v);
            }

            // Extract keyframes
            var keyframes = _cssVarExtractor.ExtractKeyframes(cssFile.Content, cssFile.FileName);
            result.Keyframes.AddRange(keyframes);

            // Extract media queries
            var mediaQueries = _cssVarExtractor.ExtractMediaQueries(cssFile.Content, cssFile.FileName);
            foreach (var mq in mediaQueries)
            {
                if (!result.MediaQueries.Any(m => m.Query == mq.Query))
                    result.MediaQueries.Add(mq);
            }
        }

        Console.WriteLine($"    Found {result.Keyframes.Count} keyframe animations");
        Console.WriteLine($"    Found {result.MediaQueries.Count} media queries");

        // 9. Extract typography system
        Console.WriteLine("  Extracting typography system...");
        result.Typography = await _typographyExtractor.ExtractAsync(page);
        Console.WriteLine($"    Found {result.Typography.FontFamilies.Count} font families, {result.Typography.FontWeights.Count} weights");

        // 10. Extract assets (images, SVGs, fonts)
        Console.WriteLine("  Extracting assets...");
        result.Assets = await _assetExtractor.ExtractAsync(page, url);
        Console.WriteLine($"    Found {result.Assets.Images.Count} images, {result.Assets.SvgIcons.Count} SVGs, {result.Assets.FontFiles.Count} font files");

        // 11. Extract SEO/meta data
        Console.WriteLine("  Extracting SEO metadata...");
        result.Seo = await _metaExtractor.ExtractAsync(page);
        Console.WriteLine($"    Title: {result.Seo.Title.Substring(0, Math.Min(50, result.Seo.Title.Length))}...");

        // 12. Extract color palette
        Console.WriteLine("  Analyzing color palette...");
        result.ColorPalette = await _colorPaletteExtractor.ExtractAsync(page);
        Console.WriteLine($"    Found {result.ColorPalette.AllColors.Count} unique colors, {result.ColorPalette.Gradients.Count} gradients");

        // 13. Extract site structure
        Console.WriteLine("  Analyzing site structure...");
        result.Structure = await _navigationExtractor.ExtractAsync(page, url);
        Console.WriteLine($"    Found {result.Structure.InternalLinks.Count} internal links, {result.Structure.ExternalLinks.Count} external links");

        // 13b. Link components to their page sections
        LinkComponentsToSections(result);

        // 14. Accessibility audit
        Console.WriteLine("  Running accessibility audit...");
        result.Accessibility = await _a11yAuditor.AuditAsync(page);
        Console.WriteLine($"    A11y score: {result.Accessibility.Score}/100");

        // 15. Detect third-party integrations
        Console.WriteLine("  Detecting third-party integrations...");
        result.ThirdParty = await _integrationDetector.DetectAsync(page);
        Console.WriteLine($"    Found {result.ThirdParty.Analytics.Count} analytics, {result.ThirdParty.Marketing.Count} marketing tools");

        return result;
    }

    private List<string> ExtractStylesheetUrls(IDocument doc)
    {
        return doc.QuerySelectorAll("link[rel='stylesheet']")
            .Select(l => l.GetAttribute("href"))
            .Where(h => !string.IsNullOrEmpty(h))
            .Cast<string>()
            .Take(20)
            .ToList();
    }

    private void LinkComponentsToSections(ComponentAnalysisResult result)
    {
        if (result.Structure?.PageSections == null || result.Structure.PageSections.Count == 0)
            return;

        foreach (var section in result.Structure.PageSections)
        {
            // Create a bounding box for the section (full viewport width)
            var sectionBox = new BoundingBox
            {
                X = 0,
                Y = section.YPosition,
                Width = 2000,  // Full viewport width
                Height = section.Height
            };

            foreach (var component in result.DetectedComponents)
            {
                var compBox = component.VisualProperties.BoundingBox;

                // Check if component's center is within section bounds
                var compCenterY = compBox.Y + (compBox.Height / 2);
                if (compCenterY >= section.YPosition && compCenterY < section.YPosition + section.Height)
                {
                    section.ComponentIds.Add(component.Id);
                    component.SectionId = section.Id ?? $"section-{section.YPosition}";
                }
            }
        }
    }
}
