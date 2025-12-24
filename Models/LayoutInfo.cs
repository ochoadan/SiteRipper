namespace SiteRipper.Models;

public class LayoutInfo
{
    public string Selector { get; set; } = "";
    public LayoutType Type { get; set; }
    public FlexboxLayout? Flexbox { get; set; }
    public GridLayout? Grid { get; set; }
    public List<ResponsiveBreakpoint> Breakpoints { get; set; } = new();
}

public enum LayoutType
{
    Block,
    Flex,
    Grid,
    InlineBlock,
    InlineFlex,
    InlineGrid,
    Table,
    None
}

public class FlexboxLayout
{
    public string Direction { get; set; } = "row";
    public string JustifyContent { get; set; } = "flex-start";
    public string AlignItems { get; set; } = "stretch";
    public string Wrap { get; set; } = "nowrap";
    public double Gap { get; set; }
    public double RowGap { get; set; }
    public double ColumnGap { get; set; }
}

public class GridLayout
{
    public int Columns { get; set; }
    public int Rows { get; set; }
    public List<string> ColumnSizes { get; set; } = new();
    public List<string> RowSizes { get; set; } = new();
    public double Gap { get; set; }
    public double RowGap { get; set; }
    public double ColumnGap { get; set; }
    public List<GridArea> Areas { get; set; } = new();
}

public class GridArea
{
    public string Name { get; set; } = "";
    public int RowStart { get; set; }
    public int RowEnd { get; set; }
    public int ColStart { get; set; }
    public int ColEnd { get; set; }
}

public class ResponsiveBreakpoint
{
    public string Name { get; set; } = "";              // "mobile", "tablet", "desktop"
    public int MinWidth { get; set; }
    public int MaxWidth { get; set; }
    public LayoutType LayoutType { get; set; }
    public int? Columns { get; set; }                   // Grid columns at this breakpoint
    public string? FlexDirection { get; set; }          // Flex direction at this breakpoint
}

public class SpacingSystem
{
    public double BaseUnit { get; set; }                // e.g., 8 for 8px grid
    public List<double> Scale { get; set; } = new();    // [4, 8, 16, 24, 32, 48, 64]
    public Dictionary<string, int> GapFrequency { get; set; } = new();
    public Dictionary<string, int> PaddingFrequency { get; set; } = new();
    public Dictionary<string, int> MarginFrequency { get; set; } = new();
}
