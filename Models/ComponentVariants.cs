namespace SiteRipper.Models;

public class ComponentVariants
{
    public string ComponentType { get; set; } = "";
    public List<SizeVariant> SizeVariants { get; set; } = new();
    public List<ColorVariant> ColorVariants { get; set; } = new();
    public List<StateVariant> StateVariants { get; set; } = new();
}

public class SizeVariant
{
    public string Name { get; set; } = "";              // "xs", "sm", "md", "lg", "xl"
    public double AvgWidth { get; set; }
    public double AvgHeight { get; set; }
    public double AvgFontSize { get; set; }
    public double AvgPadding { get; set; }
    public double AvgBorderRadius { get; set; }
    public int InstanceCount { get; set; }
}

public class ColorVariant
{
    public string Name { get; set; } = "";              // "primary", "secondary", "danger"
    public string BackgroundColor { get; set; } = "";
    public string TextColor { get; set; } = "";
    public string? BorderColor { get; set; }
    public HslColor BackgroundHsl { get; set; } = new();
    public int InstanceCount { get; set; }
}

public class StateVariant
{
    public ComponentState State { get; set; }
    public Dictionary<string, string> StyleChanges { get; set; } = new();
    public bool IsDetected { get; set; }
}

public enum ComponentState
{
    Default,
    Hover,
    Active,
    Focus,
    Disabled,
    Loading,
    Error,
    Success
}
