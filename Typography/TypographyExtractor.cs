namespace SiteRipper.Typography;

using Microsoft.Playwright;
using System.Text.Json;
using System.Text.RegularExpressions;

public class TypographyExtractor
{
    public async Task<TypographySystem> ExtractAsync(IPage page)
    {
        var result = new TypographySystem();

        try
        {
            var json = await page.EvaluateAsync<string>(@"() => {
                const fonts = new Map();
                const weights = new Map();
                const lineHeights = new Set();
                const letterSpacings = new Set();
                const textStyles = [];

                const elements = document.querySelectorAll('*');

                elements.forEach(el => {
                    const style = getComputedStyle(el);
                    const text = el.textContent?.trim();

                    if (!text || style.display === 'none' || style.visibility === 'hidden') return;

                    const fontFamily = style.fontFamily;
                    const fontWeight = style.fontWeight;
                    const fontSize = style.fontSize;
                    const lineHeight = style.lineHeight;
                    const letterSpacing = style.letterSpacing;

                    if (fontFamily) {
                        const primary = fontFamily.split(',')[0].trim().replace(/[""']/g, '');
                        fonts.set(primary, (fonts.get(primary) || 0) + 1);
                    }

                    if (fontWeight) {
                        weights.set(fontWeight, (weights.get(fontWeight) || 0) + 1);
                    }

                    if (lineHeight && lineHeight !== 'normal') {
                        lineHeights.add(lineHeight);
                    }

                    if (letterSpacing && letterSpacing !== 'normal' && letterSpacing !== '0px') {
                        letterSpacings.add(letterSpacing);
                    }

                    const tag = el.tagName.toLowerCase();
                    if (['h1','h2','h3','h4','h5','h6','p','span','a','button','label'].includes(tag)) {
                        textStyles.push({
                            tag: tag,
                            fontFamily: fontFamily?.split(',')[0].trim().replace(/[""']/g, ''),
                            fontSize: fontSize,
                            fontWeight: fontWeight,
                            lineHeight: lineHeight,
                            letterSpacing: letterSpacing,
                            color: style.color
                        });
                    }
                });

                const fontFaces = [];
                for (const sheet of document.styleSheets) {
                    try {
                        for (const rule of sheet.cssRules || []) {
                            if (rule.type === CSSRule.FONT_FACE_RULE) {
                                const style = rule.style;
                                fontFaces.push({
                                    family: style.getPropertyValue('font-family').replace(/[""']/g, ''),
                                    src: style.getPropertyValue('src'),
                                    weight: style.getPropertyValue('font-weight') || '400',
                                    style: style.getPropertyValue('font-style') || 'normal'
                                });
                            }
                        }
                    } catch(e) {}
                }

                return JSON.stringify({
                    fonts: Array.from(fonts.entries()).map(([name, count]) => ({name, count})),
                    weights: Array.from(weights.entries()).map(([weight, count]) => ({weight, count})),
                    lineHeights: Array.from(lineHeights),
                    letterSpacings: Array.from(letterSpacings),
                    textStyles: textStyles.slice(0, 100),
                    fontFaces: fontFaces
                });
            }");

            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonSerializer.Deserialize<TypographyData>(json);
                if (data != null)
                {
                    result.FontFamilies = data.fonts?
                        .OrderByDescending(f => f.count)
                        .Select(f => new FontFamilyInfo
                        {
                            Name = f.name ?? "",
                            UsageCount = f.count
                        })
                        .ToList() ?? new();

                    result.FontWeights = data.weights?
                        .OrderByDescending(w => w.count)
                        .Select(w => new FontWeightInfo
                        {
                            Weight = w.weight ?? "400",
                            UsageCount = w.count
                        })
                        .ToList() ?? new();

                    result.LineHeights = data.lineHeights?
                        .Where(lh => !string.IsNullOrEmpty(lh))
                        .Distinct()
                        .OrderBy(lh => ParseNumeric(lh))
                        .ToList() ?? new();

                    result.LetterSpacings = data.letterSpacings?
                        .Where(ls => !string.IsNullOrEmpty(ls))
                        .Distinct()
                        .OrderBy(ls => ParseNumeric(ls))
                        .ToList() ?? new();

                    result.FontFaces = data.fontFaces?
                        .Select(ff => new FontFaceInfo
                        {
                            Family = ff.family ?? "",
                            Source = ExtractFontUrl(ff.src ?? ""),
                            Weight = ff.weight ?? "400",
                            Style = ff.style ?? "normal"
                        })
                        .Where(ff => !string.IsNullOrEmpty(ff.Family))
                        .ToList() ?? new();

                    result.TextStyles = BuildTextStyleScale(data.textStyles);
                }
            }
        }
        catch
        {
        }

        return result;
    }

    private double ParseNumeric(string value)
    {
        var match = Regex.Match(value, @"[\d.]+");
        return match.Success ? double.Parse(match.Value) : 0;
    }

    private string ExtractFontUrl(string src)
    {
        var match = Regex.Match(src, @"url\(([^)]+)\)");
        return match.Success ? match.Groups[1].Value.Trim('"', '\'') : src;
    }

    private List<TextStyleInfo> BuildTextStyleScale(List<TextStyleData>? styles)
    {
        if (styles == null || styles.Count == 0) return new();

        var grouped = styles
            .Where(s => s.fontSize != null)
            .GroupBy(s => s.tag)
            .Select(g => new TextStyleInfo
            {
                Tag = g.Key ?? "",
                FontFamily = g.First().fontFamily ?? "",
                FontSize = g.First().fontSize ?? "",
                FontWeight = g.First().fontWeight ?? "400",
                LineHeight = g.First().lineHeight ?? "normal",
                LetterSpacing = g.First().letterSpacing ?? "normal",
                Color = g.First().color ?? "",
                SampleCount = g.Count()
            })
            .OrderBy(s => GetTagPriority(s.Tag))
            .ToList();

        return grouped;
    }

    private int GetTagPriority(string tag) => tag switch
    {
        "h1" => 1, "h2" => 2, "h3" => 3, "h4" => 4, "h5" => 5, "h6" => 6,
        "p" => 7, "span" => 8, "a" => 9, "button" => 10, "label" => 11,
        _ => 99
    };

    private class TypographyData
    {
        public List<FontData>? fonts { get; set; }
        public List<WeightData>? weights { get; set; }
        public List<string>? lineHeights { get; set; }
        public List<string>? letterSpacings { get; set; }
        public List<TextStyleData>? textStyles { get; set; }
        public List<FontFaceData>? fontFaces { get; set; }
    }

    private class FontData { public string? name { get; set; } public int count { get; set; } }
    private class WeightData { public string? weight { get; set; } public int count { get; set; } }
    private class FontFaceData { public string? family { get; set; } public string? src { get; set; } public string? weight { get; set; } public string? style { get; set; } }
    private class TextStyleData
    {
        public string? tag { get; set; }
        public string? fontFamily { get; set; }
        public string? fontSize { get; set; }
        public string? fontWeight { get; set; }
        public string? lineHeight { get; set; }
        public string? letterSpacing { get; set; }
        public string? color { get; set; }
    }
}

public class TypographySystem
{
    public List<FontFamilyInfo> FontFamilies { get; set; } = new();
    public List<FontWeightInfo> FontWeights { get; set; } = new();
    public List<string> LineHeights { get; set; } = new();
    public List<string> LetterSpacings { get; set; } = new();
    public List<FontFaceInfo> FontFaces { get; set; } = new();
    public List<TextStyleInfo> TextStyles { get; set; } = new();
}

public class FontFamilyInfo
{
    public string Name { get; set; } = "";
    public int UsageCount { get; set; }
}

public class FontWeightInfo
{
    public string Weight { get; set; } = "400";
    public int UsageCount { get; set; }
}

public class FontFaceInfo
{
    public string Family { get; set; } = "";
    public string Source { get; set; } = "";
    public string Weight { get; set; } = "400";
    public string Style { get; set; } = "normal";
}

public class TextStyleInfo
{
    public string Tag { get; set; } = "";
    public string FontFamily { get; set; } = "";
    public string FontSize { get; set; } = "";
    public string FontWeight { get; set; } = "";
    public string LineHeight { get; set; } = "";
    public string LetterSpacing { get; set; } = "";
    public string Color { get; set; } = "";
    public int SampleCount { get; set; }
}
