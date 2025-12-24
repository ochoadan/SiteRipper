namespace SiteRipper.Clustering;

using SiteRipper.Models;
using SiteRipper.Detection;

public class VisualFeatureVector
{
    // Dimensions (normalized to viewport)
    public double NormalizedWidth { get; set; }
    public double NormalizedHeight { get; set; }
    public double AspectRatio { get; set; }

    // Colors (HSL)
    public double BackgroundH { get; set; }
    public double BackgroundS { get; set; }
    public double BackgroundL { get; set; }
    public double TextH { get; set; }
    public double TextS { get; set; }
    public double TextL { get; set; }

    // Typography
    public double FontSizeNormalized { get; set; }
    public double FontWeightNormalized { get; set; }  // 0-1 scale

    // Spacing
    public double PaddingNormalized { get; set; }
    public double BorderRadiusNormalized { get; set; }

    // Effects
    public double HasShadow { get; set; }  // 0 or 1
    public double HasBorder { get; set; }  // 0 or 1

    private const double ViewportWidth = 1920.0;
    private const double ViewportHeight = 1080.0;
    private const double MaxFontSize = 72.0;
    private const double MaxPadding = 100.0;
    private const double MaxBorderRadius = 50.0;

    public static VisualFeatureVector FromComponent(DetectedComponent component)
    {
        var vis = component.VisualProperties;
        var bgHsl = vis.BackgroundHsl;
        var textHsl = vis.TextHsl;

        return new VisualFeatureVector
        {
            NormalizedWidth = Math.Min(vis.Width / ViewportWidth, 1.0),
            NormalizedHeight = Math.Min(vis.Height / ViewportHeight, 1.0),
            AspectRatio = vis.Height > 0 ? Math.Min(vis.Width / vis.Height, 5.0) / 5.0 : 0.5,

            BackgroundH = bgHsl.H / 360.0,
            BackgroundS = bgHsl.S,
            BackgroundL = bgHsl.L,
            TextH = textHsl.H / 360.0,
            TextS = textHsl.S,
            TextL = textHsl.L,

            FontSizeNormalized = Math.Min(vis.ParsedFontSize / MaxFontSize, 1.0),
            FontWeightNormalized = (vis.ParsedFontWeight - 100) / 800.0,

            PaddingNormalized = Math.Min(vis.ParsedPadding / MaxPadding, 1.0),
            BorderRadiusNormalized = Math.Min(vis.ParsedBorderRadius / MaxBorderRadius, 1.0),

            HasShadow = vis.HasShadow ? 1.0 : 0.0,
            HasBorder = vis.HasBorder ? 1.0 : 0.0
        };
    }

    public double[] ToArray()
    {
        return new[]
        {
            NormalizedWidth,
            NormalizedHeight,
            AspectRatio,
            BackgroundH,
            BackgroundS,
            BackgroundL,
            TextH,
            TextS,
            TextL,
            FontSizeNormalized,
            FontWeightNormalized,
            PaddingNormalized,
            BorderRadiusNormalized,
            HasShadow,
            HasBorder
        };
    }

    public double DistanceTo(VisualFeatureVector other)
    {
        var a = ToArray();
        var b = other.ToArray();

        double sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            var diff = a[i] - b[i];
            sum += diff * diff;
        }
        return Math.Sqrt(sum);
    }
}

public class ClusterableElement
{
    public DetectedComponent Component { get; set; } = null!;
    public VisualFeatureVector FeatureVector { get; set; } = null!;
    public StructuralFingerprint Fingerprint { get; set; } = null!;
    public int ClusterId { get; set; } = -1;  // -1 = unassigned, 0 = noise
}
