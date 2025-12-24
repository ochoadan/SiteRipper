namespace SiteRipper.Accessibility;

using Microsoft.Playwright;
using System.Text.Json;

public class A11yAuditor
{
    public async Task<AccessibilityReport> AuditAsync(IPage page)
    {
        var result = new AccessibilityReport();

        try
        {
            var json = await page.EvaluateAsync<string>(@"
                () => {
                    // Heading hierarchy
                    const headings = [];
                    document.querySelectorAll('h1, h2, h3, h4, h5, h6').forEach(h => {
                        headings.push({
                            level: parseInt(h.tagName[1]),
                            text: h.textContent?.trim().substring(0, 100) || '',
                            id: h.id || null
                        });
                    });

                    // Landmarks
                    const landmarks = [];
                    const landmarkRoles = ['banner', 'main', 'navigation', 'contentinfo', 'complementary', 'search', 'form', 'region'];
                    const landmarkTags = { header: 'banner', main: 'main', nav: 'navigation', footer: 'contentinfo', aside: 'complementary' };

                    // Check semantic tags
                    Object.entries(landmarkTags).forEach(([tag, role]) => {
                        document.querySelectorAll(tag).forEach(el => {
                            landmarks.push({
                                role: role,
                                tag: tag,
                                label: el.getAttribute('aria-label') || el.getAttribute('aria-labelledby') || null
                            });
                        });
                    });

                    // Check role attributes
                    landmarkRoles.forEach(role => {
                        document.querySelectorAll(`[role='${role}']`).forEach(el => {
                            if (!Object.values(landmarkTags).includes(role) || el.tagName.toLowerCase() !== Object.keys(landmarkTags).find(k => landmarkTags[k] === role)) {
                                landmarks.push({
                                    role: role,
                                    tag: el.tagName.toLowerCase(),
                                    label: el.getAttribute('aria-label') || null
                                });
                            }
                        });
                    });

                    // Images without alt
                    const imagesWithoutAlt = [];
                    document.querySelectorAll('img').forEach(img => {
                        if (!img.alt && !img.getAttribute('role')?.includes('presentation')) {
                            imagesWithoutAlt.push({
                                src: img.src?.substring(0, 200),
                                width: img.width,
                                height: img.height
                            });
                        }
                    });

                    // Interactive elements
                    const focusableElements = [];
                    const focusable = document.querySelectorAll('a, button, input, select, textarea, [tabindex]');
                    focusable.forEach((el, i) => {
                        if (i < 50) { // Limit
                            const tabIndex = el.tabIndex;
                            focusableElements.push({
                                tag: el.tagName.toLowerCase(),
                                tabIndex: tabIndex,
                                hasLabel: !!(el.getAttribute('aria-label') || el.closest('label') || document.querySelector(`label[for='${el.id}']`)),
                                text: el.textContent?.trim().substring(0, 50) || el.value?.substring(0, 50) || ''
                            });
                        }
                    });

                    // ARIA issues
                    const ariaIssues = [];

                    // Check for invalid aria attributes
                    document.querySelectorAll('[aria-labelledby]').forEach(el => {
                        const id = el.getAttribute('aria-labelledby');
                        if (id && !document.getElementById(id)) {
                            ariaIssues.push({
                                type: 'missing-labelledby-target',
                                element: el.tagName.toLowerCase(),
                                message: `aria-labelledby references non-existent id: ${id}`
                            });
                        }
                    });

                    // Check for missing button text
                    document.querySelectorAll('button').forEach(btn => {
                        if (!btn.textContent?.trim() && !btn.getAttribute('aria-label') && !btn.querySelector('img[alt]')) {
                            ariaIssues.push({
                                type: 'empty-button',
                                element: 'button',
                                message: 'Button has no accessible name'
                            });
                        }
                    });

                    // Check for missing link text
                    document.querySelectorAll('a[href]').forEach(a => {
                        if (!a.textContent?.trim() && !a.getAttribute('aria-label') && !a.querySelector('img[alt]')) {
                            ariaIssues.push({
                                type: 'empty-link',
                                element: 'a',
                                message: 'Link has no accessible name'
                            });
                        }
                    });

                    // Form labels
                    const formIssues = [];
                    document.querySelectorAll('input, select, textarea').forEach(input => {
                        if (input.type === 'hidden' || input.type === 'submit' || input.type === 'button') return;
                        const hasLabel = input.id && document.querySelector(`label[for='${input.id}']`);
                        const hasAriaLabel = input.getAttribute('aria-label') || input.getAttribute('aria-labelledby');
                        const hasPlaceholder = input.placeholder;

                        if (!hasLabel && !hasAriaLabel) {
                            formIssues.push({
                                type: input.type || input.tagName.toLowerCase(),
                                name: input.name || null,
                                hasPlaceholder: !!hasPlaceholder
                            });
                        }
                    });

                    // Language
                    const htmlLang = document.documentElement.lang || null;

                    // Skip links
                    const hasSkipLink = !!document.querySelector('a[href^=""#""][class*=skip], a[href^=""#main""], a[href^=""#content""]');

                    return JSON.stringify({
                        headings,
                        landmarks,
                        imagesWithoutAlt,
                        focusableElements,
                        ariaIssues,
                        formIssues,
                        htmlLang,
                        hasSkipLink
                    });
                }
            ");

            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonSerializer.Deserialize<A11yData>(json);
                if (data != null)
                {
                    // Headings
                    result.Headings = (data.headings ?? new())
                        .Select(h => new HeadingInfo
                        {
                            Level = h.level,
                            Text = h.text ?? "",
                            Id = h.id
                        })
                        .ToList();

                    // Check heading hierarchy
                    result.HeadingIssues = ValidateHeadingHierarchy(result.Headings);

                    // Landmarks
                    result.Landmarks = (data.landmarks ?? new())
                        .Select(l => new LandmarkInfo
                        {
                            Role = l.role ?? "",
                            Tag = l.tag ?? "",
                            Label = l.label
                        })
                        .ToList();

                    // Images without alt
                    result.ImagesWithoutAlt = (data.imagesWithoutAlt ?? new())
                        .Select(i => new ImageIssue
                        {
                            Src = i.src ?? "",
                            Width = i.width,
                            Height = i.height
                        })
                        .ToList();

                    // ARIA issues
                    result.AriaIssues = (data.ariaIssues ?? new())
                        .Select(a => new AriaIssue
                        {
                            Type = a.type ?? "",
                            Element = a.element ?? "",
                            Message = a.message ?? ""
                        })
                        .ToList();

                    // Form issues
                    result.FormIssues = (data.formIssues ?? new())
                        .Select(f => new FormLabelIssue
                        {
                            InputType = f.type ?? "",
                            Name = f.name,
                            HasPlaceholder = f.hasPlaceholder
                        })
                        .ToList();

                    result.Language = data.htmlLang ?? "";
                    result.HasSkipLink = data.hasSkipLink;

                    // Calculate score
                    result.Score = CalculateScore(result);
                }
            }
        }
        catch
        {
            // Silently fail
        }

        return result;
    }

    private List<string> ValidateHeadingHierarchy(List<HeadingInfo> headings)
    {
        var issues = new List<string>();

        // Check for missing h1
        if (!headings.Any(h => h.Level == 1))
            issues.Add("Missing h1 heading");

        // Check for multiple h1
        var h1Count = headings.Count(h => h.Level == 1);
        if (h1Count > 1)
            issues.Add($"Multiple h1 headings found ({h1Count})");

        // Check for skipped levels
        var previousLevel = 0;
        foreach (var h in headings)
        {
            if (previousLevel > 0 && h.Level > previousLevel + 1)
            {
                issues.Add($"Skipped heading level: h{previousLevel} to h{h.Level}");
            }
            previousLevel = h.Level;
        }

        return issues;
    }

    private int CalculateScore(AccessibilityReport report)
    {
        int score = 100;

        // Deductions
        score -= report.ImagesWithoutAlt.Count * 5;
        score -= report.AriaIssues.Count * 3;
        score -= report.FormIssues.Count * 5;
        score -= report.HeadingIssues.Count * 5;

        if (string.IsNullOrEmpty(report.Language)) score -= 10;
        if (!report.HasSkipLink) score -= 5;
        if (!report.Landmarks.Any(l => l.Role == "main")) score -= 10;
        if (!report.Landmarks.Any(l => l.Role == "navigation")) score -= 5;

        return Math.Max(0, Math.Min(100, score));
    }

    private class A11yData
    {
        public List<HeadingData>? headings { get; set; }
        public List<LandmarkData>? landmarks { get; set; }
        public List<ImageData>? imagesWithoutAlt { get; set; }
        public List<FocusableData>? focusableElements { get; set; }
        public List<AriaIssueData>? ariaIssues { get; set; }
        public List<FormIssueData>? formIssues { get; set; }
        public string? htmlLang { get; set; }
        public bool hasSkipLink { get; set; }
    }

    private class HeadingData { public int level { get; set; } public string? text { get; set; } public string? id { get; set; } }
    private class LandmarkData { public string? role { get; set; } public string? tag { get; set; } public string? label { get; set; } }
    private class ImageData { public string? src { get; set; } public int width { get; set; } public int height { get; set; } }
    private class FocusableData { public string? tag { get; set; } public int tabIndex { get; set; } public bool hasLabel { get; set; } public string? text { get; set; } }
    private class AriaIssueData { public string? type { get; set; } public string? element { get; set; } public string? message { get; set; } }
    private class FormIssueData { public string? type { get; set; } public string? name { get; set; } public bool hasPlaceholder { get; set; } }
}

public class AccessibilityReport
{
    public int Score { get; set; }
    public string Language { get; set; } = "";
    public bool HasSkipLink { get; set; }
    public List<HeadingInfo> Headings { get; set; } = new();
    public List<string> HeadingIssues { get; set; } = new();
    public List<LandmarkInfo> Landmarks { get; set; } = new();
    public List<ImageIssue> ImagesWithoutAlt { get; set; } = new();
    public List<AriaIssue> AriaIssues { get; set; } = new();
    public List<FormLabelIssue> FormIssues { get; set; } = new();
}

public class HeadingInfo
{
    public int Level { get; set; }
    public string Text { get; set; } = "";
    public string? Id { get; set; }
}

public class LandmarkInfo
{
    public string Role { get; set; } = "";
    public string Tag { get; set; } = "";
    public string? Label { get; set; }
}

public class ImageIssue
{
    public string Src { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
}

public class AriaIssue
{
    public string Type { get; set; } = "";
    public string Element { get; set; } = "";
    public string Message { get; set; } = "";
}

public class FormLabelIssue
{
    public string InputType { get; set; } = "";
    public string? Name { get; set; }
    public bool HasPlaceholder { get; set; }
}
