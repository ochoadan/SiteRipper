namespace SiteRipper.Assets;

using Microsoft.Playwright;
using System.Text.Json;
using System.Text.RegularExpressions;

public class AssetExtractor
{
    public async Task<ExtractedAssets> ExtractAsync(IPage page, string baseUrl)
    {
        var result = new ExtractedAssets();
        var baseUri = new Uri(baseUrl);

        try
        {
            var json = await page.EvaluateAsync<string>(@"() => {
                const images = [];
                const svgs = [];
                const fonts = [];
                const scripts = [];
                const stylesheets = [];

                document.querySelectorAll('img').forEach(img => {
                    images.push({
                        src: img.src || img.dataset.src || img.dataset.lazySrc,
                        alt: img.alt,
                        width: img.naturalWidth || img.width,
                        height: img.naturalHeight || img.height,
                        loading: img.loading,
                        srcset: img.srcset
                    });
                });

                document.querySelectorAll('*').forEach(el => {
                    const style = getComputedStyle(el);
                    const bg = style.backgroundImage;
                    if (bg && bg !== 'none' && bg.includes('url(')) {
                        const match = bg.match(/url\(([^)]+)\)/);
                        if (match) {
                            images.push({
                                src: match[1].replace(/[""']/g, ''),
                                alt: '',
                                width: 0,
                                height: 0,
                                isBackground: true,
                                element: el.tagName.toLowerCase()
                            });
                        }
                    }
                });

                document.querySelectorAll('svg').forEach(svg => {
                    const rect = svg.getBoundingClientRect();
                    svgs.push({
                        content: svg.outerHTML,
                        width: rect.width,
                        height: rect.height,
                        viewBox: svg.getAttribute('viewBox'),
                        id: svg.id || null,
                        className: svg.className?.baseVal || null
                    });
                });

                for (const sheet of document.styleSheets) {
                    try {
                        for (const rule of sheet.cssRules || []) {
                            if (rule.type === CSSRule.FONT_FACE_RULE) {
                                const src = rule.style.getPropertyValue('src');
                                const urlMatches = src.match(/url\(([^)]+)\)/g);
                                if (urlMatches) {
                                    urlMatches.forEach(u => {
                                        const urlMatch = u.match(/url\(([^)]+)\)/);
                                        if (urlMatch) {
                                            const url = urlMatch[1].replace(/[""']/g, '');
                                            fonts.push({
                                                url: url,
                                                family: rule.style.getPropertyValue('font-family').replace(/[""']/g, ''),
                                                format: url.match(/\.(woff2?|ttf|otf|eot)/i)?.[1] || 'unknown'
                                            });
                                        }
                                    });
                                }
                            }
                        }
                    } catch(e) {}
                }

                document.querySelectorAll('script[src]').forEach(s => {
                    scripts.push({
                        src: s.src,
                        async: s.async,
                        defer: s.defer,
                        type: s.type
                    });
                });

                document.querySelectorAll('link[rel=stylesheet]').forEach(l => {
                    stylesheets.push({
                        href: l.href,
                        media: l.media
                    });
                });

                const faviconEl = document.querySelector('link[rel*=icon]');
                const favicon = faviconEl ? faviconEl.href : '/favicon.ico';

                return JSON.stringify({
                    images,
                    svgs,
                    fonts,
                    scripts,
                    stylesheets,
                    favicon
                });
            }");

            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonSerializer.Deserialize<AssetData>(json);
                if (data != null)
                {
                    result.Images = data.images?
                        .Where(i => !string.IsNullOrEmpty(i.src))
                        .Select(i => new ImageAsset
                        {
                            Url = ResolveUrl(i.src!, baseUri),
                            Alt = i.alt ?? "",
                            Width = i.width,
                            Height = i.height,
                            IsLazyLoaded = i.loading == "lazy",
                            IsBackground = i.isBackground,
                            Format = DetectImageFormat(i.src!),
                            Srcset = i.srcset
                        })
                        .DistinctBy(i => i.Url)
                        .ToList() ?? new();

                    result.SvgIcons = data.svgs?
                        .Where(s => s.width > 0 && s.width < 200)
                        .Select(s => new SvgIcon
                        {
                            Content = s.content ?? "",
                            Width = (int)s.width,
                            Height = (int)s.height,
                            ViewBox = s.viewBox ?? "",
                            Id = s.id,
                            ClassName = s.className
                        })
                        .Take(100)
                        .ToList() ?? new();

                    result.FontFiles = data.fonts?
                        .Where(f => !string.IsNullOrEmpty(f.url))
                        .Select(f => new FontFile
                        {
                            Url = ResolveUrl(f.url!, baseUri),
                            Family = f.family ?? "",
                            Format = f.format ?? "unknown"
                        })
                        .DistinctBy(f => f.Url)
                        .ToList() ?? new();

                    result.Scripts = data.scripts?
                        .Where(s => !string.IsNullOrEmpty(s.src))
                        .Select(s => new ScriptInfo
                        {
                            Url = s.src!,
                            IsAsync = s.async,
                            IsDefer = s.defer,
                            Type = s.type ?? "text/javascript"
                        })
                        .ToList() ?? new();

                    result.Stylesheets = data.stylesheets?
                        .Where(s => !string.IsNullOrEmpty(s.href))
                        .Select(s => new StylesheetInfo
                        {
                            Url = s.href!,
                            Media = s.media ?? "all"
                        })
                        .ToList() ?? new();

                    result.Favicon = data.favicon ?? "";
                }
            }
        }
        catch
        {
        }

        return result;
    }

    private string ResolveUrl(string url, Uri baseUri)
    {
        if (string.IsNullOrEmpty(url)) return "";
        if (url.StartsWith("data:")) return url;
        if (url.StartsWith("//")) return $"https:{url}";
        if (url.StartsWith("/")) return new Uri(baseUri, url).ToString();
        if (!url.StartsWith("http")) return new Uri(baseUri, url).ToString();
        return url;
    }

    private string DetectImageFormat(string url)
    {
        var lower = url.ToLower();
        if (lower.Contains(".svg")) return "svg";
        if (lower.Contains(".webp")) return "webp";
        if (lower.Contains(".png")) return "png";
        if (lower.Contains(".jpg") || lower.Contains(".jpeg")) return "jpg";
        if (lower.Contains(".gif")) return "gif";
        if (lower.Contains(".avif")) return "avif";
        if (lower.StartsWith("data:image/")) return lower.Split('/')[1].Split(';')[0];
        return "unknown";
    }

    private class AssetData
    {
        public List<ImageData>? images { get; set; }
        public List<SvgData>? svgs { get; set; }
        public List<FontData>? fonts { get; set; }
        public List<ScriptData>? scripts { get; set; }
        public List<StylesheetData>? stylesheets { get; set; }
        public string? favicon { get; set; }
    }

    private class ImageData
    {
        public string? src { get; set; }
        public string? alt { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string? loading { get; set; }
        public string? srcset { get; set; }
        public bool isBackground { get; set; }
        public string? element { get; set; }
    }

    private class SvgData
    {
        public string? content { get; set; }
        public double width { get; set; }
        public double height { get; set; }
        public string? viewBox { get; set; }
        public string? id { get; set; }
        public string? className { get; set; }
    }

    private class FontData
    {
        public string? url { get; set; }
        public string? family { get; set; }
        public string? format { get; set; }
    }

    private class ScriptData
    {
        public string? src { get; set; }
        public bool async { get; set; }
        public bool defer { get; set; }
        public string? type { get; set; }
    }

    private class StylesheetData
    {
        public string? href { get; set; }
        public string? media { get; set; }
    }
}

public class ExtractedAssets
{
    public List<ImageAsset> Images { get; set; } = new();
    public List<SvgIcon> SvgIcons { get; set; } = new();
    public List<FontFile> FontFiles { get; set; } = new();
    public List<ScriptInfo> Scripts { get; set; } = new();
    public List<StylesheetInfo> Stylesheets { get; set; } = new();
    public string Favicon { get; set; } = "";
}

public class ImageAsset
{
    public string Url { get; set; } = "";
    public string Alt { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; } = "";
    public bool IsLazyLoaded { get; set; }
    public bool IsBackground { get; set; }
    public string? Srcset { get; set; }
}

public class SvgIcon
{
    public string Content { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string ViewBox { get; set; } = "";
    public string? Id { get; set; }
    public string? ClassName { get; set; }
}

public class FontFile
{
    public string Url { get; set; } = "";
    public string Family { get; set; } = "";
    public string Format { get; set; } = "";
}

public class ScriptInfo
{
    public string Url { get; set; } = "";
    public bool IsAsync { get; set; }
    public bool IsDefer { get; set; }
    public string Type { get; set; } = "";
}

public class StylesheetInfo
{
    public string Url { get; set; } = "";
    public string Media { get; set; } = "all";
}
