using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using RismanNG.Infrastructure;

namespace RismanNG;

public sealed class ConnectionMap : Control
{
    private static readonly Color MapBackground = Color.FromArgb(13, 25, 39);
    private static readonly Color Land = Color.FromArgb(42, 66, 83);
    private static readonly Color Coast = Color.FromArgb(73, 104, 124);
    private readonly List<PointF[]> _land = [];
    private readonly System.Windows.Forms.Timer _timer;
    private float _phase;
    public GeoLocation? User { get; set; }
    public GeoLocation? Exit { get; set; }

    public ConnectionMap()
    {
        DoubleBuffered = true; BackColor = MapBackground; Font = new Font("Segoe UI", 8.5f);
        LoadWorldGeometry();
        _timer = new System.Windows.Forms.Timer { Interval = 35 };
        _timer.Tick += (_, _) => { _phase = (_phase + .009f) % 1f; if (User is not null && Exit is not null) Invalidate(); };
        _timer.Start();
    }

    private void LoadWorldGeometry()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "world-countries.geojson");
        if (!File.Exists(path)) return;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var feature in document.RootElement.GetProperty("features").EnumerateArray())
            {
                var geometry = feature.GetProperty("geometry"); var type = geometry.GetProperty("type").GetString(); var polygons = geometry.GetProperty("coordinates");
                if (type == "Polygon") AddPolygon(polygons);
                else if (type == "MultiPolygon") foreach (var polygon in polygons.EnumerateArray()) AddPolygon(polygon);
            }
        }
        catch { _land.Clear(); }
    }

    private void AddPolygon(JsonElement polygon)
    {
        if (polygon.ValueKind != JsonValueKind.Array || polygon.GetArrayLength() == 0) return;
        var ring = polygon[0]; var points = new List<PointF>(ring.GetArrayLength());
        foreach (var coordinate in ring.EnumerateArray())
        {
            if (coordinate.GetArrayLength() < 2) continue;
            points.Add(new PointF((float)coordinate[0].GetDouble(), (float)coordinate[1].GetDouble()));
        }
        if (points.Count < 4) return;

        var segment = new List<PointF>();
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            if (segment.Count > 0 && Math.Abs(current.X - segment[^1].X) > 180)
            {
                SaveSegment(segment);
                segment.Clear();
            }
            segment.Add(current);
        }
        SaveSegment(segment);
    }

    private void SaveSegment(List<PointF> segment)
    {
        if (segment.Count >= 3) _land.Add(segment.ToArray());
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(MapBackground);
        var map = FitWorldMap(RectangleF.Inflate(ClientRectangle, -14, -12));
        if (map.Width < 80 || map.Height < 50) return;
        DrawGraticule(g, map);
        using var landBrush = new SolidBrush(Land); using var coastPen = new Pen(Coast, 0.8f);
        foreach (var ring in _land)
        {
            var projected = ring.Select(point => Project(point.X, point.Y, map)).ToArray();
            if (projected.Length < 3) continue;
            try { g.FillPolygon(landBrush, projected); g.DrawPolygon(coastPen, projected); } catch (ArgumentException) { }
        }

        using var captionBrush = new SolidBrush(Color.FromArgb(176, 199, 218)); using var captionFont = new Font("Segoe UI Semibold", 8.5f);
        g.DrawString("LIVE ROUTE MAP", captionFont, captionBrush, map.Left + 4, map.Top + 2);
        if (User is null || Exit is null)
        {
            using var muted = new SolidBrush(Color.FromArgb(191, 203, 216));
            var message = _land.Count == 0 ? "World map data unavailable" : "Waiting for both public IP locations";
            g.DrawString(message, Font, muted, map.Left + 8, map.Bottom - 24);
            return;
        }

        var start = Project(User.Longitude, User.Latitude, map); var end = Project(Exit.Longitude, Exit.Latitude, map);
        DrawRoute(g, start, end, map);
        DrawEndpoint(g, start, Color.FromArgb(74, 222, 128), "YOU", User.Label, map);
        DrawEndpoint(g, end, Color.FromArgb(67, 142, 255), "EXIT", Exit.Label, map);
    }

    private static void DrawGraticule(Graphics g, RectangleF map)
    {
        using var grid = new Pen(Color.FromArgb(24, 155, 177, 199), 1);
        for (var longitude = -120; longitude <= 120; longitude += 60)
        {
            var x = map.Left + (longitude + 180) / 360f * map.Width;
            g.DrawLine(grid, x, map.Top, x, map.Bottom);
        }
        for (var latitude = -60; latitude <= 60; latitude += 30)
        {
            var y = map.Top + (90 - latitude) / 180f * map.Height;
            g.DrawLine(grid, map.Left, y, map.Right, y);
        }
    }

    private static PointF Project(double longitude, double latitude, RectangleF map)
    {
        var x = map.Left + (float)((longitude + 180d) / 360d * map.Width);
        var y = map.Top + (float)((90d - latitude) / 180d * map.Height);
        return new PointF(x, y);
    }

    private static RectangleF FitWorldMap(RectangleF bounds)
    {
        const float aspect = 2f;
        var width = Math.Min(bounds.Width, bounds.Height * aspect);
        var height = Math.Min(bounds.Height, bounds.Width / aspect);
        return new RectangleF(bounds.Left + (bounds.Width - width) / 2, bounds.Top + (bounds.Height - height) / 2, width, height);
    }

    private void DrawRoute(Graphics g, PointF start, PointF end, RectangleF map)
    {
        var distance = Math.Abs(end.X - start.X);
        var control = new PointF((start.X + end.X) / 2, Math.Max(map.Top + 25, Math.Min(start.Y, end.Y) - Math.Min(45, distance * .18f)));
        using var route = new GraphicsPath(); route.AddBezier(start, new PointF(start.X + (control.X - start.X) * .65f, control.Y), new PointF(end.X + (control.X - end.X) * .65f, control.Y), end);
        using var shadow = new Pen(Color.FromArgb(65, 67, 142, 255), 5); using var line = new Pen(Color.FromArgb(190, 94, 172, 255), 1.7f);
        g.DrawPath(shadow, route); g.DrawPath(line, route);
        var t = _phase; var q = 1 - t;
        var point = new PointF(q*q*q*start.X + 3*q*q*t*(start.X+(control.X-start.X)*.65f) + 3*q*t*t*(end.X+(control.X-end.X)*.65f) + t*t*t*end.X,
            q*q*q*start.Y + 3*q*q*t*control.Y + 3*q*t*t*control.Y + t*t*t*end.Y);
        using var halo = new SolidBrush(Color.FromArgb(70, 255, 255, 255)); using var pulse = new SolidBrush(Color.White);
        g.FillEllipse(halo, point.X - 8, point.Y - 8, 16, 16); g.FillEllipse(pulse, point.X - 3, point.Y - 3, 6, 6);
    }

    private static void DrawEndpoint(Graphics g, PointF point, Color color, string role, string location, RectangleF map)
    {
        using var halo = new SolidBrush(Color.FromArgb(55, color)); using var dot = new SolidBrush(color); using var label = new SolidBrush(Color.FromArgb(235, 235, 242, 249)); using var font = new Font("Segoe UI Semibold", 8);
        g.FillEllipse(halo, point.X - 10, point.Y - 10, 20, 20); g.FillEllipse(dot, point.X - 5, point.Y - 5, 10, 10);
        var text = $"{role}  {location}"; var size = g.MeasureString(text, font); var x = point.X + 10; if (x + size.Width > map.Right) x = point.X - size.Width - 10; x = Math.Max(map.Left, x);
        var y = Math.Clamp(point.Y + 8, map.Top + 22, map.Bottom - size.Height - 2); g.DrawString(text, font, label, x, y);
    }
}
