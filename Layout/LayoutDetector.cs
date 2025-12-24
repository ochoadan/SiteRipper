namespace SiteRipper.Layout;

using SiteRipper.Models;
using Microsoft.Playwright;
using System.Text.RegularExpressions;

public class LayoutDetector
{
    public async Task<LayoutInfo> DetectLayout(IPage page, string selector, Dictionary<string, string> styles)
    {
        var layout = new LayoutInfo { Selector = selector };

        // Detect layout type from display property
        var display = styles.GetValueOrDefault("display", "block");
        layout.Type = ParseLayoutType(display);

        switch (layout.Type)
        {
            case LayoutType.Flex:
            case LayoutType.InlineFlex:
                layout.Flexbox = ParseFlexboxLayout(styles);
                break;
            case LayoutType.Grid:
            case LayoutType.InlineGrid:
                layout.Grid = await DetectGridLayout(page, selector, styles);
                break;
        }

        return layout;
    }

    private LayoutType ParseLayoutType(string display)
    {
        return display.ToLower() switch
        {
            "flex" => LayoutType.Flex,
            "inline-flex" => LayoutType.InlineFlex,
            "grid" => LayoutType.Grid,
            "inline-grid" => LayoutType.InlineGrid,
            "inline-block" => LayoutType.InlineBlock,
            "table" or "table-cell" or "table-row" => LayoutType.Table,
            "none" => LayoutType.None,
            _ => LayoutType.Block
        };
    }

    private FlexboxLayout ParseFlexboxLayout(Dictionary<string, string> styles)
    {
        return new FlexboxLayout
        {
            Direction = styles.GetValueOrDefault("flex-direction", "row"),
            JustifyContent = styles.GetValueOrDefault("justify-content", "flex-start"),
            AlignItems = styles.GetValueOrDefault("align-items", "stretch"),
            Wrap = styles.GetValueOrDefault("flex-wrap", "nowrap"),
            Gap = ParsePixelValue(styles.GetValueOrDefault("gap", "0")),
            RowGap = ParsePixelValue(styles.GetValueOrDefault("row-gap", "0")),
            ColumnGap = ParsePixelValue(styles.GetValueOrDefault("column-gap", "0"))
        };
    }

    private async Task<GridLayout> DetectGridLayout(IPage page, string selector, Dictionary<string, string> styles)
    {
        var gridLayout = new GridLayout
        {
            Gap = ParsePixelValue(styles.GetValueOrDefault("gap", "0")),
            RowGap = ParsePixelValue(styles.GetValueOrDefault("row-gap", "0")),
            ColumnGap = ParsePixelValue(styles.GetValueOrDefault("column-gap", "0"))
        };

        // Parse grid-template-columns
        var columns = styles.GetValueOrDefault("grid-template-columns", "");
        gridLayout.ColumnSizes = ParseGridTemplate(columns);
        gridLayout.Columns = gridLayout.ColumnSizes.Count;

        // Parse grid-template-rows
        var rows = styles.GetValueOrDefault("grid-template-rows", "");
        gridLayout.RowSizes = ParseGridTemplate(rows);
        gridLayout.Rows = Math.Max(gridLayout.RowSizes.Count, 1);

        // Try to get grid areas from JavaScript
        try
        {
            var areasJson = await page.EvaluateAsync<string>($@"
                () => {{
                    const el = document.querySelector('{EscapeSelector(selector)}');
                    if (!el) return '[]';

                    const areas = [];
                    const children = el.children;

                    for (let i = 0; i < Math.min(children.length, 50); i++) {{
                        const style = getComputedStyle(children[i]);
                        if (style.gridArea && style.gridArea !== 'auto') {{
                            areas.push({{
                                gridRow: style.gridRow,
                                gridColumn: style.gridColumn,
                                gridArea: style.gridArea
                            }});
                        }}
                    }}

                    return JSON.stringify(areas);
                }}
            ");

            if (!string.IsNullOrEmpty(areasJson))
            {
                var areas = System.Text.Json.JsonSerializer.Deserialize<List<GridAreaData>>(areasJson);
                gridLayout.Areas = areas?
                    .Where(a => !string.IsNullOrEmpty(a.gridArea) && a.gridArea != "auto")
                    .Select(a => ParseGridArea(a))
                    .Where(a => a != null)
                    .Cast<GridArea>()
                    .ToList() ?? new();
            }
        }
        catch
        {
            // Ignore grid area detection errors
        }

        return gridLayout;
    }

    private List<string> ParseGridTemplate(string template)
    {
        if (string.IsNullOrEmpty(template) || template == "none")
            return new List<string>();

        // Handle repeat() function
        var repeatMatch = Regex.Match(template, @"repeat\((\d+),\s*(.+?)\)");
        if (repeatMatch.Success)
        {
            var count = int.Parse(repeatMatch.Groups[1].Value);
            var value = repeatMatch.Groups[2].Value.Trim();
            return Enumerable.Repeat(value, count).ToList();
        }

        // Handle auto-fill/auto-fit
        var autoMatch = Regex.Match(template, @"repeat\((auto-fill|auto-fit),\s*(.+?)\)");
        if (autoMatch.Success)
        {
            return new List<string> { $"repeat({autoMatch.Groups[1].Value}, {autoMatch.Groups[2].Value})" };
        }

        // Split by spaces (simplified)
        return template
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private GridArea? ParseGridArea(GridAreaData data)
    {
        try
        {
            // Parse "1 / 1 / 2 / 3" format
            var parts = data.gridArea?.Split('/').Select(p => p.Trim()).ToArray();
            if (parts == null || parts.Length < 4) return null;

            return new GridArea
            {
                Name = data.gridArea ?? "",
                RowStart = int.TryParse(parts[0], out var rs) ? rs : 1,
                ColStart = int.TryParse(parts[1], out var cs) ? cs : 1,
                RowEnd = int.TryParse(parts[2], out var re) ? re : 2,
                ColEnd = int.TryParse(parts[3], out var ce) ? ce : 2
            };
        }
        catch
        {
            return null;
        }
    }

    private double ParsePixelValue(string value)
    {
        var match = Regex.Match(value, @"([\d.]+)");
        return match.Success ? double.Parse(match.Groups[1].Value) : 0;
    }

    private string EscapeSelector(string selector)
    {
        return selector.Replace("'", "\\'").Replace("\"", "\\\"");
    }

    private class GridAreaData
    {
        public string? gridRow { get; set; }
        public string? gridColumn { get; set; }
        public string? gridArea { get; set; }
    }
}
