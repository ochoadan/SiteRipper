namespace SiteRipper.Layout;

using SiteRipper.Models;
using System.Text.RegularExpressions;

public class SpacingSystemDetector
{
    public SpacingSystem DetectSpacingSystem(List<DetectedComponent> components)
    {
        var allSpacings = new List<double>();
        var gapValues = new List<string>();
        var paddingValues = new List<string>();
        var marginValues = new List<string>();

        foreach (var component in components)
        {
            var styles = component.VisualProperties.Styles;

            // Collect gap values
            if (styles.TryGetValue("gap", out var gap) && !string.IsNullOrEmpty(gap))
            {
                gapValues.Add(gap);
                allSpacings.AddRange(ParseSpacingValues(gap));
            }

            // Collect padding values
            if (styles.TryGetValue("padding", out var padding) && !string.IsNullOrEmpty(padding))
            {
                paddingValues.Add(padding);
                allSpacings.AddRange(ParseSpacingValues(padding));
            }

            // Collect margin values
            if (styles.TryGetValue("margin", out var margin) && !string.IsNullOrEmpty(margin))
            {
                marginValues.Add(margin);
                allSpacings.AddRange(ParseSpacingValues(margin));
            }
        }

        // Find base unit
        var baseUnit = FindBaseUnit(allSpacings);

        // Generate scale
        var scale = GenerateScale(allSpacings, baseUnit);

        return new SpacingSystem
        {
            BaseUnit = baseUnit,
            Scale = scale,
            GapFrequency = CountFrequency(gapValues),
            PaddingFrequency = CountFrequency(paddingValues),
            MarginFrequency = CountFrequency(marginValues)
        };
    }

    private List<double> ParseSpacingValues(string value)
    {
        var values = new List<double>();
        var matches = Regex.Matches(value, @"([\d.]+)px");

        foreach (Match match in matches)
        {
            if (double.TryParse(match.Groups[1].Value, out var px))
            {
                values.Add(px);
            }
        }

        return values;
    }

    private double FindBaseUnit(List<double> values)
    {
        if (values.Count == 0) return 8; // Default to 8px

        // Filter to reasonable values
        var filtered = values
            .Where(v => v > 0 && v <= 200)
            .Select(v => Math.Round(v))
            .Where(v => v > 0)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        if (filtered.Count < 3) return 8;

        // Test common base units
        var candidates = new[] { 4.0, 5.0, 6.0, 8.0, 10.0, 12.0 };
        var scores = new Dictionary<double, int>();

        foreach (var candidate in candidates)
        {
            scores[candidate] = filtered.Count(v => v % candidate == 0);
        }

        // Return the base unit with most matches
        return scores.OrderByDescending(s => s.Value).First().Key;
    }

    private List<double> GenerateScale(List<double> values, double baseUnit)
    {
        // Get unique values that are multiples of base unit
        var validValues = values
            .Where(v => v > 0 && v <= 200)
            .Select(v => Math.Round(v))
            .Where(v => v > 0 && (v % baseUnit == 0 || v % (baseUnit / 2) == 0))
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        if (validValues.Count == 0)
        {
            // Generate default scale
            return new List<double>
            {
                baseUnit / 2,
                baseUnit,
                baseUnit * 1.5,
                baseUnit * 2,
                baseUnit * 3,
                baseUnit * 4,
                baseUnit * 6,
                baseUnit * 8
            }.Where(v => v >= 2).ToList();
        }

        // Use detected values, filling gaps if needed
        var scale = new List<double>();
        var expectedMultipliers = new[] { 0.5, 1, 1.5, 2, 3, 4, 6, 8, 12, 16 };

        foreach (var mult in expectedMultipliers)
        {
            var expected = baseUnit * mult;
            var closest = validValues.MinBy(v => Math.Abs(v - expected));

            if (closest != default && Math.Abs(closest - expected) <= baseUnit / 2)
            {
                if (!scale.Contains(closest))
                    scale.Add(closest);
            }
            else if (expected >= 2 && expected <= 128)
            {
                if (!scale.Contains(expected))
                    scale.Add(expected);
            }
        }

        return scale.OrderBy(v => v).Take(12).ToList();
    }

    private Dictionary<string, int> CountFrequency(List<string> values)
    {
        return values
            .Where(v => !string.IsNullOrEmpty(v) && v != "0px")
            .GroupBy(v => NormalizeSpacingValue(v))
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private string NormalizeSpacingValue(string value)
    {
        // Normalize "8px 16px 8px 16px" to "8px 16px"
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 4)
        {
            // Top/Bottom Left/Right
            if (parts[0] == parts[2] && parts[1] == parts[3])
                return $"{parts[0]} {parts[1]}";

            // All same
            if (parts.All(p => p == parts[0]))
                return parts[0];
        }

        return value;
    }
}
