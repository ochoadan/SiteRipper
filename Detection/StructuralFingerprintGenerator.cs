namespace SiteRipper.Detection;

using AngleSharp.Dom;

public class StructuralFingerprintGenerator
{
    private const int MaxTagSignatureDepth = 4;
    private const int MaxChildrenPerLevel = 10;

    private static readonly HashSet<string> IgnoredTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "noscript", "meta", "link", "br", "hr", "wbr"
    };

    private static readonly HashSet<string> HeadingTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "h1", "h2", "h3", "h4", "h5", "h6"
    };

    private static readonly HashSet<string> MediaTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "img", "video", "audio", "svg", "canvas", "picture", "iframe"
    };

    private static readonly HashSet<string> InteractiveTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "button", "a", "input", "select", "textarea"
    };

    public StructuralFingerprint Generate(IElement element)
    {
        var fingerprint = new StructuralFingerprint
        {
            TagSignature = BuildTagSignature(element, 0),
            ChildCount = CountRelevantChildren(element),
            Depth = CalculateDepth(element),
            TextNodeCount = CountTextNodes(element),
            InteractiveCount = CountInteractive(element),
            MediaCount = CountMedia(element),
            HasHeading = HasDescendant(element, HeadingTags),
            HasImage = element.QuerySelector("img,svg,picture") != null,
            HasLink = element.QuerySelector("a[href]") != null,
            HasButton = element.QuerySelector("button,[role='button']") != null,
            HasForm = element.QuerySelector("form,input,textarea,select") != null,
            HasList = element.QuerySelector("ul,ol,dl") != null,
            HasTable = element.QuerySelector("table") != null
        };

        fingerprint.ComputeHash();
        return fingerprint;
    }

    private string BuildTagSignature(IElement element, int depth)
    {
        if (depth >= MaxTagSignatureDepth) return "...";

        var tag = element.TagName.ToLower();
        var children = element.Children
            .Where(c => IsStructurallyRelevant(c))
            .Take(MaxChildrenPerLevel)
            .ToList();

        if (children.Count == 0) return tag;

        var childSignatures = children
            .Select(c => BuildTagSignature(c, depth + 1))
            .ToList();

        // Group consecutive same-tag children
        var grouped = GroupConsecutive(childSignatures);

        return $"{tag}[{string.Join(",", grouped)}]";
    }

    private List<string> GroupConsecutive(List<string> signatures)
    {
        if (signatures.Count == 0) return signatures;

        var result = new List<string>();
        var current = signatures[0];
        var count = 1;

        for (int i = 1; i < signatures.Count; i++)
        {
            if (signatures[i] == current)
            {
                count++;
            }
            else
            {
                result.Add(count > 1 ? $"{current}*{count}" : current);
                current = signatures[i];
                count = 1;
            }
        }

        result.Add(count > 1 ? $"{current}*{count}" : current);
        return result;
    }

    private bool IsStructurallyRelevant(IElement element)
    {
        return !IgnoredTags.Contains(element.TagName);
    }

    private int CountRelevantChildren(IElement element)
    {
        return element.Children.Count(c => IsStructurallyRelevant(c));
    }

    private int CalculateDepth(IElement element)
    {
        int depth = 0;
        var current = element.ParentElement;
        while (current != null)
        {
            depth++;
            current = current.ParentElement;
        }
        return depth;
    }

    private int CountTextNodes(IElement element)
    {
        return element.ChildNodes
            .Count(n => n.NodeType == NodeType.Text &&
                       !string.IsNullOrWhiteSpace(n.TextContent));
    }

    private int CountInteractive(IElement element)
    {
        int count = 0;
        foreach (var child in element.QuerySelectorAll("*"))
        {
            if (InteractiveTags.Contains(child.TagName) ||
                child.HasAttribute("onclick") ||
                child.GetAttribute("role") == "button")
            {
                count++;
            }
        }
        return count;
    }

    private int CountMedia(IElement element)
    {
        return element.QuerySelectorAll(string.Join(",", MediaTags)).Length;
    }

    private bool HasDescendant(IElement element, HashSet<string> tags)
    {
        return element.QuerySelectorAll(string.Join(",", tags)).Length > 0;
    }
}
