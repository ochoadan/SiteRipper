namespace SiteRipper.Structure;

using Microsoft.Playwright;
using System.Text.Json;

public class NavigationExtractor
{
    public async Task<SiteStructure> ExtractAsync(IPage page, string baseUrl)
    {
        var result = new SiteStructure();
        var baseUri = new Uri(baseUrl);

        try
        {
            var json = await page.EvaluateAsync<string>(@"
                (baseHost) => {
                    const links = [];
                    const seenUrls = new Set();

                    // Extract all links
                    document.querySelectorAll('a[href]').forEach(a => {
                        const href = a.href;
                        if (!href || seenUrls.has(href)) return;
                        seenUrls.add(href);

                        const rect = a.getBoundingClientRect();
                        const isVisible = rect.width > 0 && rect.height > 0;

                        let location = 'body';
                        const parent = a.closest('nav, header, footer, aside');
                        if (parent) {
                            location = parent.tagName.toLowerCase();
                        }

                        links.push({
                            href: href,
                            text: a.textContent?.trim().substring(0, 100) || '',
                            isInternal: href.includes(baseHost) || href.startsWith('/'),
                            isAnchor: href.includes('#'),
                            location: location,
                            isVisible: isVisible,
                            y: rect.y
                        });
                    });

                    // Main navigation
                    const mainNav = [];
                    const navEl = document.querySelector('nav, header nav, [role=navigation]');
                    if (navEl) {
                        navEl.querySelectorAll('a[href]').forEach(a => {
                            mainNav.push({
                                href: a.href,
                                text: a.textContent?.trim() || '',
                                hasSubmenu: a.closest('li')?.querySelector('ul, [class*=dropdown], [class*=submenu]') !== null
                            });
                        });
                    }

                    // Footer navigation
                    const footerNav = [];
                    const footerEl = document.querySelector('footer');
                    if (footerEl) {
                        footerEl.querySelectorAll('a[href]').forEach(a => {
                            footerNav.push({
                                href: a.href,
                                text: a.textContent?.trim() || ''
                            });
                        });
                    }

                    // Breadcrumbs
                    let breadcrumbs = [];
                    const bcEl = document.querySelector('[class*=breadcrumb], [aria-label*=breadcrumb], nav[aria-label=Breadcrumb]');
                    if (bcEl) {
                        bcEl.querySelectorAll('a, span').forEach(item => {
                            const text = item.textContent?.trim();
                            if (text) {
                                breadcrumbs.push({
                                    text: text,
                                    href: item.href || null
                                });
                            }
                        });
                    }

                    // Page sections
                    const sections = [];
                    document.querySelectorAll('section, [class*=section], main > div').forEach(s => {
                        const rect = s.getBoundingClientRect();
                        if (rect.height > 100) {
                            const heading = s.querySelector('h1, h2, h3');
                            sections.push({
                                id: s.id || null,
                                className: s.className || null,
                                heading: heading?.textContent?.trim().substring(0, 100) || null,
                                y: rect.y,
                                height: rect.height
                            });
                        }
                    });

                    return JSON.stringify({
                        links,
                        mainNav,
                        footerNav,
                        breadcrumbs,
                        sections
                    });
                }
            ", baseUri.Host);

            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonSerializer.Deserialize<NavData>(json);
                if (data != null)
                {
                    // Process all links
                    var allLinks = data.links ?? new();

                    result.InternalLinks = allLinks
                        .Where(l => l.isInternal && !l.isAnchor)
                        .Select(l => new LinkInfo
                        {
                            Url = l.href ?? "",
                            Text = l.text ?? "",
                            Location = l.location ?? "body"
                        })
                        .DistinctBy(l => l.Url)
                        .ToList();

                    result.ExternalLinks = allLinks
                        .Where(l => !l.isInternal && !l.isAnchor)
                        .Select(l => new LinkInfo
                        {
                            Url = l.href ?? "",
                            Text = l.text ?? "",
                            Location = l.location ?? "body"
                        })
                        .DistinctBy(l => l.Url)
                        .ToList();

                    result.AnchorLinks = allLinks
                        .Where(l => l.isAnchor)
                        .Select(l => new LinkInfo
                        {
                            Url = l.href ?? "",
                            Text = l.text ?? "",
                            Location = l.location ?? "body"
                        })
                        .DistinctBy(l => l.Url)
                        .ToList();

                    // Main navigation
                    result.MainNavigation = new NavigationMenu
                    {
                        Items = (data.mainNav ?? new())
                            .Select(n => new NavItem
                            {
                                Url = n.href ?? "",
                                Text = n.text ?? "",
                                HasSubmenu = n.hasSubmenu
                            })
                            .Where(n => !string.IsNullOrEmpty(n.Text))
                            .ToList()
                    };

                    // Footer navigation
                    result.FooterNavigation = new NavigationMenu
                    {
                        Items = (data.footerNav ?? new())
                            .Select(n => new NavItem
                            {
                                Url = n.href ?? "",
                                Text = n.text ?? ""
                            })
                            .Where(n => !string.IsNullOrEmpty(n.Text))
                            .ToList()
                    };

                    // Breadcrumbs
                    result.Breadcrumbs = (data.breadcrumbs ?? new())
                        .Select(b => new BreadcrumbItem
                        {
                            Text = b.text ?? "",
                            Url = b.href
                        })
                        .Where(b => !string.IsNullOrEmpty(b.Text))
                        .ToList();

                    // Page sections
                    result.PageSections = (data.sections ?? new())
                        .OrderBy(s => s.y)
                        .Select(s => new PageSection
                        {
                            Id = s.id,
                            ClassName = s.className,
                            Heading = s.heading,
                            YPosition = (int)s.y,
                            Height = (int)s.height
                        })
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

    private class NavData
    {
        public List<LinkData>? links { get; set; }
        public List<NavItemData>? mainNav { get; set; }
        public List<NavItemData>? footerNav { get; set; }
        public List<BreadcrumbData>? breadcrumbs { get; set; }
        public List<SectionData>? sections { get; set; }
    }

    private class LinkData
    {
        public string? href { get; set; }
        public string? text { get; set; }
        public bool isInternal { get; set; }
        public bool isAnchor { get; set; }
        public string? location { get; set; }
        public bool isVisible { get; set; }
        public double y { get; set; }
    }

    private class NavItemData
    {
        public string? href { get; set; }
        public string? text { get; set; }
        public bool hasSubmenu { get; set; }
    }

    private class BreadcrumbData
    {
        public string? text { get; set; }
        public string? href { get; set; }
    }

    private class SectionData
    {
        public string? id { get; set; }
        public string? className { get; set; }
        public string? heading { get; set; }
        public double y { get; set; }
        public double height { get; set; }
    }
}

public class SiteStructure
{
    public NavigationMenu MainNavigation { get; set; } = new();
    public NavigationMenu FooterNavigation { get; set; } = new();
    public List<LinkInfo> InternalLinks { get; set; } = new();
    public List<LinkInfo> ExternalLinks { get; set; } = new();
    public List<LinkInfo> AnchorLinks { get; set; } = new();
    public List<BreadcrumbItem> Breadcrumbs { get; set; } = new();
    public List<PageSection> PageSections { get; set; } = new();
}

public class NavigationMenu
{
    public List<NavItem> Items { get; set; } = new();
}

public class NavItem
{
    public string Url { get; set; } = "";
    public string Text { get; set; } = "";
    public bool HasSubmenu { get; set; }
    public List<NavItem> Children { get; set; } = new();
}

public class LinkInfo
{
    public string Url { get; set; } = "";
    public string Text { get; set; } = "";
    public string Location { get; set; } = "";
}

public class BreadcrumbItem
{
    public string Text { get; set; } = "";
    public string? Url { get; set; }
}

public class PageSection
{
    public string? Id { get; set; }
    public string? ClassName { get; set; }
    public string? Heading { get; set; }
    public int YPosition { get; set; }
    public int Height { get; set; }
    public List<string> ComponentIds { get; set; } = new();  // Components within this section
}
