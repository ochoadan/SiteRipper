namespace SiteRipper.Orchestration;

using SiteRipper.Models;
using SiteRipper.Detection;
using SiteRipper.Clustering;
using SiteRipper.Hierarchy;
using SiteRipper.Css;
using SiteRipper.Typography;
using SiteRipper.Assets;
using SiteRipper.Seo;
using SiteRipper.Structure;
using SiteRipper.Accessibility;
using SiteRipper.ThirdParty;

public class ComponentAnalysisResult
{
    // Metadata
    public string Url { get; set; } = "";
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    // Detected components
    public List<DetectedComponent> DetectedComponents { get; set; } = new();

    // Clustering results
    public List<VisualCluster> VisualClusters { get; set; } = new();
    public List<RepeatedPattern> RepeatedPatterns { get; set; } = new();

    // Hierarchy
    public ComponentHierarchyNode? HierarchyTree { get; set; }

    // Layout analysis
    public List<LayoutInfo> Layouts { get; set; } = new();
    public SpacingSystem? SpacingSystem { get; set; }

    // Variants
    public List<ComponentVariants> ComponentVariants { get; set; } = new();

    // CSS analysis
    public List<CssCustomProperty> CssVariables { get; set; } = new();
    public List<KeyframeAnimation> Keyframes { get; set; } = new();
    public List<MediaQueryInfo> MediaQueries { get; set; } = new();
    public List<string> CssSourceFiles { get; set; } = new();

    // v3.0 - Typography
    public TypographySystem? Typography { get; set; }

    // v3.0 - Assets
    public ExtractedAssets? Assets { get; set; }

    // v3.0 - SEO/Meta
    public SeoData? Seo { get; set; }

    // v3.0 - Color Palette
    public ColorPalette? ColorPalette { get; set; }

    // v3.0 - Site Structure
    public SiteStructure? Structure { get; set; }

    // v3.0 - Accessibility
    public AccessibilityReport? Accessibility { get; set; }

    // v3.0 - Third-Party Integrations
    public ThirdPartyIntegrations? ThirdParty { get; set; }
}
