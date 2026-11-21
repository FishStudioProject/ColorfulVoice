using System.Globalization;
using System.Text;

namespace NanoFish.Interop;

/// <summary>音高曲线上的一个点（X 为相对音符起点的毫秒，Y 为半音）。</summary>
public sealed class PitchPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>'s' / 'r' / 'j'，'\0' 表示线性。</summary>
    public char Shape { get; set; }

    public PitchPoint Clone() => new() { X = X, Y = Y, Shape = Shape };
}

/// <summary>
/// UST 音高弯曲的读写：PBS（起点）+ PBW（段宽）+ PBY（拐点音高）+ PBM（形状）。
/// 内部统一表示为绝对点序列，编辑与绘制都基于它。
/// </summary>
public sealed class PitchCurve
{
    public List<PitchPoint> Points { get; } = new();

    public bool IsEmpty => Points.Count < 2;

    public PitchCurve Clone()
    {
        var c = new PitchCurve();
        foreach (var p in Points)
        {
            c.Points.Add(p.Clone());
        }
        return c;
    }

    /// <summary>从音符的附加字段里解析 PBS/PBW/PBY/PBM；没有曲线时返回 null。</summary>
    public static PitchCurve? FromExtras(string extras)
    {
        string? pbs = null, pbw = null, pby = null, pbm = null;
        foreach (var raw in extras.Split('\n'))
        {
            var line = raw.Trim();
            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            switch (key)
            {
                case "PBS": pbs = value; break;
                case "PBW": pbw = value; break;
                case "PBY": pby = value; break;
                case "PBM": pbm = value; break;
            }
        }

        if (pbs is null && pbw is null && pby is null)
        {
            return null;
        }

        var curve = new PitchCurve();
        var start = (pbs ?? "0;0").Split(pbs?.Contains(';') == true ? ';' : ',');
        curve.Points.Add(new PitchPoint
        {
            X = Num(start, 0),
            Y = Num(start, 1),
        });

        var widths = Split(pbw);
        var ys = Split(pby);
        var shapes = (pbm ?? string.Empty).Split(',');

        var x = curve.Points[0].X;
        for (var i = 0; i < widths.Count; i++)
        {
            x += widths[i];
            curve.Points.Add(new PitchPoint
            {
                X = x,
                Y = i < ys.Count ? ys[i] : 0,
                Shape = ShapeAt(shapes, i + 1),
            });
        }
        curve.Points[0].Shape = ShapeAt(shapes, 0);
        return curve.IsEmpty ? null : curve;
    }

    /// <summary>生成 PBS/PBW/PBY/PBM 行（不带换行结尾）。</summary>
    public string ToLines()
    {
        if (Points.Count < 2)
        {
            return string.Empty;
        }
        var sb = new StringBuilder();
        sb.Append("PBS=").Append(Fmt(Points[0].X)).Append(';').Append(Fmt(Points[0].Y)).Append('\n');

        var widths = new List<string>();
        var ys = new List<string>();
        var shapes = new List<string>();
        foreach (var p in Points)
        {
            shapes.Add(p.Shape switch { 's' => "s", 'r' => "r", 'j' => "j", _ => string.Empty });
        }
        for (var i = 1; i < Points.Count; i++)
        {
            widths.Add(Fmt(Points[i].X - Points[i - 1].X));
            ys.Add(Fmt(Points[i].Y));
        }
        sb.Append("PBW=").Append(string.Join(",", widths)).Append('\n');
        sb.Append("PBY=").Append(string.Join(",", ys)).Append('\n');
        sb.Append("PBM=").Append(string.Join(",", shapes));
        return sb.ToString();
    }

    /// <summary>把附加字段里的旧曲线行替换成新曲线（其余字段原样保留）。</summary>
    public static string ReplaceLines(string extras, PitchCurve? curve)
    {
        var kept = extras
            .Split('\n')
            .Where(line =>
            {
                var t = line.Trim();
                return !(t.StartsWith("PBS=") || t.StartsWith("PBW=") || t.StartsWith("PBY=") || t.StartsWith("PBM="));
            })
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (curve is { IsEmpty: false })
        {
            kept.Add(curve.ToLines());
        }
        return string.Join("\n", kept);
    }

    /// <summary>按 X（毫秒）线性插值求 Y。</summary>
    public double YAt(double x)
    {
        if (Points.Count == 0)
        {
            return 0;
        }
        if (x <= Points[0].X)
        {
            return Points[0].Y;
        }
        for (var i = 1; i < Points.Count; i++)
        {
            var a = Points[i - 1];
            var b = Points[i];
            if (x <= b.X)
            {
                var span = b.X - a.X;
                return span <= 1e-9 ? b.Y : a.Y + (b.Y - a.Y) * (x - a.X) / span;
            }
        }
        return Points[^1].Y;
    }

    private static List<double> Split(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? new List<double>()
            : text.Split(',')
                .Select(s => double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0.0)
                .ToList();

    private static double Num(string[] parts, int i) =>
        i < parts.Length && double.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0.0;

    private static char ShapeAt(string[] shapes, int i)
    {
        if (i >= shapes.Length)
        {
            return '\0';
        }
        return shapes[i].Trim() switch
        {
            "s" => 's',
            "r" => 'r',
            "j" => 'j',
            _ => '\0',
        };
    }

    private static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
