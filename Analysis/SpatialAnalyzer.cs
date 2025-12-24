namespace SiteRipper.Analysis;

using SiteRipper.Models;
using SiteRipper.Clustering;

/// <summary>
/// Shared spatial analysis utilities for layout detection
/// </summary>
public static class SpatialAnalyzer
{
    /// <summary>
    /// Analyze spatial arrangement of bounding boxes
    /// </summary>
    public static SpatialArrangement Analyze(List<BoundingBox> boxes)
    {
        if (boxes.Count < 2)
            return new SpatialArrangement();

        var yCoords = boxes.Select(b => b.Y).ToList();
        var yVariance = CalculateVariance(yCoords);

        var xCoords = boxes.Select(b => b.X).ToList();
        var xVariance = CalculateVariance(xCoords);

        var isHorizontal = yVariance < 50 && xVariance > 100;
        var isVertical = xVariance < 50 && yVariance > 100;
        var isGrid = IsGridPattern(boxes);

        return new SpatialArrangement
        {
            IsHorizontal = isHorizontal,
            IsVertical = isVertical,
            IsGrid = isGrid,
            Columns = isGrid ? DetectColumnCount(boxes) : (isHorizontal ? boxes.Count : 1),
            Rows = isGrid ? DetectRowCount(boxes) : (isVertical ? boxes.Count : 1),
            Gap = CalculateGap(boxes, isHorizontal)
        };
    }

    /// <summary>
    /// Determine layout type from arrangement
    /// </summary>
    public static string GetLayoutType(SpatialArrangement arrangement)
    {
        if (arrangement.IsGrid) return "grid";
        if (arrangement.IsHorizontal) return "row";
        if (arrangement.IsVertical) return "column";
        return "scattered";
    }

    public static double CalculateVariance(List<double> values)
    {
        if (values.Count < 2) return 0;
        var mean = values.Average();
        return values.Average(v => Math.Pow(v - mean, 2));
    }

    public static bool IsGridPattern(List<BoundingBox> boxes)
    {
        if (boxes.Count < 4) return false;

        var uniqueYs = boxes
            .Select(b => Math.Round(b.Y / 30) * 30)
            .Distinct()
            .ToList();

        var uniqueXs = boxes
            .Select(b => Math.Round(b.X / 50) * 50)
            .Distinct()
            .ToList();

        return uniqueYs.Count >= 2 && uniqueXs.Count >= 2;
    }

    public static int DetectColumnCount(List<BoundingBox> boxes)
    {
        return boxes
            .Select(b => Math.Round(b.X / 50) * 50)
            .Distinct()
            .Count();
    }

    public static int DetectRowCount(List<BoundingBox> boxes)
    {
        return boxes
            .Select(b => Math.Round(b.Y / 30) * 30)
            .Distinct()
            .Count();
    }

    public static double CalculateGap(List<BoundingBox> boxes, bool horizontal)
    {
        if (boxes.Count < 2) return 0;

        var sorted = horizontal
            ? boxes.OrderBy(b => b.X).ToList()
            : boxes.OrderBy(b => b.Y).ToList();

        var gaps = new List<double>();

        for (int i = 1; i < sorted.Count; i++)
        {
            var gap = horizontal
                ? sorted[i].X - sorted[i - 1].Right
                : sorted[i].Y - sorted[i - 1].Bottom;

            if (gap > 0 && gap < 200)
                gaps.Add(gap);
        }

        return gaps.Count > 0 ? Math.Round(gaps.Average()) : 0;
    }
}
