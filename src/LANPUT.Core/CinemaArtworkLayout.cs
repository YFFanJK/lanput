namespace LANPUT.Core;

public readonly record struct CinemaRect(double X, double Y, double Width, double Height)
{
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
}

// Coordinates refer to the unmodified, user-supplied 2752 x 1536 photograph.
// Both frontends must preserve its aspect ratio and place copy inside this screen.
public static class CinemaArtworkLayout
{
    public const double ImageWidth = 2752, ImageHeight = 1536;
    public static CinemaRect ScreenPixels => new(669, 262, 1416, 694);
    public const double ContentWidth = 800;
    public static double ContentHeight => ContentWidth * ScreenPixels.Height / ScreenPixels.Width;

    public static CinemaRect ScreenIn(double width, double height)
    {
        double scale = Math.Min(Math.Max(0, width) / ImageWidth, Math.Max(0, height) / ImageHeight);
        return new((width - ImageWidth * scale) / 2 + ScreenPixels.X * scale,
            (height - ImageHeight * scale) / 2 + ScreenPixels.Y * scale,
            ScreenPixels.Width * scale, ScreenPixels.Height * scale);
    }

    public static CinemaRect FrameAt(double width, double height, double progress)
    {
        var screen = ScreenIn(width, height);
        double p = Math.Clamp(progress, 0, 1);
        double start = screen.Width / ContentWidth;
        double end = Math.Max(width / ContentWidth, height / ContentHeight);
        double scale = start + (end - start) * p;
        double centerX = screen.CenterX + (width / 2 - screen.CenterX) * p;
        double centerY = screen.CenterY + (height / 2 - screen.CenterY) * p;
        return new(centerX - ContentWidth * scale / 2, centerY - ContentHeight * scale / 2,
            ContentWidth * scale, ContentHeight * scale);
    }
}
