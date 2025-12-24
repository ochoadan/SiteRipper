namespace SiteRipper.Clustering;

using SiteRipper.Models;
using SiteRipper.Detection;
using SiteRipper.Analysis;

public class RepeatedPatternDetector
{
    private readonly double _structuralSimilarityThreshold;

    public RepeatedPatternDetector(double structuralSimilarityThreshold = 0.8)
    {
        _structuralSimilarityThreshold = structuralSimilarityThreshold;
    }

    public List<RepeatedPattern> DetectPatterns(List<DetectedComponent> components)
    {
        var patterns = new List<RepeatedPattern>();

        // Group by structural fingerprint hash
        var hashGroups = components
            .GroupBy(c => c.Fingerprint.Hash)
            .Where(g => g.Count() >= 2)
            .ToList();

        foreach (var group in hashGroups)
        {
            var elements = group.ToList();
            var arrangement = AnalyzeSpatialArrangement(elements);

            patterns.Add(new RepeatedPattern
            {
                FingerprintHash = group.Key,
                Count = elements.Count,
                Arrangement = arrangement,
                LayoutType = DetermineLayoutType(arrangement),
                Elements = elements,
                SampleElement = elements.First(),
                ComponentType = elements.First().Type
            });
        }

        // Also detect patterns by structural similarity (not exact match)
        var nonExactPatterns = DetectSimilarPatterns(components, hashGroups);
        patterns.AddRange(nonExactPatterns);

        return patterns
            .OrderByDescending(p => p.Count)
            .ToList();
    }

    private List<RepeatedPattern> DetectSimilarPatterns(
        List<DetectedComponent> components,
        List<IGrouping<string, DetectedComponent>> existingGroups)
    {
        var patterns = new List<RepeatedPattern>();
        var alreadyGrouped = existingGroups
            .SelectMany(g => g.Select(c => c.Id))
            .ToHashSet();

        var ungrouped = components
            .Where(c => !alreadyGrouped.Contains(c.Id))
            .ToList();

        if (ungrouped.Count < 2) return patterns;

        // Find groups by structural similarity
        var processed = new HashSet<string>();

        foreach (var component in ungrouped)
        {
            if (processed.Contains(component.Id)) continue;

            var similar = ungrouped
                .Where(c => c.Id != component.Id &&
                           !processed.Contains(c.Id) &&
                           c.Fingerprint.SimilarityTo(component.Fingerprint) >= _structuralSimilarityThreshold)
                .ToList();

            if (similar.Count >= 1)
            {
                var group = new List<DetectedComponent> { component };
                group.AddRange(similar);

                var arrangement = AnalyzeSpatialArrangement(group);

                patterns.Add(new RepeatedPattern
                {
                    FingerprintHash = $"similar_{component.Id}",
                    Count = group.Count,
                    Arrangement = arrangement,
                    LayoutType = DetermineLayoutType(arrangement),
                    Elements = group,
                    SampleElement = component,
                    ComponentType = component.Type,
                    IsSimilarityBased = true
                });

                foreach (var c in group)
                    processed.Add(c.Id);
            }
        }

        return patterns;
    }

    private SpatialArrangement AnalyzeSpatialArrangement(List<DetectedComponent> elements)
    {
        var boxes = elements.Select(e => e.VisualProperties.BoundingBox).ToList();
        return SpatialAnalyzer.Analyze(boxes);
    }

    private string DetermineLayoutType(SpatialArrangement arrangement)
    {
        return SpatialAnalyzer.GetLayoutType(arrangement);
    }
}

public class RepeatedPattern
{
    public string FingerprintHash { get; set; } = "";
    public int Count { get; set; }
    public SpatialArrangement Arrangement { get; set; } = new();
    public string LayoutType { get; set; } = "";
    public List<DetectedComponent> Elements { get; set; } = new();
    public DetectedComponent SampleElement { get; set; } = null!;
    public string ComponentType { get; set; } = "";
    public bool IsSimilarityBased { get; set; }
}
