namespace SiteRipper.Clustering;

using SiteRipper.Models;
using SiteRipper.Analysis;

public class VisualClusteringEngine
{
    private readonly double _epsilon;      // Maximum distance for neighbors
    private readonly int _minPoints;       // Minimum points to form a cluster

    public VisualClusteringEngine(double epsilon = 0.20, int minPoints = 2)
    {
        _epsilon = epsilon;
        _minPoints = minPoints;
    }

    public List<VisualCluster> Cluster(List<ClusterableElement> elements)
    {
        if (elements.Count < _minPoints)
            return new List<VisualCluster>();

        var vectors = elements.Select(e => e.FeatureVector.ToArray()).ToList();
        var labels = new int[vectors.Count];
        Array.Fill(labels, -1);  // -1 = unvisited

        int clusterId = 0;

        for (int i = 0; i < vectors.Count; i++)
        {
            if (labels[i] != -1) continue;  // Already processed

            var neighbors = GetNeighbors(vectors, i);

            if (neighbors.Count < _minPoints)
            {
                labels[i] = 0;  // Noise
                continue;
            }

            clusterId++;
            labels[i] = clusterId;
            elements[i].ClusterId = clusterId;

            // Expand cluster
            var seedSet = new Queue<int>(neighbors);
            while (seedSet.Count > 0)
            {
                var j = seedSet.Dequeue();

                if (labels[j] == 0)  // Was noise, now border point
                {
                    labels[j] = clusterId;
                    elements[j].ClusterId = clusterId;
                }

                if (labels[j] != -1) continue;  // Already processed

                labels[j] = clusterId;
                elements[j].ClusterId = clusterId;

                var jNeighbors = GetNeighbors(vectors, j);
                if (jNeighbors.Count >= _minPoints)
                {
                    foreach (var n in jNeighbors.Where(n => labels[n] <= 0))
                        seedSet.Enqueue(n);
                }
            }
        }

        // Build cluster objects
        return BuildClusters(elements, labels, clusterId);
    }

    private List<int> GetNeighbors(List<double[]> vectors, int pointIndex)
    {
        var neighbors = new List<int>();
        var point = vectors[pointIndex];

        for (int i = 0; i < vectors.Count; i++)
        {
            if (i == pointIndex) continue;
            if (EuclideanDistance(point, vectors[i]) <= _epsilon)
                neighbors.Add(i);
        }

        return neighbors;
    }

    private double EuclideanDistance(double[] a, double[] b)
    {
        double sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            var diff = a[i] - b[i];
            sum += diff * diff;
        }
        return Math.Sqrt(sum);
    }

    private List<VisualCluster> BuildClusters(
        List<ClusterableElement> elements,
        int[] labels,
        int maxClusterId)
    {
        var clusters = new List<VisualCluster>();

        for (int cId = 1; cId <= maxClusterId; cId++)
        {
            var clusterElements = elements
                .Where(e => e.ClusterId == cId)
                .ToList();

            if (clusterElements.Count < _minPoints) continue;

            var cluster = new VisualCluster
            {
                Id = cId,
                Elements = clusterElements,
                Centroid = CalculateCentroid(clusterElements),
                ComponentType = InferComponentType(clusterElements),
                Arrangement = AnalyzeArrangement(clusterElements)
            };

            clusters.Add(cluster);
        }

        return clusters;
    }

    private VisualFeatureVector CalculateCentroid(List<ClusterableElement> elements)
    {
        if (elements.Count == 0) return new VisualFeatureVector();

        var vectors = elements.Select(e => e.FeatureVector.ToArray()).ToList();
        var centroid = new double[vectors[0].Length];

        for (int i = 0; i < centroid.Length; i++)
        {
            centroid[i] = vectors.Average(v => v[i]);
        }

        return new VisualFeatureVector
        {
            NormalizedWidth = centroid[0],
            NormalizedHeight = centroid[1],
            AspectRatio = centroid[2],
            BackgroundH = centroid[3],
            BackgroundS = centroid[4],
            BackgroundL = centroid[5],
            TextH = centroid[6],
            TextS = centroid[7],
            TextL = centroid[8],
            FontSizeNormalized = centroid[9],
            FontWeightNormalized = centroid[10],
            PaddingNormalized = centroid[11],
            BorderRadiusNormalized = centroid[12],
            HasShadow = centroid[13],
            HasBorder = centroid[14]
        };
    }

    private string InferComponentType(List<ClusterableElement> elements)
    {
        // Get most common detected type
        var typeGroups = elements
            .GroupBy(e => e.Component.Type)
            .OrderByDescending(g => g.Count())
            .ToList();

        if (typeGroups.Count == 0) return "component-group";

        var primaryType = typeGroups[0].Key;
        var count = elements.Count;

        // If they're arranged in a grid/row pattern
        var arrangement = AnalyzeArrangement(elements);
        if (arrangement.IsGrid || arrangement.IsHorizontal)
        {
            return $"{primaryType}-grid";
        }

        if (count >= 2)
            return $"{primaryType}-group";

        return primaryType;
    }

    private SpatialArrangement AnalyzeArrangement(List<ClusterableElement> elements)
    {
        if (elements.Count < 2)
            return new SpatialArrangement();

        var boxes = elements.Select(e => e.Component.VisualProperties.BoundingBox).ToList();
        return SpatialAnalyzer.Analyze(boxes);
    }
}

public class VisualCluster
{
    public int Id { get; set; }
    public List<ClusterableElement> Elements { get; set; } = new();
    public VisualFeatureVector Centroid { get; set; } = new();
    public string ComponentType { get; set; } = "";
    public SpatialArrangement Arrangement { get; set; } = new();
}

public class SpatialArrangement
{
    public bool IsHorizontal { get; set; }
    public bool IsVertical { get; set; }
    public bool IsGrid { get; set; }
    public int Columns { get; set; }
    public int Rows { get; set; }
    public double Gap { get; set; }
}
