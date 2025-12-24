namespace SiteRipper.Css;

using Microsoft.Playwright;
using System.Text.Json;
using System.Text.RegularExpressions;

public class ColorPaletteExtractor
{
    public async Task<ColorPalette> ExtractAsync(IPage page)
    {
        var result = new ColorPalette();

        try
        {
            var json = await page.EvaluateAsync<string>(@"
                () => {
                    const textColors = new Map();
                    const bgColors = new Map();
                    const borderColors = new Map();
                    const gradients = [];

                    const normalizeColor = (color) => {
                        if (!color || color === 'transparent' || color === 'rgba(0, 0, 0, 0)') return null;
                        return color;
                    };

                    document.querySelectorAll('*').forEach(el => {
                        const style = getComputedStyle(el);

                        // Text colors
                        const color = normalizeColor(style.color);
                        if (color) {
                            textColors.set(color, (textColors.get(color) || 0) + 1);
                        }

                        // Background colors
                        const bgColor = normalizeColor(style.backgroundColor);
                        if (bgColor) {
                            bgColors.set(bgColor, (bgColors.get(bgColor) || 0) + 1);
                        }

                        // Border colors
                        const borderColor = normalizeColor(style.borderColor);
                        if (borderColor && style.borderWidth !== '0px') {
                            borderColors.set(borderColor, (borderColors.get(borderColor) || 0) + 1);
                        }

                        // Gradients
                        const bgImage = style.backgroundImage;
                        if (bgImage && bgImage.includes('gradient')) {
                            gradients.push(bgImage);
                        }
                    });

                    return JSON.stringify({
                        textColors: Array.from(textColors.entries())
                            .map(([color, count]) => ({color, count}))
                            .sort((a, b) => b.count - a.count),
                        bgColors: Array.from(bgColors.entries())
                            .map(([color, count]) => ({color, count}))
                            .sort((a, b) => b.count - a.count),
                        borderColors: Array.from(borderColors.entries())
                            .map(([color, count]) => ({color, count}))
                            .sort((a, b) => b.count - a.count),
                        gradients: [...new Set(gradients)]
                    });
                }
            ");

            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonSerializer.Deserialize<ColorData>(json);
                if (data != null)
                {
                    result.TextColors = data.textColors?
                        .Select(c => new ColorUsage
                        {
                            Color = c.color ?? "",
                            Hex = RgbToHex(c.color ?? ""),
                            UsageCount = c.count,
                            Category = "text"
                        })
                        .Where(c => !string.IsNullOrEmpty(c.Color))
                        .ToList() ?? new();

                    result.BackgroundColors = data.bgColors?
                        .Select(c => new ColorUsage
                        {
                            Color = c.color ?? "",
                            Hex = RgbToHex(c.color ?? ""),
                            UsageCount = c.count,
                            Category = "background"
                        })
                        .Where(c => !string.IsNullOrEmpty(c.Color))
                        .ToList() ?? new();

                    result.BorderColors = data.borderColors?
                        .Select(c => new ColorUsage
                        {
                            Color = c.color ?? "",
                            Hex = RgbToHex(c.color ?? ""),
                            UsageCount = c.count,
                            Category = "border"
                        })
                        .Where(c => !string.IsNullOrEmpty(c.Color))
                        .ToList() ?? new();

                    result.Gradients = data.gradients?
                        .Select(g => new GradientDefinition { Value = g ?? "" })
                        .Where(g => !string.IsNullOrEmpty(g.Value))
                        .ToList() ?? new();

                    // Identify primary/secondary colors based on usage
                    var allColors = result.BackgroundColors
                        .Where(c => !IsNeutral(c.Color))
                        .OrderByDescending(c => c.UsageCount)
                        .ToList();

                    if (allColors.Count > 0)
                        result.PrimaryColor = allColors[0];
                    if (allColors.Count > 1)
                        result.SecondaryColor = allColors[1];
                    if (allColors.Count > 2)
                        result.AccentColor = allColors[2];

                    // Build unified palette
                    result.AllColors = result.TextColors
                        .Concat(result.BackgroundColors)
                        .Concat(result.BorderColors)
                        .GroupBy(c => c.Hex)
                        .Select(g => new ColorUsage
                        {
                            Color = g.First().Color,
                            Hex = g.Key,
                            UsageCount = g.Sum(c => c.UsageCount),
                            Category = string.Join(", ", g.Select(c => c.Category).Distinct())
                        })
                        .OrderByDescending(c => c.UsageCount)
                        .Take(50)
                        .ToList();
                }
            }
        }
        catch
        {
            // Silently fail
        }

        return result;
    }

    private string RgbToHex(string rgb)
    {
        if (string.IsNullOrEmpty(rgb)) return "";
        if (rgb.StartsWith("#")) return rgb;

        var match = Regex.Match(rgb, @"rgba?\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)");
        if (!match.Success) return rgb;

        var r = int.Parse(match.Groups[1].Value);
        var g = int.Parse(match.Groups[2].Value);
        var b = int.Parse(match.Groups[3].Value);

        return $"#{r:X2}{g:X2}{b:X2}".ToLower();
    }

    private bool IsNeutral(string color)
    {
        if (string.IsNullOrEmpty(color)) return true;

        var match = Regex.Match(color, @"rgba?\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)");
        if (!match.Success) return false;

        var r = int.Parse(match.Groups[1].Value);
        var g = int.Parse(match.Groups[2].Value);
        var b = int.Parse(match.Groups[3].Value);

        // Check if grayscale (r ≈ g ≈ b)
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));

        return max - min < 20; // Low saturation = neutral
    }

    private class ColorData
    {
        public List<ColorEntry>? textColors { get; set; }
        public List<ColorEntry>? bgColors { get; set; }
        public List<ColorEntry>? borderColors { get; set; }
        public List<string>? gradients { get; set; }
    }

    private class ColorEntry
    {
        public string? color { get; set; }
        public int count { get; set; }
    }
}

public class ColorPalette
{
    public List<ColorUsage> AllColors { get; set; } = new();
    public ColorUsage? PrimaryColor { get; set; }
    public ColorUsage? SecondaryColor { get; set; }
    public ColorUsage? AccentColor { get; set; }
    public List<ColorUsage> TextColors { get; set; } = new();
    public List<ColorUsage> BackgroundColors { get; set; } = new();
    public List<ColorUsage> BorderColors { get; set; } = new();
    public List<GradientDefinition> Gradients { get; set; } = new();
}

public class ColorUsage
{
    public string Color { get; set; } = "";
    public string Hex { get; set; } = "";
    public int UsageCount { get; set; }
    public string Category { get; set; } = "";
}

public class GradientDefinition
{
    public string Value { get; set; } = "";
}
