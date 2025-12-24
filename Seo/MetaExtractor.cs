namespace SiteRipper.Seo;

using Microsoft.Playwright;
using System.Text.Json;

public class MetaExtractor
{
    public async Task<SeoData> ExtractAsync(IPage page)
    {
        var result = new SeoData();

        try
        {
            var json = await page.EvaluateAsync<string>(@"
                () => {
                    const getMeta = (name) => {
                        const el = document.querySelector(`meta[name='${name}'], meta[property='${name}']`);
                        return el?.content || null;
                    };

                    // Basic meta
                    const title = document.title;
                    const description = getMeta('description');
                    const canonical = document.querySelector('link[rel=canonical]')?.href;
                    const robots = getMeta('robots');

                    // Open Graph
                    const og = {
                        title: getMeta('og:title'),
                        description: getMeta('og:description'),
                        image: getMeta('og:image'),
                        url: getMeta('og:url'),
                        type: getMeta('og:type'),
                        siteName: getMeta('og:site_name'),
                        locale: getMeta('og:locale')
                    };

                    // Twitter Card
                    const twitter = {
                        card: getMeta('twitter:card'),
                        title: getMeta('twitter:title'),
                        description: getMeta('twitter:description'),
                        image: getMeta('twitter:image'),
                        site: getMeta('twitter:site'),
                        creator: getMeta('twitter:creator')
                    };

                    // Favicon
                    const favicon = document.querySelector('link[rel*=icon]')?.href || '/favicon.ico';

                    // Alternate links (hreflang)
                    const alternates = [];
                    document.querySelectorAll('link[rel=alternate][hreflang]').forEach(l => {
                        alternates.push({
                            hreflang: l.hreflang,
                            href: l.href
                        });
                    });

                    // JSON-LD structured data
                    const jsonLd = [];
                    document.querySelectorAll('script[type=""application/ld+json""]').forEach(s => {
                        try {
                            const data = JSON.parse(s.textContent);
                            jsonLd.push(data);
                        } catch(e) {}
                    });

                    // Other meta tags
                    const otherMeta = {};
                    document.querySelectorAll('meta[name], meta[property]').forEach(m => {
                        const name = m.name || m.getAttribute('property');
                        if (name && !name.startsWith('og:') && !name.startsWith('twitter:')) {
                            otherMeta[name] = m.content;
                        }
                    });

                    // Charset and viewport
                    const charset = document.characterSet;
                    const viewport = getMeta('viewport');

                    // Preconnects and prefetches
                    const preconnects = [];
                    document.querySelectorAll('link[rel=preconnect], link[rel=dns-prefetch]').forEach(l => {
                        preconnects.push({
                            rel: l.rel,
                            href: l.href
                        });
                    });

                    return JSON.stringify({
                        title,
                        description,
                        canonical,
                        robots,
                        openGraph: og,
                        twitter,
                        favicon,
                        alternates,
                        jsonLd,
                        otherMeta,
                        charset,
                        viewport,
                        preconnects
                    });
                }
            ");

            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonSerializer.Deserialize<SeoRawData>(json);
                if (data != null)
                {
                    result.Title = data.title ?? "";
                    result.Description = data.description ?? "";
                    result.CanonicalUrl = data.canonical ?? "";
                    result.Robots = data.robots ?? "";
                    result.Favicon = data.favicon ?? "";
                    result.Charset = data.charset ?? "UTF-8";
                    result.Viewport = data.viewport ?? "";

                    if (data.openGraph != null)
                    {
                        result.OpenGraph = new OpenGraphData
                        {
                            Title = data.openGraph.title ?? "",
                            Description = data.openGraph.description ?? "",
                            Image = data.openGraph.image ?? "",
                            Url = data.openGraph.url ?? "",
                            Type = data.openGraph.type ?? "",
                            SiteName = data.openGraph.siteName ?? "",
                            Locale = data.openGraph.locale ?? ""
                        };
                    }

                    if (data.twitter != null)
                    {
                        result.TwitterCard = new TwitterCardData
                        {
                            Card = data.twitter.card ?? "",
                            Title = data.twitter.title ?? "",
                            Description = data.twitter.description ?? "",
                            Image = data.twitter.image ?? "",
                            Site = data.twitter.site ?? "",
                            Creator = data.twitter.creator ?? ""
                        };
                    }

                    result.AlternateLinks = data.alternates?
                        .Select(a => new AlternateLink
                        {
                            Hreflang = a.hreflang ?? "",
                            Href = a.href ?? ""
                        })
                        .ToList() ?? new();

                    result.StructuredData = data.jsonLd ?? new();

                    result.Preconnects = data.preconnects?
                        .Select(p => new PreconnectLink
                        {
                            Rel = p.rel ?? "",
                            Href = p.href ?? ""
                        })
                        .ToList() ?? new();

                    result.OtherMeta = data.otherMeta ?? new();
                }
            }
        }
        catch
        {
            // Silently fail
        }

        return result;
    }

    private class SeoRawData
    {
        public string? title { get; set; }
        public string? description { get; set; }
        public string? canonical { get; set; }
        public string? robots { get; set; }
        public string? favicon { get; set; }
        public string? charset { get; set; }
        public string? viewport { get; set; }
        public OgRawData? openGraph { get; set; }
        public TwitterRawData? twitter { get; set; }
        public List<AlternateRaw>? alternates { get; set; }
        public List<object>? jsonLd { get; set; }
        public Dictionary<string, string>? otherMeta { get; set; }
        public List<PreconnectRaw>? preconnects { get; set; }
    }

    private class OgRawData
    {
        public string? title { get; set; }
        public string? description { get; set; }
        public string? image { get; set; }
        public string? url { get; set; }
        public string? type { get; set; }
        public string? siteName { get; set; }
        public string? locale { get; set; }
    }

    private class TwitterRawData
    {
        public string? card { get; set; }
        public string? title { get; set; }
        public string? description { get; set; }
        public string? image { get; set; }
        public string? site { get; set; }
        public string? creator { get; set; }
    }

    private class AlternateRaw
    {
        public string? hreflang { get; set; }
        public string? href { get; set; }
    }

    private class PreconnectRaw
    {
        public string? rel { get; set; }
        public string? href { get; set; }
    }
}

public class SeoData
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string CanonicalUrl { get; set; } = "";
    public string Robots { get; set; } = "";
    public string Favicon { get; set; } = "";
    public string Charset { get; set; } = "";
    public string Viewport { get; set; } = "";
    public OpenGraphData OpenGraph { get; set; } = new();
    public TwitterCardData TwitterCard { get; set; } = new();
    public List<AlternateLink> AlternateLinks { get; set; } = new();
    public List<object> StructuredData { get; set; } = new();
    public List<PreconnectLink> Preconnects { get; set; } = new();
    public Dictionary<string, string> OtherMeta { get; set; } = new();
}

public class OpenGraphData
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Image { get; set; } = "";
    public string Url { get; set; } = "";
    public string Type { get; set; } = "";
    public string SiteName { get; set; } = "";
    public string Locale { get; set; } = "";
}

public class TwitterCardData
{
    public string Card { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Image { get; set; } = "";
    public string Site { get; set; } = "";
    public string Creator { get; set; } = "";
}

public class AlternateLink
{
    public string Hreflang { get; set; } = "";
    public string Href { get; set; } = "";
}

public class PreconnectLink
{
    public string Rel { get; set; } = "";
    public string Href { get; set; } = "";
}
