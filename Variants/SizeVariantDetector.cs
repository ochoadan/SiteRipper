namespace SiteRipper.Variants;

using SiteRipper.Models;

public class SizeVariantDetector
{
    private readonly double[] _sizeBreakpoints = { 0.6, 0.85, 1.15, 1.4 };
    private readonly string[] _sizeNames = { "xs", "sm", "md", "lg", "xl" };

    public List<SizeVariant> DetectSizeVariants(List<DetectedComponent> components)
    {
        if (components.Count < 2) return new();

        // Extract size metrics
        var sizeMetrics = components.Select(c => new SizeMetric
        {
            Component = c,
            Width = c.VisualProperties.Width,
            Height = c.VisualProperties.Height,
            FontSize = c.VisualProperties.ParsedFontSize,
            Padding = c.VisualProperties.ParsedPadding,
            BorderRadius = c.VisualProperties.ParsedBorderRadius
        }).ToList();

        // Calculate median values (baseline for "md")
        var medianWidth = Median(sizeMetrics.Select(m => m.Width));
        var medianHeight = Median(sizeMetrics.Select(m => m.Height));
        var medianFontSize = Median(sizeMetrics.Select(m => m.FontSize));
        var medianPadding = Median(sizeMetrics.Select(m => m.Padding));
        var medianBorderRadius = Median(sizeMetrics.Select(m => m.BorderRadius));

        // Group by size bucket
        var variants = new Dictionary<string, SizeVariant>();

        foreach (var metric in sizeMetrics)
        {
            // Calculate size ratio relative to median
            var widthRatio = medianWidth > 0 ? metric.Width / medianWidth : 1;
            var heightRatio = medianHeight > 0 ? metric.Height / medianHeight : 1;
            var sizeRatio = (widthRatio + heightRatio) / 2;

            // Map to size bucket
            var sizeIndex = 2; // Default to md
            for (int i = 0; i < _sizeBreakpoints.Length; i++)
            {
                if (sizeRatio < _sizeBreakpoints[i])
                {
                    sizeIndex = i;
                    break;
                }
                sizeIndex = i + 1;
            }

            var sizeName = _sizeNames[Math.Clamp(sizeIndex, 0, _sizeNames.Length - 1)];

            if (!variants.ContainsKey(sizeName))
            {
                variants[sizeName] = new SizeVariant
                {
                    Name = sizeName,
                    InstanceCount = 0
                };
            }

            var v = variants[sizeName];
            var count = v.InstanceCount + 1;

            // Update running averages
            v.AvgWidth = UpdateAverage(v.AvgWidth, metric.Width, v.InstanceCount, count);
            v.AvgHeight = UpdateAverage(v.AvgHeight, metric.Height, v.InstanceCount, count);
            v.AvgFontSize = UpdateAverage(v.AvgFontSize, metric.FontSize, v.InstanceCount, count);
            v.AvgPadding = UpdateAverage(v.AvgPadding, metric.Padding, v.InstanceCount, count);
            v.AvgBorderRadius = UpdateAverage(v.AvgBorderRadius, metric.BorderRadius, v.InstanceCount, count);

            v.InstanceCount = count;
        }

        return variants.Values
            .Where(v => v.InstanceCount >= 1)
            .OrderBy(v => Array.IndexOf(_sizeNames, v.Name))
            .ToList();
    }

    private double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return 0;
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2
            : sorted[mid];
    }

    private double UpdateAverage(double currentAvg, double newValue, int oldCount, int newCount)
    {
        return (currentAvg * oldCount + newValue) / newCount;
    }

    private class SizeMetric
    {
        public DetectedComponent Component { get; set; } = null!;
        public double Width { get; set; }
        public double Height { get; set; }
        public double FontSize { get; set; }
        public double Padding { get; set; }
        public double BorderRadius { get; set; }
    }
}
