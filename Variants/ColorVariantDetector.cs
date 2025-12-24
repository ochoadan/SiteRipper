namespace SiteRipper.Variants;

using SiteRipper.Models;

public class ColorVariantDetector
{
    private static readonly Dictionary<string, HslColor> SemanticColors = new()
    {
        ["primary"] = new HslColor(220, 0.8, 0.5),      // Blue
        ["secondary"] = new HslColor(210, 0.15, 0.5),   // Gray-blue
        ["success"] = new HslColor(140, 0.7, 0.45),     // Green
        ["danger"] = new HslColor(0, 0.75, 0.55),       // Red
        ["warning"] = new HslColor(40, 0.9, 0.55),      // Orange/Yellow
        ["info"] = new HslColor(195, 0.7, 0.5)          // Cyan
    };

    public List<ColorVariant> DetectColorVariants(List<DetectedComponent> components)
    {
        var variants = new Dictionary<string, ColorVariant>();

        foreach (var component in components)
        {
            var bgColor = component.VisualProperties.BackgroundColor;
            var textColor = component.VisualProperties.Color;

            // Skip transparent/no background
            if (string.IsNullOrEmpty(bgColor) ||
                bgColor.Contains("rgba(0, 0, 0, 0)") ||
                bgColor == "transparent")
                continue;

            var bgHsl = HslColor.FromRgb(bgColor);
            var variantName = ClassifyColor(bgHsl);

            if (!variants.ContainsKey(variantName))
            {
                variants[variantName] = new ColorVariant
                {
                    Name = variantName,
                    BackgroundColor = bgColor,
                    TextColor = textColor ?? "",
                    BackgroundHsl = bgHsl,
                    InstanceCount = 0
                };
            }

            variants[variantName].InstanceCount++;
        }

        return variants.Values
            .Where(v => v.InstanceCount >= 1)
            .OrderByDescending(v => v.InstanceCount)
            .ToList();
    }

    private string ClassifyColor(HslColor color)
    {
        // Check for achromatic (grayscale)
        if (color.S < 0.1)
        {
            if (color.L > 0.9) return "light";
            if (color.L < 0.15) return "dark";
            return "neutral";
        }

        // Find closest semantic color by hue
        var closest = SemanticColors
            .OrderBy(sc => HueDistance(color.H, sc.Value.H))
            .First();

        // Check if it's close enough to be classified
        if (HueDistance(color.H, closest.Value.H) < 35)
            return closest.Key;

        // Fall back to hue name
        return GetHueName(color.H);
    }

    private double HueDistance(double h1, double h2)
    {
        var diff = Math.Abs(h1 - h2);
        return Math.Min(diff, 360 - diff);
    }

    private string GetHueName(double hue)
    {
        return hue switch
        {
            < 15 => "red",
            < 45 => "orange",
            < 75 => "yellow",
            < 150 => "green",
            < 210 => "cyan",
            < 270 => "blue",
            < 330 => "purple",
            _ => "red"
        };
    }
}
