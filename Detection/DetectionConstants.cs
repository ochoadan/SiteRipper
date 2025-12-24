namespace SiteRipper.Detection;

/// <summary>
/// Shared constants for component detection
/// </summary>
public static class DetectionConstants
{
    /// <summary>
    /// HTML tags that are not visual elements
    /// </summary>
    public static readonly HashSet<string> IgnoredTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "noscript", "meta", "link", "head",
        "html", "br", "hr", "wbr", "template"
    };

    /// <summary>
    /// Check if a tag is a visual element
    /// </summary>
    public static bool IsVisualTag(string tagName)
    {
        return !IgnoredTags.Contains(tagName.ToLowerInvariant());
    }
}
