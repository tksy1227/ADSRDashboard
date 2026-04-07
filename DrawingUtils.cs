using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ADSRDashboard
{
    public static class DrawingUtils
    {
        public static GraphicsPath RoundedRect(Rectangle r, int rad)
        {
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            path.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            path.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
            path.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static PointF[] HexPts(PointF c, float r)
        {
            var pts = new PointF[6];
            for (int i = 0; i < 6; i++) {
                double a = Math.PI / 180.0 * (60 * i - 30);
                pts[i] = new PointF(c.X + r * (float)Math.Cos(a), c.Y + r * (float)Math.Sin(a));
            }
            return pts;
        }

        public static Color DarkenColor(Color color, int percent)
        {
            float factor = 1f - (Math.Clamp(percent, 0, 100) / 100f);
            return Color.FromArgb(color.A, (int)(color.R * factor), (int)(color.G * factor), (int)(color.B * factor));
        }
    }
}