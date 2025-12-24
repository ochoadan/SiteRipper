namespace SiteRipper.Layout;

using SiteRipper.Models;
using Microsoft.Playwright;

public class ResponsiveBreakpointDetector
{
    private readonly int[] _testWidths = { 320, 375, 480, 640, 768, 1024, 1280, 1440, 1920 };

    public async Task<List<ResponsiveBreakpoint>> DetectBreakpoints(
        IPage page,
        string selector,
        int originalWidth = 1920)
    {
        var breakpoints = new List<ResponsiveBreakpoint>();
        LayoutSnapshot? previousSnapshot = null;
        int breakpointStart = _testWidths[0];

        foreach (var width in _testWidths)
        {
            try
            {
                // Resize viewport
                await page.SetViewportSizeAsync(width, 1080);
                await page.WaitForTimeoutAsync(150); // Allow reflow

                // Get current layout snapshot
                var currentSnapshot = await GetLayoutSnapshot(page, selector);

                if (currentSnapshot == null) continue;

                // Check if layout changed significantly
                if (previousSnapshot != null && HasLayoutChanged(previousSnapshot, currentSnapshot))
                {
                    breakpoints.Add(new ResponsiveBreakpoint
                    {
                        MinWidth = breakpointStart,
                        MaxWidth = width - 1,
                        Name = GetBreakpointName(breakpointStart),
                        LayoutType = previousSnapshot.LayoutType,
                        Columns = previousSnapshot.Columns,
                        FlexDirection = previousSnapshot.FlexDirection
                    });

                    breakpointStart = width;
                }

                previousSnapshot = currentSnapshot;
            }
            catch
            {
                // Skip this width if there's an error
            }
        }

        // Add final breakpoint
        if (previousSnapshot != null)
        {
            breakpoints.Add(new ResponsiveBreakpoint
            {
                MinWidth = breakpointStart,
                MaxWidth = int.MaxValue,
                Name = GetBreakpointName(breakpointStart),
                LayoutType = previousSnapshot.LayoutType,
                Columns = previousSnapshot.Columns,
                FlexDirection = previousSnapshot.FlexDirection
            });
        }

        // Reset viewport
        await page.SetViewportSizeAsync(originalWidth, 1080);

        return breakpoints;
    }

    private async Task<LayoutSnapshot?> GetLayoutSnapshot(IPage page, string selector)
    {
        try
        {
            var result = await page.EvaluateAsync<LayoutSnapshotData?>($@"
                () => {{
                    const el = document.querySelector('{EscapeSelector(selector)}');
                    if (!el) return null;

                    const style = getComputedStyle(el);
                    const children = el.children;

                    // Count visible children
                    let visibleChildren = 0;
                    let childRects = [];

                    for (let i = 0; i < Math.min(children.length, 20); i++) {{
                        const rect = children[i].getBoundingClientRect();
                        if (rect.width > 0 && rect.height > 0) {{
                            visibleChildren++;
                            childRects.push({{ x: rect.x, y: rect.y, w: rect.width, h: rect.height }});
                        }}
                    }}

                    // Detect columns from child positions
                    const uniqueX = new Set(childRects.map(r => Math.round(r.x / 20) * 20)).size;
                    const uniqueY = new Set(childRects.map(r => Math.round(r.y / 20) * 20)).size;

                    return {{
                        display: style.display,
                        flexDirection: style.flexDirection,
                        gridTemplateColumns: style.gridTemplateColumns,
                        visibleChildren: visibleChildren,
                        uniqueXPositions: uniqueX,
                        uniqueYPositions: uniqueY,
                        containerWidth: el.offsetWidth
                    }};
                }}
            ");

            if (result == null) return null;

            var layoutType = result.display switch
            {
                "flex" or "inline-flex" => LayoutType.Flex,
                "grid" or "inline-grid" => LayoutType.Grid,
                _ => LayoutType.Block
            };

            // Infer column count
            int columns = 1;
            if (layoutType == LayoutType.Grid && !string.IsNullOrEmpty(result.gridTemplateColumns))
            {
                columns = result.gridTemplateColumns
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Length;
            }
            else if (result.uniqueXPositions > 1 && result.uniqueYPositions > 1)
            {
                columns = result.uniqueXPositions;
            }
            else if (result.flexDirection == "row" && result.uniqueXPositions > 1)
            {
                columns = result.uniqueXPositions;
            }

            return new LayoutSnapshot
            {
                LayoutType = layoutType,
                FlexDirection = result.flexDirection,
                Columns = columns,
                VisibleChildren = result.visibleChildren,
                ContainerWidth = result.containerWidth
            };
        }
        catch
        {
            return null;
        }
    }

    private bool HasLayoutChanged(LayoutSnapshot a, LayoutSnapshot b)
    {
        // Column count changed
        if (a.Columns != b.Columns) return true;

        // Flex direction changed
        if (a.FlexDirection != b.FlexDirection) return true;

        // Layout type changed
        if (a.LayoutType != b.LayoutType) return true;

        return false;
    }

    private string GetBreakpointName(int width)
    {
        return width switch
        {
            < 375 => "mobile-sm",
            < 480 => "mobile",
            < 640 => "mobile-lg",
            < 768 => "tablet-sm",
            < 1024 => "tablet",
            < 1280 => "desktop",
            < 1440 => "desktop-lg",
            _ => "desktop-xl"
        };
    }

    private string EscapeSelector(string selector)
    {
        return selector.Replace("'", "\\'").Replace("\"", "\\\"");
    }

    private class LayoutSnapshotData
    {
        public string? display { get; set; }
        public string? flexDirection { get; set; }
        public string? gridTemplateColumns { get; set; }
        public int visibleChildren { get; set; }
        public int uniqueXPositions { get; set; }
        public int uniqueYPositions { get; set; }
        public double containerWidth { get; set; }
    }

    private class LayoutSnapshot
    {
        public LayoutType LayoutType { get; set; }
        public string? FlexDirection { get; set; }
        public int Columns { get; set; }
        public int VisibleChildren { get; set; }
        public double ContainerWidth { get; set; }
    }
}
