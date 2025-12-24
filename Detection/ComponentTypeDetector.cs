namespace SiteRipper.Detection;

using SiteRipper.Models;
using AngleSharp.Dom;

public class ComponentTypeDetector
{
    private readonly StructuralFingerprintGenerator _fingerprintGenerator = new();
    private readonly double _confidenceThreshold;

    public ComponentTypeDetector(double confidenceThreshold = 0.65)
    {
        _confidenceThreshold = confidenceThreshold;
    }

    public List<DetectedComponent> DetectComponents(
        IElement root,
        Dictionary<int, Dictionary<string, string>> styles,
        Dictionary<int, BoundingBox> boxes)
    {
        var detected = new List<DetectedComponent>();
        var allElements = root.QuerySelectorAll("*").ToList();

        for (int i = 0; i < allElements.Count; i++)
        {
            var element = allElements[i];

            // Skip non-visual elements
            if (!IsVisualElement(element)) continue;

            // Get visual properties
            if (!boxes.TryGetValue(i, out var box)) continue;

            // Skip tiny elements (not meaningful components)
            if (box.Width < 20 || box.Height < 20) continue;

            // Skip full-viewport elements (body, main containers)
            if (box.Width > 1900 && box.Height > 1000) continue;

            var visual = new VisualProperties
            {
                BoundingBox = box,
                Styles = styles.GetValueOrDefault(i) ?? new()
            };

            // Generate fingerprint
            var fingerprint = _fingerprintGenerator.Generate(element);

            // Skip single-character decorative elements (animated letters, icons, etc)
            var directText = GetDirectText(element);
            if (directText.Length == 1 && fingerprint.ChildCount == 0)
                continue;

            // Find best matching component type
            var bestMatch = FindBestMatch(fingerprint, visual);

            if (bestMatch.Confidence >= _confidenceThreshold)
            {
                // Capture outer HTML (truncate to 1000 chars for AI replication)
                var outerHtml = element.OuterHtml;
                if (outerHtml.Length > 1000)
                    outerHtml = outerHtml[..1000] + "...";

                detected.Add(new DetectedComponent
                {
                    Type = bestMatch.Type,
                    Confidence = bestMatch.Confidence,
                    Element = element,
                    DomIndex = i,
                    Selector = GetSelector(element),
                    ElementId = element.Id,
                    Classes = element.ClassList.ToList(),
                    Text = directText,  // Reuse already-computed text
                    OuterHtml = outerHtml,  // For AI replication
                    Fingerprint = fingerprint,
                    VisualProperties = visual
                });
            }
        }

        // Remove duplicates (parent containing same type as child)
        detected = RemoveNestedDuplicates(detected, allElements);

        return detected;
    }

    private (string Type, double Confidence) FindBestMatch(
        StructuralFingerprint fingerprint,
        VisualProperties visual)
    {
        string bestType = "unknown";
        double bestScore = 0;

        foreach (var rule in ComponentTypeRules.Rules)
        {
            var score = rule.Calculate(fingerprint, visual);
            if (score > bestScore)
            {
                bestScore = score;
                bestType = rule.ComponentType;
            }
        }

        return (bestType, bestScore);
    }

    private List<DetectedComponent> RemoveNestedDuplicates(
        List<DetectedComponent> components,
        List<IElement> allElements)
    {
        var toRemove = new HashSet<string>();

        foreach (var component in components)
        {
            foreach (var other in components)
            {
                if (component.Id == other.Id) continue;

                // Check if one contains the other (regardless of type)
                if (component.Element!.Contains(other.Element!))
                {
                    // Parent contains child
                    // If same type: always keep child (more specific)
                    // If different type: keep child if same or higher confidence
                    if (component.Type == other.Type || other.Confidence >= component.Confidence)
                    {
                        toRemove.Add(component.Id);
                    }
                }
            }
        }

        // Also remove components with very similar bounding boxes (likely same visual element)
        var sorted = components.OrderByDescending(c => c.Confidence).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            if (toRemove.Contains(sorted[i].Id)) continue;

            for (int j = i + 1; j < sorted.Count; j++)
            {
                if (toRemove.Contains(sorted[j].Id)) continue;

                var boxA = sorted[i].VisualProperties.BoundingBox;
                var boxB = sorted[j].VisualProperties.BoundingBox;

                // If boxes overlap significantly (>80%), remove the lower confidence one
                if (BoxesOverlapSignificantly(boxA, boxB))
                {
                    toRemove.Add(sorted[j].Id);
                }
            }
        }

        return components.Where(c => !toRemove.Contains(c.Id)).ToList();
    }

    private bool BoxesOverlapSignificantly(BoundingBox a, BoundingBox b)
    {
        // Calculate intersection
        var x1 = Math.Max(a.X, b.X);
        var y1 = Math.Max(a.Y, b.Y);
        var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

        if (x2 <= x1 || y2 <= y1) return false;

        var intersectionArea = (x2 - x1) * (y2 - y1);
        var smallerArea = Math.Min(a.Width * a.Height, b.Width * b.Height);

        return intersectionArea / smallerArea > 0.8;
    }

    private bool IsVisualElement(IElement element)
    {
        return DetectionConstants.IsVisualTag(element.TagName);
    }

    private string GetSelector(IElement element)
    {
        if (!string.IsNullOrEmpty(element.Id))
            return $"#{element.Id}";

        var classes = element.ClassList
            .Where(c => !c.Contains(":"))
            .Take(2)
            .ToList();

        if (classes.Count > 0)
            return $"{element.TagName.ToLower()}.{string.Join(".", classes)}";

        return element.TagName.ToLower();
    }

    private string GetDirectText(IElement element)
    {
        var text = string.Join(" ", element.ChildNodes
            .Where(n => n.NodeType == NodeType.Text)
            .Select(n => n.TextContent.Trim())
            .Where(t => !string.IsNullOrEmpty(t)));

        return text.Length > 100 ? text[..100] + "..." : text;
    }
}
