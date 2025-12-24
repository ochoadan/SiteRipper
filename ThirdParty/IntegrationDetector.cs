namespace SiteRipper.ThirdParty;

using Microsoft.Playwright;
using System.Text.Json;
using System.Text.RegularExpressions;

public class IntegrationDetector
{
    public async Task<ThirdPartyIntegrations> DetectAsync(IPage page)
    {
        var result = new ThirdPartyIntegrations();

        try
        {
            var json = await page.EvaluateAsync<string>(@"
                () => {
                    const scripts = [];
                    const iframes = [];
                    const globalVars = [];

                    // Collect all external scripts
                    document.querySelectorAll('script[src]').forEach(s => {
                        scripts.push(s.src);
                    });

                    // Collect inline scripts content hints
                    document.querySelectorAll('script:not([src])').forEach(s => {
                        const content = s.textContent || '';
                        // Look for common patterns
                        if (content.includes('gtag') || content.includes('ga(')) globalVars.push('google-analytics');
                        if (content.includes('fbq')) globalVars.push('facebook-pixel');
                        if (content.includes('mixpanel')) globalVars.push('mixpanel');
                        if (content.includes('segment')) globalVars.push('segment');
                        if (content.includes('hotjar')) globalVars.push('hotjar');
                        if (content.includes('clarity')) globalVars.push('microsoft-clarity');
                        if (content.includes('intercom')) globalVars.push('intercom');
                        if (content.includes('drift')) globalVars.push('drift');
                        if (content.includes('crisp')) globalVars.push('crisp');
                        if (content.includes('hubspot')) globalVars.push('hubspot');
                        if (content.includes('stripe')) globalVars.push('stripe');
                        if (content.includes('typeform')) globalVars.push('typeform');
                    });

                    // Check global objects
                    if (window.gtag) globalVars.push('gtag');
                    if (window.ga) globalVars.push('google-analytics');
                    if (window.dataLayer) globalVars.push('google-tag-manager');
                    if (window.fbq) globalVars.push('facebook-pixel');
                    if (window.Intercom) globalVars.push('intercom');
                    if (window.$crisp) globalVars.push('crisp');
                    if (window.Drift) globalVars.push('drift');
                    if (window.HubSpotConversations) globalVars.push('hubspot-chat');

                    // Collect iframes
                    document.querySelectorAll('iframe').forEach(f => {
                        if (f.src) iframes.push(f.src);
                    });

                    return JSON.stringify({ scripts, iframes, globalVars: [...new Set(globalVars)] });
                }
            ");

            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonSerializer.Deserialize<RawData>(json);
                if (data != null)
                {
                    var scripts = data.scripts ?? new();
                    var iframes = data.iframes ?? new();
                    var globals = data.globalVars ?? new();

                    // Analytics detection
                    result.Analytics = DetectAnalytics(scripts, globals);

                    // Chat widgets
                    result.ChatWidget = DetectChatWidget(scripts, globals, iframes);

                    // Marketing/CRM
                    result.Marketing = DetectMarketing(scripts, globals);

                    // Payment
                    result.Payment = DetectPayment(scripts, globals);

                    // CDN detection
                    result.Cdn = DetectCdn(scripts);

                    // Social embeds
                    result.SocialEmbeds = DetectSocialEmbeds(scripts, iframes);

                    // All external services
                    result.AllServices = BuildServiceList(scripts, iframes, globals);
                }
            }
        }
        catch
        {
            // Silently fail
        }

        return result;
    }

    private List<AnalyticsService> DetectAnalytics(List<string> scripts, List<string> globals)
    {
        var analytics = new List<AnalyticsService>();

        if (scripts.Any(s => s.Contains("googletagmanager")) || globals.Contains("google-tag-manager"))
            analytics.Add(new() { Name = "Google Tag Manager", Type = "tag-manager" });

        if (scripts.Any(s => s.Contains("google-analytics") || s.Contains("analytics.js")) || globals.Contains("google-analytics"))
            analytics.Add(new() { Name = "Google Analytics", Type = "analytics" });

        if (scripts.Any(s => s.Contains("gtag")) || globals.Contains("gtag"))
            analytics.Add(new() { Name = "Google Analytics 4", Type = "analytics" });

        if (scripts.Any(s => s.Contains("mixpanel")) || globals.Contains("mixpanel"))
            analytics.Add(new() { Name = "Mixpanel", Type = "analytics" });

        if (scripts.Any(s => s.Contains("segment")) || globals.Contains("segment"))
            analytics.Add(new() { Name = "Segment", Type = "cdp" });

        if (scripts.Any(s => s.Contains("amplitude")) || globals.Contains("amplitude"))
            analytics.Add(new() { Name = "Amplitude", Type = "analytics" });

        if (scripts.Any(s => s.Contains("hotjar")) || globals.Contains("hotjar"))
            analytics.Add(new() { Name = "Hotjar", Type = "heatmap" });

        if (scripts.Any(s => s.Contains("clarity")) || globals.Contains("microsoft-clarity"))
            analytics.Add(new() { Name = "Microsoft Clarity", Type = "heatmap" });

        if (scripts.Any(s => s.Contains("fullstory")))
            analytics.Add(new() { Name = "FullStory", Type = "session-replay" });

        if (scripts.Any(s => s.Contains("logrocket")))
            analytics.Add(new() { Name = "LogRocket", Type = "session-replay" });

        return analytics;
    }

    private ChatWidgetInfo? DetectChatWidget(List<string> scripts, List<string> globals, List<string> iframes)
    {
        if (scripts.Any(s => s.Contains("intercom")) || globals.Contains("intercom"))
            return new() { Name = "Intercom", Type = "chat" };

        if (scripts.Any(s => s.Contains("drift")) || globals.Contains("drift"))
            return new() { Name = "Drift", Type = "chat" };

        if (scripts.Any(s => s.Contains("crisp")) || globals.Contains("crisp"))
            return new() { Name = "Crisp", Type = "chat" };

        if (scripts.Any(s => s.Contains("zendesk")))
            return new() { Name = "Zendesk", Type = "chat" };

        if (scripts.Any(s => s.Contains("tawk.to")))
            return new() { Name = "Tawk.to", Type = "chat" };

        if (scripts.Any(s => s.Contains("hubspot")) || globals.Contains("hubspot-chat"))
            return new() { Name = "HubSpot Chat", Type = "chat" };

        if (scripts.Any(s => s.Contains("freshdesk") || s.Contains("freshchat")))
            return new() { Name = "Freshchat", Type = "chat" };

        return null;
    }

    private List<MarketingService> DetectMarketing(List<string> scripts, List<string> globals)
    {
        var marketing = new List<MarketingService>();

        if (scripts.Any(s => s.Contains("facebook") || s.Contains("fbevents")) || globals.Contains("facebook-pixel"))
            marketing.Add(new() { Name = "Facebook Pixel", Type = "advertising" });

        if (scripts.Any(s => s.Contains("linkedin")))
            marketing.Add(new() { Name = "LinkedIn Insight", Type = "advertising" });

        if (scripts.Any(s => s.Contains("twitter") || s.Contains("twq")))
            marketing.Add(new() { Name = "Twitter Pixel", Type = "advertising" });

        if (scripts.Any(s => s.Contains("hubspot")) || globals.Contains("hubspot"))
            marketing.Add(new() { Name = "HubSpot", Type = "crm" });

        if (scripts.Any(s => s.Contains("marketo")))
            marketing.Add(new() { Name = "Marketo", Type = "marketing-automation" });

        if (scripts.Any(s => s.Contains("mailchimp")))
            marketing.Add(new() { Name = "Mailchimp", Type = "email" });

        if (scripts.Any(s => s.Contains("klaviyo")))
            marketing.Add(new() { Name = "Klaviyo", Type = "email" });

        return marketing;
    }

    private List<PaymentService> DetectPayment(List<string> scripts, List<string> globals)
    {
        var payment = new List<PaymentService>();

        if (scripts.Any(s => s.Contains("stripe")) || globals.Contains("stripe"))
            payment.Add(new() { Name = "Stripe", Type = "payment" });

        if (scripts.Any(s => s.Contains("paypal")))
            payment.Add(new() { Name = "PayPal", Type = "payment" });

        if (scripts.Any(s => s.Contains("braintree")))
            payment.Add(new() { Name = "Braintree", Type = "payment" });

        if (scripts.Any(s => s.Contains("square")))
            payment.Add(new() { Name = "Square", Type = "payment" });

        return payment;
    }

    private CdnInfo? DetectCdn(List<string> scripts)
    {
        if (scripts.Any(s => s.Contains("cloudflare")))
            return new() { Name = "Cloudflare", Features = new() { "cdn", "ddos-protection" } };

        if (scripts.Any(s => s.Contains("fastly")))
            return new() { Name = "Fastly", Features = new() { "cdn" } };

        if (scripts.Any(s => s.Contains("akamai")))
            return new() { Name = "Akamai", Features = new() { "cdn" } };

        if (scripts.Any(s => s.Contains("cloudfront")))
            return new() { Name = "AWS CloudFront", Features = new() { "cdn" } };

        return null;
    }

    private List<SocialEmbed> DetectSocialEmbeds(List<string> scripts, List<string> iframes)
    {
        var embeds = new List<SocialEmbed>();

        if (iframes.Any(f => f.Contains("youtube")))
            embeds.Add(new() { Platform = "YouTube", Type = "video" });

        if (iframes.Any(f => f.Contains("vimeo")))
            embeds.Add(new() { Platform = "Vimeo", Type = "video" });

        if (iframes.Any(f => f.Contains("twitter") || f.Contains("x.com")))
            embeds.Add(new() { Platform = "Twitter/X", Type = "social" });

        if (iframes.Any(f => f.Contains("facebook")))
            embeds.Add(new() { Platform = "Facebook", Type = "social" });

        if (iframes.Any(f => f.Contains("instagram")))
            embeds.Add(new() { Platform = "Instagram", Type = "social" });

        if (iframes.Any(f => f.Contains("spotify")))
            embeds.Add(new() { Platform = "Spotify", Type = "audio" });

        if (scripts.Any(s => s.Contains("typeform")) || iframes.Any(f => f.Contains("typeform")))
            embeds.Add(new() { Platform = "Typeform", Type = "form" });

        return embeds;
    }

    private List<ExternalService> BuildServiceList(List<string> scripts, List<string> iframes, List<string> globals)
    {
        var services = new List<ExternalService>();

        foreach (var script in scripts.Distinct())
        {
            var domain = ExtractDomain(script);
            if (!string.IsNullOrEmpty(domain))
            {
                services.Add(new() { Url = script, Domain = domain, Type = "script" });
            }
        }

        foreach (var iframe in iframes.Distinct())
        {
            var domain = ExtractDomain(iframe);
            if (!string.IsNullOrEmpty(domain))
            {
                services.Add(new() { Url = iframe, Domain = domain, Type = "iframe" });
            }
        }

        return services.DistinctBy(s => s.Domain).ToList();
    }

    private string ExtractDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return "";
        }
    }

    private class RawData
    {
        public List<string>? scripts { get; set; }
        public List<string>? iframes { get; set; }
        public List<string>? globalVars { get; set; }
    }
}

public class ThirdPartyIntegrations
{
    public List<AnalyticsService> Analytics { get; set; } = new();
    public ChatWidgetInfo? ChatWidget { get; set; }
    public List<MarketingService> Marketing { get; set; } = new();
    public List<PaymentService> Payment { get; set; } = new();
    public CdnInfo? Cdn { get; set; }
    public List<SocialEmbed> SocialEmbeds { get; set; } = new();
    public List<ExternalService> AllServices { get; set; } = new();
}

public class AnalyticsService
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}

public class ChatWidgetInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}

public class MarketingService
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}

public class PaymentService
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}

public class CdnInfo
{
    public string Name { get; set; } = "";
    public List<string> Features { get; set; } = new();
}

public class SocialEmbed
{
    public string Platform { get; set; } = "";
    public string Type { get; set; } = "";
}

public class ExternalService
{
    public string Url { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Type { get; set; } = "";
}
