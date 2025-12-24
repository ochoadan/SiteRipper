namespace SiteRipper.Css;

using Microsoft.Playwright;
using System.Text.RegularExpressions;

public class CssVariableExtractor
{
    public async Task<List<CssCustomProperty>> ExtractFromPageAsync(IPage page)
    {
        var variables = new List<CssCustomProperty>();

        try
        {
            var json = await page.EvaluateAsync<string>(@"
                () => {
                    const vars = [];
                    const seen = new Set();

                    // Get from :root and html rules
                    for (const sheet of document.styleSheets) {
                        try {
                            for (const rule of sheet.cssRules || []) {
                                if (rule.type === CSSRule.STYLE_RULE) {
                                    if (rule.selectorText === ':root' ||
                                        rule.selectorText === 'html' ||
                                        rule.selectorText === ':root, :host') {
                                        for (const prop of rule.style) {
                                            if (prop.startsWith('--') && !seen.has(prop)) {
                                                seen.add(prop);
                                                vars.push({
                                                    name: prop,
                                                    value: rule.style.getPropertyValue(prop).trim(),
                                                    source: sheet.href || 'inline'
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                        } catch (e) {
                            // CORS blocked stylesheets
                        }
                    }

                    // Also get computed values for variables we found
                    const rootStyles = getComputedStyle(document.documentElement);
                    for (const v of vars) {
                        const computed = rootStyles.getPropertyValue(v.name);
                        if (computed && computed !== v.value) {
                            v.computedValue = computed.trim();
                        }
                    }

                    return JSON.stringify(vars);
                }
            ");

            if (!string.IsNullOrEmpty(json))
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<CssVarData>>(json);
                if (parsed != null)
                {
                    variables = parsed.Select(v => new CssCustomProperty
                    {
                        Name = v.name ?? "",
                        Value = v.value ?? "",
                        ComputedValue = v.computedValue,
                        Source = v.source ?? "inline"
                    }).ToList();
                }
            }
        }
        catch
        {
            // Silently fail
        }

        return variables;
    }

    public async Task<(List<KeyframeAnimation> Keyframes, List<MediaQueryInfo> MediaQueries)> ExtractRulesFromPageAsync(IPage page)
    {
        var keyframes = new List<KeyframeAnimation>();
        var mediaQueries = new List<MediaQueryInfo>();

        try
        {
            var json = await page.EvaluateAsync<string>(@"
                () => {
                    const keyframes = [];
                    const mediaQueries = new Map();
                    const seenKeyframes = new Set();

                    for (const sheet of document.styleSheets) {
                        try {
                            const source = sheet.href || 'inline';
                            const rules = sheet.cssRules || [];

                            for (const rule of rules) {
                                // Extract @keyframes
                                if (rule.type === CSSRule.KEYFRAMES_RULE) {
                                    const name = rule.name;
                                    if (!seenKeyframes.has(name)) {
                                        seenKeyframes.add(name);
                                        keyframes.push({
                                            name: name,
                                            cssText: rule.cssText,
                                            source: source
                                        });
                                    }
                                }

                                // Extract @media
                                if (rule.type === CSSRule.MEDIA_RULE) {
                                    const query = rule.conditionText || rule.media.mediaText;
                                    if (!mediaQueries.has(query)) {
                                        mediaQueries.set(query, {
                                            query: query,
                                            ruleCount: 1,
                                            source: source
                                        });
                                    } else {
                                        mediaQueries.get(query).ruleCount++;
                                    }
                                }
                            }
                        } catch (e) {
                            // CORS blocked stylesheets - skip
                        }
                    }

                    return JSON.stringify({
                        keyframes: keyframes,
                        mediaQueries: Array.from(mediaQueries.values())
                    });
                }
            ");

            if (!string.IsNullOrEmpty(json))
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<BrowserCssRules>(json);
                if (parsed != null)
                {
                    keyframes = parsed.keyframes?.Select(k => new KeyframeAnimation
                    {
                        Name = k.name ?? "",
                        RawCss = k.cssText ?? "",
                        Source = k.source ?? "inline"
                    }).ToList() ?? new();

                    mediaQueries = parsed.mediaQueries?.Select(m =>
                    {
                        var mq = new MediaQueryInfo
                        {
                            Query = m.query ?? "",
                            RuleCount = m.ruleCount,
                            Source = m.source ?? "inline"
                        };

                        // Parse min/max width
                        var minMatch = Regex.Match(mq.Query, @"min-width:\s*([\d.]+)(px|em|rem)?");
                        if (minMatch.Success)
                        {
                            mq.MinWidth = ParsePxValue(minMatch);
                            mq.BreakpointName = InferBreakpointName(mq.MinWidth, true);
                        }

                        var maxMatch = Regex.Match(mq.Query, @"max-width:\s*([\d.]+)(px|em|rem)?");
                        if (maxMatch.Success)
                        {
                            mq.MaxWidth = ParsePxValue(maxMatch);
                            if (mq.BreakpointName == null)
                                mq.BreakpointName = InferBreakpointName(mq.MaxWidth, false);
                        }

                        return mq;
                    }).ToList() ?? new();
                }
            }
        }
        catch
        {
            // Silently fail
        }

        return (keyframes, mediaQueries);
    }

    public List<CssCustomProperty> ExtractFromCssContent(string cssContent, string sourceName)
    {
        var variables = new List<CssCustomProperty>();

        // Find :root, html, or :host blocks
        var rootBlockPattern = @"(?::root|html|:host)\s*\{([^}]+)\}";
        var matches = Regex.Matches(cssContent, rootBlockPattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var block = match.Groups[1].Value;

            // Find custom properties
            var propPattern = @"(--[\w-]+)\s*:\s*([^;]+);?";
            var propMatches = Regex.Matches(block, propPattern);

            foreach (Match propMatch in propMatches)
            {
                var name = propMatch.Groups[1].Value.Trim();
                var value = propMatch.Groups[2].Value.Trim();

                // Check if already exists
                if (!variables.Any(v => v.Name == name))
                {
                    variables.Add(new CssCustomProperty
                    {
                        Name = name,
                        Value = value,
                        Source = sourceName
                    });
                }
            }
        }

        return variables;
    }

    public List<KeyframeAnimation> ExtractKeyframes(string cssContent, string sourceName)
    {
        var keyframes = new List<KeyframeAnimation>();

        // Pattern for multi-line keyframes (handles nested braces)
        var fullPattern = @"@keyframes\s+([\w-]+)\s*\{((?:[^{}]|\{[^{}]*\})*)\}";

        var matches = Regex.Matches(cssContent, fullPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value.Trim();
            var body = match.Groups[2].Value.Trim();

            keyframes.Add(new KeyframeAnimation
            {
                Name = name,
                RawCss = match.Value,
                Source = sourceName
            });
        }

        return keyframes;
    }

    public List<MediaQueryInfo> ExtractMediaQueries(string cssContent, string sourceName)
    {
        var queries = new Dictionary<string, MediaQueryInfo>();

        var pattern = @"@media\s+([^{]+)\s*\{";
        var matches = Regex.Matches(cssContent, pattern);

        foreach (Match match in matches)
        {
            var query = match.Groups[1].Value.Trim();

            if (!queries.ContainsKey(query))
            {
                queries[query] = new MediaQueryInfo
                {
                    Query = query,
                    Source = sourceName,
                    RuleCount = 0
                };

                // Try to parse min/max width
                var minMatch = Regex.Match(query, @"min-width:\s*([\d.]+)(px|em|rem)?");
                if (minMatch.Success)
                {
                    queries[query].MinWidth = ParsePxValue(minMatch);
                    queries[query].BreakpointName = InferBreakpointName(queries[query].MinWidth, true);
                }

                var maxMatch = Regex.Match(query, @"max-width:\s*([\d.]+)(px|em|rem)?");
                if (maxMatch.Success)
                {
                    queries[query].MaxWidth = ParsePxValue(maxMatch);
                    if (queries[query].BreakpointName == null)
                        queries[query].BreakpointName = InferBreakpointName(queries[query].MaxWidth, false);
                }
            }

            queries[query].RuleCount++;
        }

        return queries.Values
            .OrderBy(q => q.MinWidth ?? q.MaxWidth ?? 0)
            .ToList();
    }

    private int? ParsePxValue(Match match)
    {
        if (!double.TryParse(match.Groups[1].Value, out var value))
            return null;

        var unit = match.Groups[2].Value;
        return unit switch
        {
            "em" => (int)(value * 16),
            "rem" => (int)(value * 16),
            _ => (int)value
        };
    }

    private string? InferBreakpointName(int? width, bool isMin)
    {
        if (width == null) return null;

        if (isMin)
        {
            return width switch
            {
                >= 1536 => "2xl",
                >= 1280 => "xl",
                >= 1024 => "lg",
                >= 768 => "md",
                >= 640 => "sm",
                >= 480 => "xs",
                _ => null
            };
        }
        else
        {
            return width switch
            {
                <= 480 => "xs",
                <= 640 => "sm",
                <= 768 => "md",
                <= 1024 => "lg",
                <= 1280 => "xl",
                _ => null
            };
        }
    }

    private class CssVarData
    {
        public string? name { get; set; }
        public string? value { get; set; }
        public string? computedValue { get; set; }
        public string? source { get; set; }
    }

    private class BrowserCssRules
    {
        public List<BrowserKeyframe>? keyframes { get; set; }
        public List<BrowserMediaQuery>? mediaQueries { get; set; }
    }

    private class BrowserKeyframe
    {
        public string? name { get; set; }
        public string? cssText { get; set; }
        public string? source { get; set; }
    }

    private class BrowserMediaQuery
    {
        public string? query { get; set; }
        public int ruleCount { get; set; }
        public string? source { get; set; }
    }
}

public class CssCustomProperty
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string? ComputedValue { get; set; }
    public string Source { get; set; } = "";
    public int UsageCount { get; set; }
}

public class KeyframeAnimation
{
    public string Name { get; set; } = "";
    public string RawCss { get; set; } = "";
    public string Source { get; set; } = "";
}

public class MediaQueryInfo
{
    public string Query { get; set; } = "";
    public int? MinWidth { get; set; }
    public int? MaxWidth { get; set; }
    public string? BreakpointName { get; set; }
    public int RuleCount { get; set; }
    public string Source { get; set; } = "";
}
