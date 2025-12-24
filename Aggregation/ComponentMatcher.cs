namespace SiteRipper.Aggregation;

using SiteRipper.Models;
using SiteRipper.Detection;
using SiteRipper.Clustering;

public class ComponentMatcher
{
    public const double ExactMatchThreshold = 1.0;
    public const double SimilarityThreshold = 0.8;
    public const double VisualDistanceThreshold = 0.20;

    public MatchResult Match(DetectedComponent a, DetectedComponent b)
    {
        // Exact hash match
        if (a.Fingerprint.Hash == b.Fingerprint.Hash)
        {
            return new MatchResult
            {
                IsMatch = true,
                MatchType = MatchType.Exact,
                StructuralSimilarity = 1.0,
                VisualDistance = CalculateVisualDistance(a, b)
            };
        }

        // Structural similarity match
        var similarity = a.Fingerprint.SimilarityTo(b.Fingerprint);
        if (similarity >= SimilarityThreshold && a.Type == b.Type)
        {
            var visualDistance = CalculateVisualDistance(a, b);
            if (visualDistance <= VisualDistanceThreshold)
            {
                return new MatchResult
                {
                    IsMatch = true,
                    MatchType = MatchType.Similar,
                    StructuralSimilarity = similarity,
                    VisualDistance = visualDistance
                };
            }
        }

        return new MatchResult
        {
            IsMatch = false,
            MatchType = MatchType.None,
            StructuralSimilarity = similarity,
            VisualDistance = CalculateVisualDistance(a, b)
        };
    }

    public List<ComponentGroup> GroupComponents(List<DetectedComponent> components)
    {
        var groups = new List<ComponentGroup>();
        var assigned = new HashSet<string>();

        // First pass: group by exact hash
        var hashGroups = components.GroupBy(c => c.Fingerprint.Hash);
        foreach (var group in hashGroups)
        {
            if (group.Count() >= 1)
            {
                var componentGroup = new ComponentGroup
                {
                    GroupId = Guid.NewGuid().ToString()[..8],
                    MatchType = MatchType.Exact,
                    FingerprintHash = group.Key,
                    Components = group.ToList()
                };
                groups.Add(componentGroup);

                foreach (var c in group)
                    assigned.Add(c.Id);
            }
        }

        // Second pass: group remaining by similarity
        var unassigned = components.Where(c => !assigned.Contains(c.Id)).ToList();

        for (int i = 0; i < unassigned.Count; i++)
        {
            if (assigned.Contains(unassigned[i].Id))
                continue;

            var similarGroup = new List<DetectedComponent> { unassigned[i] };
            assigned.Add(unassigned[i].Id);

            for (int j = i + 1; j < unassigned.Count; j++)
            {
                if (assigned.Contains(unassigned[j].Id))
                    continue;

                var matchResult = Match(unassigned[i], unassigned[j]);
                if (matchResult.IsMatch && matchResult.MatchType == MatchType.Similar)
                {
                    similarGroup.Add(unassigned[j]);
                    assigned.Add(unassigned[j].Id);
                }
            }

            if (similarGroup.Count >= 1)
            {
                groups.Add(new ComponentGroup
                {
                    GroupId = Guid.NewGuid().ToString()[..8],
                    MatchType = similarGroup.Count > 1 ? MatchType.Similar : MatchType.Unique,
                    FingerprintHash = unassigned[i].Fingerprint.Hash,
                    Components = similarGroup
                });
            }
        }

        return groups;
    }

    private double CalculateVisualDistance(DetectedComponent a, DetectedComponent b)
    {
        var vecA = VisualFeatureVector.FromComponent(a);
        var vecB = VisualFeatureVector.FromComponent(b);
        return vecA.DistanceTo(vecB);
    }
}

public class MatchResult
{
    public bool IsMatch { get; set; }
    public MatchType MatchType { get; set; }
    public double StructuralSimilarity { get; set; }
    public double VisualDistance { get; set; }
}

public enum MatchType
{
    None,
    Exact,
    Similar,
    Unique
}

public class ComponentGroup
{
    public string GroupId { get; set; } = "";
    public MatchType MatchType { get; set; }
    public string FingerprintHash { get; set; } = "";
    public List<DetectedComponent> Components { get; set; } = new();

    public int Count => Components.Count;
    public DetectedComponent Representative => Components.First();
}
