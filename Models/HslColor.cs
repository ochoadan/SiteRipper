namespace SiteRipper.Models;

using System.Text.RegularExpressions;

public class HslColor
{
    public double H { get; set; }  // 0-360
    public double S { get; set; }  // 0-1
    public double L { get; set; }  // 0-1

    public HslColor() { }

    public HslColor(double h, double s, double l)
    {
        H = h;
        S = s;
        L = l;
    }

    public static HslColor FromRgb(string rgbString)
    {
        var match = Regex.Match(rgbString, @"rgba?\((\d+),\s*(\d+),\s*(\d+)");
        if (!match.Success) return new HslColor();

        var r = int.Parse(match.Groups[1].Value) / 255.0;
        var g = int.Parse(match.Groups[2].Value) / 255.0;
        var b = int.Parse(match.Groups[3].Value) / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var l = (max + min) / 2.0;
        double h = 0, s = 0;

        if (delta != 0)
        {
            s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);

            if (max == r)
                h = ((g - b) / delta + (g < b ? 6 : 0)) * 60;
            else if (max == g)
                h = ((b - r) / delta + 2) * 60;
            else
                h = ((r - g) / delta + 4) * 60;
        }

        return new HslColor(h, s, l);
    }

    public string ToRgb()
    {
        double r, g, b;

        if (S == 0)
        {
            r = g = b = L;
        }
        else
        {
            var q = L < 0.5 ? L * (1 + S) : L + S - L * S;
            var p = 2 * L - q;
            r = HueToRgb(p, q, H / 360 + 1.0 / 3);
            g = HueToRgb(p, q, H / 360);
            b = HueToRgb(p, q, H / 360 - 1.0 / 3);
        }

        return $"rgb({(int)(r * 255)}, {(int)(g * 255)}, {(int)(b * 255)})";
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }

    public double DistanceTo(HslColor other)
    {
        var hueDiff = Math.Min(Math.Abs(H - other.H), 360 - Math.Abs(H - other.H)) / 180.0;
        var satDiff = Math.Abs(S - other.S);
        var lightDiff = Math.Abs(L - other.L);
        return Math.Sqrt(hueDiff * hueDiff + satDiff * satDiff + lightDiff * lightDiff);
    }
}
