namespace SiteRipper.Services;

using System.Text.RegularExpressions;
using SiteRipper.Orchestration;
using SiteRipper.Css;

/// <summary>
/// Maps raw CSS values to design token names for AI-friendly output
/// </summary>
public class DesignTokenMapper
{
    private Dictionary<string, string> _colorTokens = new();    // hex → token name
    private Dictionary<string, string> _spacingTokens = new();  // "16px" → "md"
    private Dictionary<string, string> _fontTokens = new();     // "Inter" → "body"
    private Dictionary<string, string> _radiusTokens = new();   // "8px" → "md"

    // Generated tokens for output (group by token name, take first value)
    public Dictionary<string, string> ColorTokens => _colorTokens
        .GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key);
    public Dictionary<string, string> SpacingTokens => _spacingTokens
        .GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key);
    public Dictionary<string, string> FontTokens => _fontTokens
        .GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key);
    public Dictionary<string, string> RadiusTokens => _radiusTokens
        .GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key);

    /// <summary>
    /// Build token mappings from analysis result
    /// </summary>
    public void BuildFromAnalysis(ComponentAnalysisResult result)
    {
        BuildColorTokens(result);
        BuildSpacingTokens(result);
        BuildFontTokens(result);
        BuildRadiusTokens(result);
    }

    private void BuildColorTokens(ComponentAnalysisResult result)
    {
        // First, add CSS custom properties that look like colors
        foreach (var cssVar in result.CssVariables)
        {
            if (IsColorValue(cssVar.Value))
            {
                var hex = NormalizeToHex(cssVar.Value);
                if (!string.IsNullOrEmpty(hex) && !_colorTokens.ContainsKey(hex))
                {
                    // Use variable name without -- prefix
                    var tokenName = cssVar.Name.TrimStart('-');
                    _colorTokens[hex] = tokenName;
                }
            }
        }

        // Then add palette colors with semantic names
        if (result.ColorPalette != null)
        {
            if (result.ColorPalette.PrimaryColor != null)
            {
                var hex = NormalizeToHex(result.ColorPalette.PrimaryColor.Hex);
                if (!string.IsNullOrEmpty(hex) && !_colorTokens.ContainsKey(hex))
                    _colorTokens[hex] = "primary";
            }

            if (result.ColorPalette.SecondaryColor != null)
            {
                var hex = NormalizeToHex(result.ColorPalette.SecondaryColor.Hex);
                if (!string.IsNullOrEmpty(hex) && !_colorTokens.ContainsKey(hex))
                    _colorTokens[hex] = "secondary";
            }

            if (result.ColorPalette.AccentColor != null)
            {
                var hex = NormalizeToHex(result.ColorPalette.AccentColor.Hex);
                if (!string.IsNullOrEmpty(hex) && !_colorTokens.ContainsKey(hex))
                    _colorTokens[hex] = "accent";
            }
        }
    }

    private void BuildSpacingTokens(ComponentAnalysisResult result)
    {
        // Standard spacing scale
        var spacingScale = new Dictionary<string, string>
        {
            { "0px", "0" }, { "2px", "0.5" }, { "4px", "1" }, { "6px", "1.5" },
            { "8px", "2" }, { "10px", "2.5" }, { "12px", "3" }, { "14px", "3.5" },
            { "16px", "4" }, { "20px", "5" }, { "24px", "6" }, { "28px", "7" },
            { "32px", "8" }, { "36px", "9" }, { "40px", "10" }, { "44px", "11" },
            { "48px", "12" }, { "56px", "14" }, { "64px", "16" }, { "80px", "20" },
            { "96px", "24" }, { "112px", "28" }, { "128px", "32" }
        };

        foreach (var (px, scale) in spacingScale)
        {
            _spacingTokens[px] = scale;
        }

        // Also check CSS variables for spacing
        foreach (var cssVar in result.CssVariables)
        {
            if (cssVar.Name.Contains("spacing") || cssVar.Name.Contains("space") || cssVar.Name.Contains("gap"))
            {
                if (!_spacingTokens.ContainsKey(cssVar.Value))
                {
                    _spacingTokens[cssVar.Value] = cssVar.Name.TrimStart('-');
                }
            }
        }
    }

    private void BuildFontTokens(ComponentAnalysisResult result)
    {
        if (result.Typography == null) return;

        var fontFamilies = result.Typography.FontFamilies
            .OrderByDescending(f => f.UsageCount)
            .ToList();

        // Primary font = body, secondary = heading
        if (fontFamilies.Count > 0)
        {
            var primary = ExtractFontName(fontFamilies[0].Name);
            _fontTokens[primary] = "body";
        }

        if (fontFamilies.Count > 1)
        {
            var secondary = ExtractFontName(fontFamilies[1].Name);
            if (!_fontTokens.ContainsKey(secondary))
                _fontTokens[secondary] = "heading";
        }

        // Check for monospace fonts
        foreach (var font in fontFamilies)
        {
            var name = font.Name.ToLower();
            if (name.Contains("mono") || name.Contains("code") || name.Contains("consolas"))
            {
                var extracted = ExtractFontName(font.Name);
                if (!_fontTokens.ContainsKey(extracted))
                    _fontTokens[extracted] = "mono";
            }
        }
    }

    private void BuildRadiusTokens(ComponentAnalysisResult result)
    {
        // Standard radius scale (unique names)
        _radiusTokens["0px"] = "none";
        _radiusTokens["2px"] = "sm";
        _radiusTokens["4px"] = "md";
        _radiusTokens["6px"] = "base";
        _radiusTokens["8px"] = "lg";
        _radiusTokens["12px"] = "xl";
        _radiusTokens["16px"] = "2xl";
        _radiusTokens["24px"] = "3xl";
        _radiusTokens["9999px"] = "full";
        _radiusTokens["50%"] = "round";
    }

    /// <summary>
    /// Resolve a raw color value to its token name
    /// </summary>
    public string? ResolveColor(string? rawValue)
    {
        if (string.IsNullOrEmpty(rawValue)) return null;

        var hex = NormalizeToHex(rawValue);
        if (string.IsNullOrEmpty(hex)) return null;

        return _colorTokens.GetValueOrDefault(hex);
    }

    /// <summary>
    /// Resolve a spacing value to its token name
    /// </summary>
    public string? ResolveSpacing(string? rawValue)
    {
        if (string.IsNullOrEmpty(rawValue)) return null;
        return _spacingTokens.GetValueOrDefault(rawValue.Trim());
    }

    /// <summary>
    /// Resolve a font family to its token name
    /// </summary>
    public string? ResolveFont(string? rawValue)
    {
        if (string.IsNullOrEmpty(rawValue)) return null;

        var fontName = ExtractFontName(rawValue);
        return _fontTokens.GetValueOrDefault(fontName);
    }

    /// <summary>
    /// Resolve a border radius to its token name
    /// </summary>
    public string? ResolveRadius(string? rawValue)
    {
        if (string.IsNullOrEmpty(rawValue)) return null;
        return _radiusTokens.GetValueOrDefault(rawValue.Trim());
    }

    /// <summary>
    /// Create a token reference object: { value: "...", token: "..." }
    /// </summary>
    public object CreateTokenRef(string? value, string? token)
    {
        return new { value, token };
    }

    // Helper: Check if a value looks like a color
    private bool IsColorValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return value.StartsWith("#") ||
               value.StartsWith("rgb") ||
               value.StartsWith("hsl") ||
               Regex.IsMatch(value, @"^[a-z]+$", RegexOptions.IgnoreCase);  // named colors
    }

    // Helper: Normalize any color format to lowercase hex
    private string? NormalizeToHex(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        value = value.Trim().ToLower();

        // Already hex
        if (value.StartsWith("#"))
        {
            // Expand short hex (#fff → #ffffff)
            if (value.Length == 4)
                return $"#{value[1]}{value[1]}{value[2]}{value[2]}{value[3]}{value[3]}";
            return value.Length >= 7 ? value[..7] : value;  // Strip alpha if present
        }

        // RGB/RGBA
        var rgbMatch = Regex.Match(value, @"rgba?\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)");
        if (rgbMatch.Success)
        {
            var r = int.Parse(rgbMatch.Groups[1].Value);
            var g = int.Parse(rgbMatch.Groups[2].Value);
            var b = int.Parse(rgbMatch.Groups[3].Value);
            return $"#{r:x2}{g:x2}{b:x2}";
        }

        return null;
    }

    // Helper: Extract primary font name from font-family string
    private string ExtractFontName(string fontFamily)
    {
        // Take first font, remove quotes
        var first = fontFamily.Split(',')[0].Trim();
        return first.Trim('"', '\'', ' ');
    }
}
