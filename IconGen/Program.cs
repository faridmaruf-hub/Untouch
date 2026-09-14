using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
var pngBlobs = new List<byte[]>();

foreach (var size in sizes)
{
    using var bmp = DrawTouchpad(size, Color.SeaGreen);
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    pngBlobs.Add(ms.ToArray());
}

var outPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Untouch", "icon.ico");
outPath = Path.GetFullPath(outPath);
WriteIco(outPath, sizes, pngBlobs);
Console.WriteLine($"Wrote {outPath}");

static Bitmap DrawTouchpad(int size, Color color)
{
    var bmp = new Bitmap(size, size);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.Clear(Color.Transparent);

    float margin = size * 0.0625f;
    float bodyHeight = size * 0.75f;
    var bounds = new RectangleF(margin, margin, size - margin * 2, bodyHeight);
    float radius = size * 0.15f;

    using var body = RoundedRect(bounds, radius);
    using var fill = new SolidBrush(color);
    g.FillPath(fill, body);

    float penWidth = Math.Max(1f, size / 21f);
    using var border = new Pen(Color.FromArgb(160, Color.Black), penWidth);
    g.DrawPath(border, body);

    float barY = bounds.Bottom - bounds.Height * 0.18f;
    using var clickBar = new Pen(Color.FromArgb(160, Color.Black), penWidth);
    g.DrawLine(clickBar, bounds.Left + bounds.Width * 0.12f, barY, bounds.Right - bounds.Width * 0.12f, barY);

    return bmp;
}

static GraphicsPath RoundedRect(RectangleF bounds, float radius)
{
    float d = radius * 2;
    var path = new GraphicsPath();
    path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
    path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
    path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
    path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}

static void WriteIco(string path, int[] sizes, List<byte[]> pngBlobs)
{
    using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
    using var w = new BinaryWriter(fs);

    w.Write((ushort)0);         // reserved
    w.Write((ushort)1);         // type: icon
    w.Write((ushort)sizes.Length);

    int headerSize = 6 + 16 * sizes.Length;
    int offset = headerSize;

    for (int i = 0; i < sizes.Length; i++)
    {
        byte b = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
        w.Write(b);              // width
        w.Write(b);              // height
        w.Write((byte)0);        // color count
        w.Write((byte)0);        // reserved
        w.Write((ushort)1);      // planes
        w.Write((ushort)32);     // bit count
        w.Write((uint)pngBlobs[i].Length);
        w.Write((uint)offset);
        offset += pngBlobs[i].Length;
    }

    foreach (var blob in pngBlobs)
        w.Write(blob);
}
