using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LANPUT.Desktop;

public sealed class VinylArtwork : Control
{
    double angle;
    public double Angle { get => angle; set { angle = value; InvalidateVisual(); } }
    public override void Render(DrawingContext dc)
    {
        base.Render(dc);
        double size = Math.Min(Bounds.Width, Bounds.Height), r = size * .43;
        var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
        dc.DrawEllipse(Brush.Parse("#05090E"), new Pen(Brush.Parse("#53606C"), 1), c, r, r);
        using (dc.PushTransform(Matrix.CreateTranslation(-c.X, -c.Y)
            * Matrix.CreateRotation(angle * Math.PI / 180) * Matrix.CreateTranslation(c.X, c.Y)))
        {
            for (double ring = .4; ring < 1; ring += .027)
                dc.DrawEllipse(null, new Pen(Brush.Parse("#242A33"), .7), c, r * ring, r * ring);
            dc.DrawEllipse(Brush.Parse("#D9BA6E"), null, c, r * .31, r * .31);
            // The off-centre label marks rotate with the complete record.
            dc.DrawLine(new Pen(Brush.Parse("#133254"), 7), new Point(c.X - r * .16, c.Y - r * .08), new Point(c.X + r * .16, c.Y - r * .08));
            dc.DrawLine(new Pen(Brush.Parse("#133254"), 3), new Point(c.X - r * .1, c.Y + r * .06), new Point(c.X + r * .1, c.Y + r * .06));
        }
        dc.DrawEllipse(Brush.Parse("#0A1017"), null, c, 4, 4);
        dc.DrawLine(new Pen(Brush.Parse("#A6B3BE"), 5), new Point(c.X + r * .96, c.Y - r * .85), new Point(c.X + r * .78, c.Y + r * .3));
        dc.DrawLine(new Pen(Brush.Parse("#D9BA6E"), 7), new Point(c.X + r * .78, c.Y + r * .3), new Point(c.X + r * .61, c.Y + r * .38));
    }
}
