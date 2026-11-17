using System.Windows;
using System.Windows.Media;

namespace LANPUT.Windows;

// Frozen vector drawings: no WebView, downloaded image, continuous render loop
// or third-party scene assets. The hall only animates during a mode transition.
public abstract class CinemaArtwork : FrameworkElement
{
    protected abstract DrawingGroup Artwork { get; }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        dc.PushTransform(new ScaleTransform(ActualWidth / 1400, ActualHeight / 800));
        dc.DrawDrawing(Artwork);
        dc.Pop(); dc.Pop();
    }
    protected static SolidColorBrush B(string hex)
    { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
    protected static LinearGradientBrush Gradient(string top, string bottom)
    { var b = new LinearGradientBrush(((SolidColorBrush)B(top)).Color, ((SolidColorBrush)B(bottom)).Color, 90); b.Freeze(); return b; }
    protected static void Shape(DrawingContext dc, Brush brush, params Point[] points)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open()) { g.BeginFigure(points[0], true, true); g.PolyLineTo(points.Skip(1).ToArray(), true, false); }
        geometry.Freeze(); dc.DrawGeometry(brush, null, geometry);
    }
    protected static void Line(DrawingContext dc, string color, double width, double x1, double y1, double x2, double y2)
        => dc.DrawLine(new Pen(B(color), width), new Point(x1, y1), new Point(x2, y2));
}

public sealed class CinemaHall : CinemaArtwork
{
    static readonly DrawingGroup scene = Create();
    protected override DrawingGroup Artwork => scene;
    static DrawingGroup Create()
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            dc.DrawRectangle(Gradient("#191A1B", "#040609"), null, new Rect(0, 0, 1400, 800));
            Shape(dc, B("#111518"), new(0, 0), new(1400, 0), new(1078, 88), new(322, 88));
            for (int i = 0; i < 11; i++) Line(dc, i % 2 == 0 ? "#070A0D" : "#24938364", i % 2 == 0 ? 4 : 1,
                i * 140, 0, 700 + (i * 140 - 700) * .57, 120);
            foreach (bool right in new[] { false, true })
            {
                dc.PushTransform(new MatrixTransform(right ? new Matrix(-1, 0, 0, 1, 1400, 0) : Matrix.Identity));
                Shape(dc, Gradient("#15191C", "#080B0F"), new(0, 0), new(308, 80), new(308, 488), new(0, 800));
                for (int i = 0; i < 6; i++) Line(dc, "#208E7954", 1, 35 + i * 43, 110 + i * 8, 35 + i * 43, 720 - i * 42);
                for (int i = 0; i < 3; i++)
                {
                    double x = 49 + i * 87, y = 272 + i * 12;
                    var light = new RadialGradientBrush();
                    light.GradientStops.Add(new GradientStop(Color.FromArgb(36, 232, 200, 122), 0));
                    light.GradientStops.Add(new GradientStop(Colors.Transparent, 1)); light.Freeze();
                    dc.DrawEllipse(light, null, new Point(x, y + 20), 65, 155);
                    dc.DrawRoundedRectangle(B("#AB8D55"), null, new Rect(x - 1, y, 2, 84 - i * 10), 1, 1);
                }
                dc.DrawRoundedRectangle(B("#05090B"), null, new Rect(98, 384, 57, 96), 2, 2);
                dc.DrawRectangle(B("#536C6554"), null, new Rect(105, 373, 42, 7));
                // Heavy side drapes, with soft vertical folds.
                for (int i = 0; i < 7; i++)
                    dc.DrawRectangle(Gradient(i % 2 == 0 ? "#18171B" : "#0A0B0E", "#06080B"), null, new Rect(i * 17, 0, 18, 800));
                dc.Pop();
            }
            Shape(dc, B("#211F1A"), new(679, 464), new(721, 464), new(791, 800), new(609, 800));
            for (int i = 0; i < 8; i++)
            {
                double y = 488 + i * 44, spread = (y / 800 - .58) * 224;
                Line(dc, "#66C5A468", 1.4, 700 - spread - 10, y, 700 - spread, y);
                Line(dc, "#66C5A468", 1.4, 700 + spread, y, 700 + spread + 10, y);
            }
            for (int row = 0; row < 5; row++)
            {
                double scale = .54 + row * .17, sw = 88.2 * scale, sh = 108 * scale, y = 484 + row * 69.6, aisle = 49 + row * 12.6;
                foreach (int side in new[] { -1, 1 }) for (int col = 0; col < 10; col++)
                {
                    double x = 700 + side * (aisle / 2 + col * sw * 1.15 + sw / 2) - sw / 2;
                    if (x < -sw || x > 1400) continue;
                    dc.DrawRoundedRectangle(B("#A0000000"), null, new Rect(x - 2, y + sh * .43, sw + 4, sh * .78), sw * .13, sw * .13);
                    dc.DrawRoundedRectangle(Gradient("#393039", "#11151C"), new Pen(B("#398D7A65"), .7), new Rect(x, y, sw, sh), sw * .18, sw * .18);
                    Line(dc, "#259E9297", .8, x + sw * .2, y + sh * .15, x + sw * .8, y + sh * .15);
                    dc.DrawRoundedRectangle(Gradient("#34303B", "#15171F"), null, new Rect(x + sw * .06, y + sh * .7, sw * .88, sh * .37), sw * .09, sw * .09);
                    dc.DrawRoundedRectangle(B("#0A0D12"), null, new Rect(x - sw * .055, y + sh * .51, sw * .12, sh * .49), sw * .05, sw * .05);
                    dc.DrawRoundedRectangle(B("#0A0D12"), null, new Rect(x + sw * .93, y + sh * .51, sw * .12, sh * .49), sw * .05, sw * .05);
                    dc.DrawEllipse(null, new Pen(B("#667C6C51"), .8), new Point(x + sw * .99, y + sh * .57), sw * .037, sh * .022);
                }
            }
            dc.DrawRectangle(Gradient("#0002070A", "#CE020407"), null, new Rect(0, 640, 1400, 160));
        }
        drawing.Freeze(); return drawing;
    }
}

public sealed class CinemaPoster : CinemaArtwork
{
    static readonly DrawingGroup scene = Create();
    protected override DrawingGroup Artwork => scene;
    static DrawingGroup Create()
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            dc.DrawRectangle(Gradient("#071A29", "#526163"), null, new Rect(0, 0, 1400, 800));
            var random = new Random(710);
            for (int i = 0; i < 110; i++) dc.DrawEllipse(B("#718AA8B5"), null,
                new Point(random.NextDouble() * 1400, random.NextDouble() * 540), .4 + random.NextDouble(), .4 + random.NextDouble());
            dc.DrawEllipse(B("#D4C6A0"), null, new Point(1060, 215), 31, 31);
            dc.DrawEllipse(B("#112B3C"), null, new Point(1073, 207), 29, 29);
            for (int layer = 0; layer < 4; layer++)
            {
                var points = new List<Point> { new(0, 800) };
                for (int x = 0; x <= 1450; x += 50) points.Add(new Point(x, 500 + layer * 80 - random.Next(30, 150)));
                points.Add(new Point(1400, 800));
                Shape(dc, B(new[] { "#374F58", "#233C4B", "#102937", "#081B29" }[layer]), points.ToArray());
            }
            dc.DrawRectangle(Gradient("#20030C14", "#D5030B12"), null, new Rect(0, 0, 1400, 800));
        }
        drawing.Freeze(); return drawing;
    }
}
