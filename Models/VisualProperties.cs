namespace SiteRipper.Models;

public class VisualProperties
{
    public BoundingBox BoundingBox { get; set; } = new();
    public Dictionary<string, string> Styles { get; set; } = new();

    // Computed properties
    public double Width => BoundingBox.Width;
    public double Height => BoundingBox.Height;

    public string? BackgroundColor => Styles.GetValueOrDefault("background-color");
    public string? Color => Styles.GetValueOrDefault("color");
    public string? FontSize => Styles.GetValueOrDefault("font-size");
    public string? FontFamily => Styles.GetValueOrDefault("font-family");
    public string? FontWeight => Styles.GetValueOrDefault("font-weight");
    public string? Padding => Styles.GetValueOrDefault("padding");
    public string? Margin => Styles.GetValueOrDefault("margin");
    public string? BorderRadius => Styles.GetValueOrDefault("border-radius");
    public string? Border => Styles.GetValueOrDefault("border");
    public string? BoxShadow => Styles.GetValueOrDefault("box-shadow");
    public string? Display => Styles.GetValueOrDefault("display");
    public string? Position => Styles.GetValueOrDefault("position");
    public string? ZIndex => Styles.GetValueOrDefault("z-index");
    public string? FlexDirection => Styles.GetValueOrDefault("flex-direction");
    public string? Gap => Styles.GetValueOrDefault("gap");

    // Text properties
    public string? TextAlign => Styles.GetValueOrDefault("text-align");
    public string? TextDecoration => Styles.GetValueOrDefault("text-decoration");
    public string? TextTransform => Styles.GetValueOrDefault("text-transform");
    public string? WhiteSpace => Styles.GetValueOrDefault("white-space");
    public string? VerticalAlign => Styles.GetValueOrDefault("vertical-align");
    public string? TextShadow => Styles.GetValueOrDefault("text-shadow");
    public string? TextOverflow => Styles.GetValueOrDefault("text-overflow");
    public string? LineHeight => Styles.GetValueOrDefault("line-height");
    public string? LetterSpacing => Styles.GetValueOrDefault("letter-spacing");

    // Flex/Grid properties
    public string? FlexWrap => Styles.GetValueOrDefault("flex-wrap");
    public string? FlexGrow => Styles.GetValueOrDefault("flex-grow");
    public string? FlexShrink => Styles.GetValueOrDefault("flex-shrink");
    public string? FlexBasis => Styles.GetValueOrDefault("flex-basis");
    public string? JustifyContent => Styles.GetValueOrDefault("justify-content");
    public string? AlignItems => Styles.GetValueOrDefault("align-items");
    public string? AlignContent => Styles.GetValueOrDefault("align-content");
    public string? AlignSelf => Styles.GetValueOrDefault("align-self");
    public string? Order => Styles.GetValueOrDefault("order");
    public string? GridTemplateColumns => Styles.GetValueOrDefault("grid-template-columns");
    public string? GridTemplateRows => Styles.GetValueOrDefault("grid-template-rows");

    // Size constraints
    public string? CssWidth => Styles.GetValueOrDefault("width");
    public string? CssHeight => Styles.GetValueOrDefault("height");
    public string? MaxWidth => Styles.GetValueOrDefault("max-width");
    public string? MinWidth => Styles.GetValueOrDefault("min-width");
    public string? MaxHeight => Styles.GetValueOrDefault("max-height");
    public string? MinHeight => Styles.GetValueOrDefault("min-height");

    // Overflow
    public string? Overflow => Styles.GetValueOrDefault("overflow");
    public string? OverflowX => Styles.GetValueOrDefault("overflow-x");
    public string? OverflowY => Styles.GetValueOrDefault("overflow-y");

    // Position offsets
    public string? Top => Styles.GetValueOrDefault("top");
    public string? Left => Styles.GetValueOrDefault("left");
    public string? Right => Styles.GetValueOrDefault("right");
    public string? Bottom => Styles.GetValueOrDefault("bottom");

    // Animation
    public string? Transition => Styles.GetValueOrDefault("transition");
    public string? Animation => Styles.GetValueOrDefault("animation");
    public string? Opacity => Styles.GetValueOrDefault("opacity");
    public string? Transform => Styles.GetValueOrDefault("transform");

    // Other
    public string? Cursor => Styles.GetValueOrDefault("cursor");
    public string? Visibility => Styles.GetValueOrDefault("visibility");
    public string? ObjectFit => Styles.GetValueOrDefault("object-fit");
    public string? AspectRatio => Styles.GetValueOrDefault("aspect-ratio");
    public string? BackgroundImage => Styles.GetValueOrDefault("background-image");

    public bool HasShadow => !string.IsNullOrEmpty(BoxShadow) && BoxShadow != "none";
    public bool HasBorder => !string.IsNullOrEmpty(Border) && !Border.Contains("0px") && Border != "none";
    public bool HasBorderRadius => !string.IsNullOrEmpty(BorderRadius) && BorderRadius != "0px";

    public bool HasDistinctBackground
    {
        get
        {
            if (string.IsNullOrEmpty(BackgroundColor)) return false;
            if (BackgroundColor.Contains("rgba(0, 0, 0, 0)")) return false;
            if (BackgroundColor == "transparent") return false;
            return true;
        }
    }

    public bool IsCentered
    {
        get
        {
            // Check if element is roughly centered on viewport
            var viewportCenter = 960; // Assuming 1920 width
            return Math.Abs(BoundingBox.CenterX - viewportCenter) < 200;
        }
    }

    public int ParsedZIndex
    {
        get
        {
            if (string.IsNullOrEmpty(ZIndex) || ZIndex == "auto") return 0;
            return int.TryParse(ZIndex, out var z) ? z : 0;
        }
    }

    public double ParsedFontSize
    {
        get
        {
            if (string.IsNullOrEmpty(FontSize)) return 16;
            var match = System.Text.RegularExpressions.Regex.Match(FontSize, @"([\d.]+)");
            return match.Success ? double.Parse(match.Groups[1].Value) : 16;
        }
    }

    public int ParsedFontWeight
    {
        get
        {
            if (string.IsNullOrEmpty(FontWeight)) return 400;
            return FontWeight switch
            {
                "normal" => 400,
                "bold" => 700,
                _ => int.TryParse(FontWeight, out var w) ? w : 400
            };
        }
    }

    public double ParsedPadding
    {
        get
        {
            if (string.IsNullOrEmpty(Padding)) return 0;
            var matches = System.Text.RegularExpressions.Regex.Matches(Padding, @"([\d.]+)px");
            return matches.Count > 0 ? matches.Average(m => double.Parse(m.Groups[1].Value)) : 0;
        }
    }

    public double ParsedBorderRadius
    {
        get
        {
            if (string.IsNullOrEmpty(BorderRadius)) return 0;
            var match = System.Text.RegularExpressions.Regex.Match(BorderRadius, @"([\d.]+)");
            return match.Success ? double.Parse(match.Groups[1].Value) : 0;
        }
    }

    public HslColor BackgroundHsl => HslColor.FromRgb(BackgroundColor ?? "");
    public HslColor TextHsl => HslColor.FromRgb(Color ?? "");
}
