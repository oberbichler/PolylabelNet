using System;
using System.IO;
using System.Text.Json;
using System.Globalization;
using Xunit;
using Polylabel;

namespace Polylabel.Tests;

public class PolylabelTests
{
    private static Polygon LoadFixture(string filename)
    {
        string fullPath = Path.Combine(AppContext.BaseDirectory, "fixtures", filename);
        string json = File.ReadAllText(fullPath);
        double[][][] coords = JsonSerializer.Deserialize<double[][][]>(json)
            ?? throw new Exception($"Failed to deserialize {filename}");
        return new Polygon(coords);
    }

    [Fact]
    public void FindsPoleOfInaccessibilityForWater1AndPrecision1()
    {
        var water1 = LoadFixture("water1.json");
        var (point, distance) = Polylabel.Run(water1, 1.0);

        Assert.Equal(3865.85009765625, point.X);
        Assert.Equal(2124.87841796875, point.Y);
        Assert.Equal(288.8493574779127, distance, precision: 12);
    }

    [Fact]
    public void FindsPoleOfInaccessibilityForWater1AndPrecision50()
    {
        var water1 = LoadFixture("water1.json");
        var (point, distance) = Polylabel.Run(water1, 50.0);

        Assert.Equal(3854.296875, point.X);
        Assert.Equal(2123.828125, point.Y);
        Assert.Equal(278.5795872381558, distance, precision: 12);
    }

    [Fact]
    public void FindsPoleOfInaccessibilityForWater2AndDefaultPrecision1()
    {
        var water2 = LoadFixture("water2.json");
        var (point, distance) = Polylabel.Run(water2, 1.0);

        Assert.Equal(3263.5, point.X);
        Assert.Equal(3263.5, point.Y);
        Assert.Equal(960.5, distance, precision: 12);
    }

    [Fact]
    public void WorksOnDegeneratePolygons()
    {
        var p1Coords = new double[][][] { new double[][] { new double[] { 0, 0 }, new double[] { 1, 0 }, new double[] { 2, 0 }, new double[] { 0, 0 } } };
        var polygon1 = new Polygon(p1Coords);
        var (point1, distance1) = Polylabel.Run(polygon1);

        Assert.Equal(0, point1.X);
        Assert.Equal(0, point1.Y);
        Assert.Equal(0, distance1);

        var p2Coords = new double[][][] { new double[][] { new double[] { 0, 0 }, new double[] { 1, 0 }, new double[] { 1, 1 }, new double[] { 1, 0 }, new double[] { 0, 0 } } };
        var polygon2 = new Polygon(p2Coords);
        var (point2, distance2) = Polylabel.Run(polygon2);

        Assert.Equal(0, point2.X);
        Assert.Equal(0, point2.Y);
        Assert.Equal(0, distance2);
    }

    private readonly struct CustomVector2 : IPoint
    {
        public double X => XCoord;
        public double Y => YCoord;

        public double XCoord { get; }
        public double YCoord { get; }

        public CustomVector2(double x, double y)
        {
            XCoord = x;
            YCoord = y;
        }
    }

    [Fact]
    public void WorksWithCustomPointType()
    {
        var rings = new CustomVector2[][]
        {
            new CustomVector2[]
            {
                new CustomVector2(0, 0),
                new CustomVector2(10, 0),
                new CustomVector2(10, 10),
                new CustomVector2(0, 10),
                new CustomVector2(0, 0)
            }
        };

        var polygon = new Polygon<CustomVector2>(rings);
        var (point, distance) = Polylabel.Run(polygon, 1.0);

        Assert.Equal(5.0, point.X);
        Assert.Equal(5.0, point.Y);
        Assert.Equal(5.0, distance);
    }

    private readonly struct Vector2Adapter : IPoint
    {
        private readonly System.Numerics.Vector2 _vector;

        public double X => _vector.X;
        public double Y => _vector.Y;

        public Vector2Adapter(System.Numerics.Vector2 vector) => _vector = vector;
    }

    [Fact]
    public void WorksWithExternalVector2()
    {
        var rings = new System.Numerics.Vector2[][]
        {
            new System.Numerics.Vector2[]
            {
                new System.Numerics.Vector2(0, 0),
                new System.Numerics.Vector2(10, 0),
                new System.Numerics.Vector2(10, 10),
                new System.Numerics.Vector2(0, 10),
                new System.Numerics.Vector2(0, 0)
            }
        };

        var wrappedRings = Array.ConvertAll(rings,
            ring => Array.ConvertAll(ring, v => new Vector2Adapter(v)));

        var polygon = new Polygon<Vector2Adapter>(wrappedRings);
        var (point, distance) = Polylabel.Run(polygon, 1.0);

        Assert.Equal(5.0, point.X);
        Assert.Equal(5.0, point.Y);
        Assert.Equal(5.0, distance);
    }

    private readonly struct CustomPolygon : IPolygon<Point>
    {
        private readonly Point[] _outerRing;

        public int RingCount => 1;

        public ReadOnlySpan<Point> GetRing(int index) => index == 0 ? _outerRing : ReadOnlySpan<Point>.Empty;

        public CustomPolygon(Point[] outerRing) => _outerRing = outerRing;
    }

    [Fact]
    public void WorksWithCustomPolygonType()
    {
        var outerRing = new Point[]
        {
            new Point(0, 0),
            new Point(10, 0),
            new Point(10, 10),
            new Point(0, 10),
            new Point(0, 0)
        };

        var polygon = new CustomPolygon(outerRing);
        var (point, distance) = Polylabel.Run<CustomPolygon, Point>(polygon, 1.0);

        Assert.Equal(5.0, point.X);
        Assert.Equal(5.0, point.Y);
        Assert.Equal(5.0, distance);
    }

    [Fact]
    public void CalculateSvgPolygonResult()
    {
        var outerRing = new Point[]
        {
            new Point(15, 15),
            new Point(135, 15),
            new Point(135, 135),
            new Point(15, 135),
            new Point(15, 15)
        };
        var holeA = new Point[]
        {
            new Point(85, 35),
            new Point(125, 35),
            new Point(125, 85),
            new Point(85, 35)
        };
        var holeB = new Point[]
        {
            new Point(25, 80),
            new Point(55, 80),
            new Point(55, 125),
            new Point(25, 125),
            new Point(25, 80)
        };

        var polygon = new Polygon(new Point[][] { outerRing, holeA, holeB });
        var (point, distance) = Polylabel.Run(polygon, 0.01);
        
        Assert.Equal(90.7, point.X, 1);
        Assert.Equal(99.3, point.Y, 1);
        Assert.Equal(35.7, distance, 1);
    }

    [Fact]
    public void GenerateVisualResultSVGs()
    {
        // Compute path to images directory in workspace root
        string testProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        string workspaceRoot = Path.GetFullPath(Path.Combine(testProjectDir, ".."));
        string imagesDir = Path.Combine(workspaceRoot, "images");

        Directory.CreateDirectory(imagesDir);

        GenerateSVG("water1.json", Path.Combine(imagesDir, "water1.svg"));
        GenerateSVG("water2.json", Path.Combine(imagesDir, "water2.svg"));
    }

    private static void GenerateSVG(string filename, string outputPath)
    {
        var polygon = LoadFixture(filename);
        var result = Polylabel.Run(polygon, 1.0);

        // 1. Calculate Centroid locally
        double area = 0;
        double cx = 0;
        double cy = 0;
        var outerPoints = polygon.Rings[0];
        int len = outerPoints.Length;
        if (len > 0)
        {
            var b = outerPoints[len - 1];
            for (int i = 0; i < len; i++)
            {
                var a = outerPoints[i];
                double f = a.X * b.Y - b.X * a.Y;
                cx += (a.X + b.X) * f;
                cy += (a.Y + b.Y) * f;
                area += f * 3.0;
                b = a;
            }
        }
        double centroidX = area == 0 ? outerPoints[0].X : cx / area;
        double centroidY = area == 0 ? outerPoints[0].Y : cy / area;

        // 2. Compute bounding box for scaling
        double minX = double.PositiveInfinity;
        double minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double maxY = double.NegativeInfinity;
        foreach (var p in outerPoints)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        double width = maxX - minX;
        double height = maxY - minY;
        double maxDim = Math.Max(width, height);
        double scale = 440.0 / maxDim; // 30px padding on each side (500 - 60 = 440)
        double offsetX = 30.0 - minX * scale;
        double offsetY = 30.0 - minY * scale;

        (double X, double Y) Transform(double x, double y) => (x * scale + offsetX, y * scale + offsetY);

        double scaledRadius = result.Distance * scale;

        // Invariant culture formatter to ensure periods (.) are used as decimal separators instead of commas (,)
        string F(double val) => val.ToString("F2", CultureInfo.InvariantCulture);

        // 3. Construct SVG
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<svg width=\"500\" height=\"500\" viewBox=\"0 0 500 500\" xmlns=\"http://www.w3.org/2000/svg\">");
        sb.AppendLine("  <!-- Background grid pattern -->");
        sb.AppendLine("  <rect width=\"500\" height=\"500\" fill=\"#f8f9fa\"/>");
        sb.AppendLine("  <defs>");
        sb.AppendLine("    <pattern id=\"grid\" width=\"20\" height=\"20\" patternUnits=\"userSpaceOnUse\">");
        sb.AppendLine("      <path d=\"M 20 0 L 0 0 0 20\" fill=\"none\" stroke=\"#e9ecef\" stroke-width=\"1\"/>");
        sb.AppendLine("    </pattern>");
        sb.AppendLine("  </defs>");
        sb.AppendLine("  <rect width=\"500\" height=\"500\" fill=\"url(#grid)\"/>");

        // Polygon path with evenodd fill rule
        sb.Append("  <!-- Polygon shape (with holes punched via evenodd) -->");
        sb.Append("  <path fill-rule=\"evenodd\" fill=\"#228be6\" fill-opacity=\"0.12\" stroke=\"#1c7ed6\" stroke-width=\"1.5\" d=\"");
        for (int r = 0; r < polygon.Rings.Length; r++)
        {
            var ring = polygon.Rings[r];
            if (ring.Length == 0) continue;
            var start = Transform(ring[0].X, ring[0].Y);
            sb.Append($"M {F(start.X)} {F(start.Y)} ");
            for (int i = 1; i < ring.Length; i++)
            {
                var p = Transform(ring[i].X, ring[i].Y);
                sb.Append($"L {F(p.X)} {F(p.Y)} ");
            }
            sb.Append("Z ");
        }
        sb.AppendLine("\"/>");

        // Draw Polygon Centroid (blue cross)
        var scaledCentroid = Transform(centroidX, centroidY);
        sb.AppendLine("  <!-- Centroid (blue) -->");
        sb.AppendLine($"  <circle cx=\"{F(scaledCentroid.X)}\" cy=\"{F(scaledCentroid.Y)}\" r=\"4\" fill=\"#339af0\" stroke=\"#1c7ed6\" stroke-width=\"1\"/>");
        sb.AppendLine($"  <line x1=\"{F(scaledCentroid.X)}\" y1=\"{F(scaledCentroid.Y - 8)}\" x2=\"{F(scaledCentroid.X)}\" y2=\"{F(scaledCentroid.Y + 8)}\" stroke=\"#1c7ed6\" stroke-width=\"1\"/>");
        sb.AppendLine($"  <line x1=\"{F(scaledCentroid.X - 8)}\" y1=\"{F(scaledCentroid.Y)}\" x2=\"{F(scaledCentroid.X + 8)}\" y2=\"{F(scaledCentroid.Y)}\" stroke=\"#1c7ed6\" stroke-width=\"1\"/>");

        // Draw Pole of Inaccessibility (red) with concentric distance circle
        var scaledPole = Transform(result.Point.X, result.Point.Y);
        sb.AppendLine("  <!-- Pole of Inaccessibility (red) with distance circle -->");
        sb.AppendLine($"  <circle cx=\"{F(scaledPole.X)}\" cy=\"{F(scaledPole.Y)}\" r=\"{F(scaledRadius)}\" fill=\"none\" stroke=\"#fa5252\" stroke-width=\"1.5\" stroke-dasharray=\"3 3\"/>");
        sb.AppendLine($"  <circle cx=\"{F(scaledPole.X)}\" cy=\"{F(scaledPole.Y)}\" r=\"4\" fill=\"#fa5252\" stroke=\"#e03131\" stroke-width=\"1\"/>");
        sb.AppendLine($"  <circle cx=\"{F(scaledPole.X)}\" cy=\"{F(scaledPole.Y)}\" r=\"9\" fill=\"none\" stroke=\"#fa5252\" stroke-width=\"1\" stroke-dasharray=\"1.5 1.5\"/>");

        // Title and Legend
        sb.AppendLine("  <!-- Legend and Metadata -->");
        sb.AppendLine("  <rect x=\"15\" y=\"15\" width=\"210\" height=\"85\" rx=\"6\" fill=\"white\" fill-opacity=\"0.9\" stroke=\"#dee2e6\" stroke-width=\"1\"/>");
        sb.AppendLine($"  <text x=\"25\" y=\"32\" font-family=\"sans-serif\" font-size=\"11\" font-weight=\"bold\" fill=\"#212529\">{filename} Result</text>");
        sb.AppendLine("  <circle cx=\"30\" cy=\"52\" r=\"4\" fill=\"#fa5252\"/>");
        sb.AppendLine("  <text x=\"45\" y=\"55\" font-family=\"sans-serif\" font-size=\"10\" fill=\"#495057\">Pole of Inaccessibility</text>");
        sb.AppendLine("  <circle cx=\"30\" cy=\"72\" r=\"4\" fill=\"#339af0\"/>");
        sb.AppendLine("  <text x=\"45\" y=\"75\" font-family=\"sans-serif\" font-size=\"10\" fill=\"#495057\">Polygon Centroid</text>");

        sb.AppendLine("</svg>");

        File.WriteAllText(outputPath, sb.ToString());
    }
}
