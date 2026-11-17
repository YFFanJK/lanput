using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace LANPUT.Windows;

// Frozen vectors are rasterized once into the cached record surface. WPF animates
// only its rotation transform; the spindle label shares that same transform.
public abstract class MusicArtwork : FrameworkElement
{
    protected abstract DrawingGroup Artwork { get; }
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        dc.PushTransform(new ScaleTransform(ActualWidth / 600, ActualHeight / 600));
        dc.DrawDrawing(Artwork); dc.Pop();
    }
    protected static SolidColorBrush B(string color)
    { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!; b.Freeze(); return b; }
    protected static void Text(DrawingContext dc, string text, double y, double size, Brush color, bool bold = false)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, color, 1);
        dc.DrawText(formatted, new Point(300 - formatted.Width / 2, y));
    }
}

public sealed class VinylRecord : MusicArtwork
{
    static readonly DrawingGroup scene = Create();
    protected override DrawingGroup Artwork => scene;
    static DrawingGroup Create()
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            var center = new Point(300, 300);
            dc.DrawEllipse(B("#090E13"), new Pen(B("#434849"), 1), center, 298, 298);
            for (double radius = 104; radius < 296; radius += 3.4)
                dc.DrawEllipse(null, new Pen(B("#292F31"), .8), center, radius, radius);
            dc.PushClip(new EllipseGeometry(center, 296, 296));
            var silver = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, .7) };
            silver.GradientStops.Add(new(Color.FromArgb(0, 160, 184, 198), 0));
            silver.GradientStops.Add(new(Color.FromArgb(35, 160, 184, 198), .5));
            silver.GradientStops.Add(new(Color.FromArgb(0, 160, 184, 198), 1));
            dc.DrawGeometry(silver, null, Geometry.Parse("M300,300 L380,-20 L680,170 Z"));
            var gold = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            gold.GradientStops.Add(new(Color.FromArgb(0, 222, 196, 132), 0));
            gold.GradientStops.Add(new(Color.FromArgb(26, 222, 196, 132), .5));
            gold.GradientStops.Add(new(Color.FromArgb(0, 222, 196, 132), 1));
            dc.DrawGeometry(gold, null, Geometry.Parse("M300,300 L20,650 L-100,380 Z"));
            dc.Pop();
            var label = new RadialGradientBrush { GradientOrigin = new Point(.35, .25), Center = new Point(.4, .35), RadiusX = .8, RadiusY = .8 };
            label.GradientStops.Add(new(Color.FromRgb(240, 215, 155), 0));
            label.GradientStops.Add(new(Color.FromRgb(192, 147, 73), 1));
            dc.DrawEllipse(label, new Pen(B("#13191C"), 9), center, 89, 89);
            Text(dc, "L A N P U T", 253, 18, B("#302518"), true);
            Text(dc, "SIDE A  ·  33⅓ RPM", 342, 8.5, B("#67502D"));
            dc.DrawEllipse(B("#111A20"), null, center, 6, 6);
        }
        drawing.Freeze(); return drawing;
    }
}

public sealed class VinylOrbit : MusicArtwork
{
    static readonly DrawingGroup scene = Create();
    protected override DrawingGroup Artwork => scene;
    static DrawingGroup Create()
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            dc.PushTransform(new RotateTransform(-25, 300, 300));
            dc.DrawEllipse(null, new Pen(B("#35D0AD63"), 1), new(300, 300), 286, 264);
            dc.DrawEllipse(null, new Pen(B("#10FFFFFF"), .7), new(300, 300), 298, 298);
            dc.DrawEllipse(B("#E8C87A"), null, new(80, 132), 3, 3); dc.Pop();
        }
        drawing.Freeze(); return drawing;
    }
}

public sealed class VinylTonearm : MusicArtwork
{
    static readonly DrawingGroup scene = Create();
    protected override DrawingGroup Artwork => scene;
    static DrawingGroup Create()
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            dc.DrawLine(new Pen(B("#77000000"), 16), new(533, 80), new(435, 354));
            var metal = new LinearGradientBrush(Color.FromRgb(60, 69, 70), Color.FromRgb(209, 211, 189), new Point(0, 0), new Point(1, .3));
            dc.DrawLine(new Pen(metal, 10) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, new(521, 65), new(419, 348));
            var pivot = new RadialGradientBrush(Color.FromRgb(144, 150, 132), Color.FromRgb(36, 47, 51));
            dc.DrawEllipse(pivot, new Pen(B("#39474A"), 1), new(521, 65), 20, 20);
            dc.PushTransform(new RotateTransform(20, 422, 335));
            dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(24, 34, 38), Color.FromRgb(88, 96, 80), 0),
                new Pen(B("#777F6E"), 1), new Rect(409, 311, 26, 64), 4, 4); dc.Pop();
        }
        drawing.Freeze(); return drawing;
    }
}

public sealed class MusicAtmosphere : MusicArtwork
{
    static readonly DrawingGroup scene = Create();
    protected override DrawingGroup Artwork => scene;
    static DrawingGroup Create()
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            var random = new Random(3161);
            for (int i = 0; i < 65; i++)
                dc.DrawEllipse(B(i % 4 == 0 ? "#16E8C87A" : "#0AADBCC4"), null,
                    new(random.NextDouble() * 600, random.NextDouble() * 600), .15 + random.NextDouble() * .25, .25 + random.NextDouble() * .3);
            dc.DrawLine(new Pen(B("#08E8C87A"), .35), new(120, 600), new(440, 0));
        }
        drawing.Freeze(); return drawing;
    }
}
