namespace SiteRipper.Hierarchy;

using System.Text.Json.Serialization;
using SiteRipper.Models;

public class ComponentHierarchyNode
{
    public string Id { get; set; } = "";
    public string ComponentType { get; set; } = "";
    public string Selector { get; set; } = "";
    public double Confidence { get; set; }

    [JsonIgnore]
    public ComponentHierarchyNode? Parent { get; set; }

    public List<ComponentHierarchyNode> Children { get; set; } = new();

    // Metrics
    public int Depth { get; set; }
    public int SubtreeSize { get; set; }

    // Composition info
    public bool IsAtomic { get; set; }          // No component children
    public bool IsCompound { get; set; }        // Has component children
    public CompositionType CompositionType { get; set; }
    public List<string> ChildTypes { get; set; } = new();  // Types of child components

    // Reference to detected component (not serialized)
    [JsonIgnore]
    public DetectedComponent? Component { get; set; }

    public void CalculateMetrics()
    {
        SubtreeSize = 1 + Children.Sum(c =>
        {
            c.CalculateMetrics();
            return c.SubtreeSize;
        });

        IsAtomic = !Children.Any(c => c.Component != null);
        IsCompound = Children.Any(c => c.Component != null);

        ChildTypes = Children
            .Where(c => c.Component != null)
            .Select(c => c.ComponentType)
            .Distinct()
            .ToList();

        CompositionType = DetermineCompositionType();
    }

    private CompositionType DetermineCompositionType()
    {
        if (Children.Count == 0)
            return CompositionType.None;

        if (Children.Count == 1)
            return CompositionType.Nested;

        if (ChildTypes.Count == 1)
            return CompositionType.Sequential;

        if (ChildTypes.Count <= 3 && IsLogicalGrouping(ChildTypes))
            return CompositionType.Grouped;

        return CompositionType.Mixed;
    }

    private bool IsLogicalGrouping(List<string> types)
    {
        var commonGroupings = new[]
        {
            new HashSet<string> { "avatar", "badge", "text" },
            new HashSet<string> { "icon", "text", "button" },
            new HashSet<string> { "image", "heading", "text", "button" },
            new HashSet<string> { "input", "label", "button" },
            new HashSet<string> { "avatar", "heading", "text" },
            new HashSet<string> { "badge", "text" }
        };

        var typeSet = new HashSet<string>(types, StringComparer.OrdinalIgnoreCase);
        return commonGroupings.Any(g => typeSet.IsSubsetOf(g));
    }

    public ComponentHierarchyNode? FindById(string id)
    {
        if (Id == id) return this;

        foreach (var child in Children)
        {
            var found = child.FindById(id);
            if (found != null) return found;
        }

        return null;
    }

    public IEnumerable<ComponentHierarchyNode> GetAllDescendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var descendant in child.GetAllDescendants())
                yield return descendant;
        }
    }

    public IEnumerable<ComponentHierarchyNode> GetAncestors()
    {
        var current = Parent;
        while (current != null)
        {
            yield return current;
            current = current.Parent;
        }
    }
}

public enum CompositionType
{
    None,           // Atomic component (no children)
    Nested,         // Single nested component
    Sequential,     // Children of same type (list)
    Grouped,        // Logically related children
    Mixed           // Various types of children
}
