namespace SiteRipper.Models;

public class BoundingBox
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public double Area => Width * Height;
    public double AspectRatio => Height > 0 ? Width / Height : 0;

    public bool Intersects(BoundingBox other)
    {
        return X < other.Right && Right > other.X &&
               Y < other.Bottom && Bottom > other.Y;
    }

    public bool Contains(BoundingBox other)
    {
        return X <= other.X && Right >= other.Right &&
               Y <= other.Y && Bottom >= other.Bottom;
    }

    public double OverlapArea(BoundingBox other)
    {
        var overlapX = Math.Max(0, Math.Min(Right, other.Right) - Math.Max(X, other.X));
        var overlapY = Math.Max(0, Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y));
        return overlapX * overlapY;
    }
}
