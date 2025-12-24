namespace SiteRipper.Hierarchy;

using SiteRipper.Models;
using AngleSharp.Dom;

public class ComponentHierarchyBuilder
{
    public ComponentHierarchyNode BuildTree(
        IElement rootElement,
        List<DetectedComponent> detectedComponents)
    {
        // Create lookup for fast element -> component mapping
        var componentMap = detectedComponents.ToDictionary(
            c => c.DomIndex,
            c => c
        );

        // Get all elements for index lookup
        var allElements = rootElement.QuerySelectorAll("*").ToList();

        // Build tree recursively
        var root = BuildNode(rootElement, allElements, componentMap, null, 0);

        // Calculate all metrics
        root.CalculateMetrics();

        // Update component parent/child relationships
        UpdateComponentRelationships(root);

        return root;
    }

    private ComponentHierarchyNode BuildNode(
        IElement element,
        List<IElement> allElements,
        Dictionary<int, DetectedComponent> componentMap,
        ComponentHierarchyNode? parent,
        int depth)
    {
        var index = allElements.IndexOf(element);
        componentMap.TryGetValue(index, out var component);

        var node = new ComponentHierarchyNode
        {
            Id = component?.Id ?? Guid.NewGuid().ToString("N")[..8],
            ComponentType = component?.Type ?? "container",
            Selector = component?.Selector ?? GetSelector(element),
            Confidence = component?.Confidence ?? 0,
            Component = component,
            Parent = parent,
            Depth = depth
        };

        // Recursively process children
        foreach (var child in element.Children)
        {
            if (!ShouldProcessChild(child)) continue;

            var childNode = BuildNode(child, allElements, componentMap, node, depth + 1);

            // Only add if it has a component or has children with components
            if (childNode.Component != null || childNode.Children.Any())
            {
                node.Children.Add(childNode);
            }
        }

        return node;
    }

    private bool ShouldProcessChild(IElement element)
    {
        var tag = element.TagName.ToLower();
        var ignored = new HashSet<string>
        {
            "script", "style", "noscript", "meta", "link", "template"
        };
        return !ignored.Contains(tag);
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

    private void UpdateComponentRelationships(ComponentHierarchyNode root)
    {
        var queue = new Queue<ComponentHierarchyNode>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();

            if (node.Component != null)
            {
                // Set parent reference
                var parentWithComponent = node.GetAncestors()
                    .FirstOrDefault(a => a.Component != null);
                node.Component.ParentId = parentWithComponent?.Component?.Id;

                // Set child references
                node.Component.ChildIds = node.Children
                    .Where(c => c.Component != null)
                    .Select(c => c.Component!.Id)
                    .ToList();

                // Set composition
                node.Component.Composition = node.ChildTypes;
                node.Component.IsAtomic = node.IsAtomic;
                node.Component.IsCompound = node.IsCompound;
            }

            foreach (var child in node.Children)
                queue.Enqueue(child);
        }
    }

    public ComponentComposition AnalyzeComposition(ComponentHierarchyNode node)
    {
        var composition = new ComponentComposition
        {
            RootType = node.ComponentType,
            Selector = node.Selector
        };

        foreach (var child in node.Children)
        {
            if (child.Component != null)
            {
                composition.Children.Add(new ChildComponentInfo
                {
                    Type = child.ComponentType,
                    Selector = child.Selector,
                    Position = DeterminePosition(node, child),
                    Count = CountSimilarSiblings(node, child)
                });
            }

            // Recurse for nested compositions
            if (child.Children.Any())
            {
                composition.NestedCompositions.Add(AnalyzeComposition(child));
            }
        }

        return composition;
    }

    private string DeterminePosition(ComponentHierarchyNode parent, ComponentHierarchyNode child)
    {
        var index = parent.Children.IndexOf(child);
        var total = parent.Children.Count;

        if (index == 0) return "start";
        if (index == total - 1) return "end";
        return "middle";
    }

    private int CountSimilarSiblings(ComponentHierarchyNode parent, ComponentHierarchyNode child)
    {
        return parent.Children.Count(c => c.ComponentType == child.ComponentType);
    }
}

public class ComponentComposition
{
    public string RootType { get; set; } = "";
    public string Selector { get; set; } = "";
    public List<ChildComponentInfo> Children { get; set; } = new();
    public List<ComponentComposition> NestedCompositions { get; set; } = new();
}

public class ChildComponentInfo
{
    public string Type { get; set; } = "";
    public string Selector { get; set; } = "";
    public string Position { get; set; } = "";
    public int Count { get; set; }
}
