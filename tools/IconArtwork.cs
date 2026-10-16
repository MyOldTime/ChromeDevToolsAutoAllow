// Original, resolution-aware vector artwork. No third-party images or icon fonts.
// Design: midnight tile, mint shield, bold check. Tray drops the tile and decoration.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

internal static class IconArtwork
{
    private static Color C(string hex) { return ColorTranslator.FromHtml(hex); }
    private static GraphicsPath Round(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath(); float d = 2 * r;
        p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    private static GraphicsPath Shield()
    {
        var p = new GraphicsPath();
        p.AddBezier(128, 45, 147, 55, 170, 63, 188, 68);
        p.AddLine(188, 68, 188, 118);
        p.AddBezier(188, 118, 188, 155, 165, 183, 128, 207);
        p.AddBezier(128, 207, 91, 183, 68, 155, 68, 118);
        p.AddLine(68, 118, 68, 68);
        p.AddBezier(68, 68, 86, 63, 109, 55, 128, 45); p.CloseFigure(); return p;
    }
    private static void Check(Graphics g, Color color, float width, PointF a, PointF b, PointF c)
    {
        using (var p = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            g.DrawLines(p, new PointF[] { a, b, c });
    }
    internal static Bitmap Render(int size, bool tray)
    {
        int scale = size <= 128 ? 8 : 2;
        using (var hi = new Bitmap(size * scale, size * scale, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(hi))
            {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingQuality = CompositingQuality.HighQuality; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float unit = tray ? 64f : 256f; g.ScaleTransform(hi.Width / unit, hi.Height / unit);
                if (tray) DrawTray(g); else DrawApp(g, size <= 32);
            }
            var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            {
                g.Clear(Color.Transparent); g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.CompositingQuality = CompositingQuality.HighQuality; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(hi, new Rectangle(0, 0, size, size), 0, 0, hi.Width, hi.Height, GraphicsUnit.Pixel);
            }
            return result;
        }
    }
    private static void DrawApp(Graphics g, bool small)
    {
        using (var tile = Round(14, 14, 228, 228, 53))
        {
            using (var b = new LinearGradientBrush(new Point(26, 18), new Point(228, 244), C("#223E67"), C("#08142C"))) g.FillPath(b, tile);
            using (var p = new Pen(Color.FromArgb(82, 147, 188, 227), 1.2f)) g.DrawPath(p, tile);
        }
        var shieldState = g.Save();
        if (small) { g.TranslateTransform(128, 128); g.ScaleTransform(1.1f, 1.1f); g.TranslateTransform(-128, -128); }
        using (var shield = Shield())
        {
            if (!small)
            {
                var shadow = g.Save(); g.TranslateTransform(0, 7);
                using (var b = new SolidBrush(Color.FromArgb(55, 0, 4, 19))) g.FillPath(b, shield);
                g.Restore(shadow);
            }
            using (var b = new LinearGradientBrush(new Point(74, 52), new Point(172, 205), C("#73F7D2"), C("#08BFD3"))) g.FillPath(b, shield);
            if (!small)
            {
                using (var p = new Pen(Color.FromArgb(125, 207, 255, 243), 1.4f)) g.DrawPath(p, shield);
                using (var highlight = new GraphicsPath())
                {
                    highlight.AddBezier(128, 53, 110, 63, 89, 70, 76, 74);
                    using (var p = new Pen(Color.FromArgb(145, 232, 255, 248), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(p, highlight);
                }
            }
            Check(g, C("#0C354B"), small ? 18 : 16, new PointF(96, 120), new PointF(119, 143), new PointF(160, 99));
        }
        g.Restore(shieldState);
    }
    private static void DrawTray(Graphics g)
    {
        using (var p = new GraphicsPath())
        {
            p.AddBezier(32, 4, 40, 8, 48, 11, 56, 13);
            p.AddLine(56, 13, 56, 29); p.AddBezier(56, 29, 56, 42, 46, 52, 32, 60);
            p.AddBezier(32, 60, 18, 52, 8, 42, 8, 29); p.AddLine(8, 29, 8, 13);
            p.AddBezier(8, 13, 16, 11, 24, 8, 32, 4); p.CloseFigure();
            using (var b = new SolidBrush(C("#39DFC1"))) g.FillPath(b, p);
            using (var pen = new Pen(C("#126679"), 2.3f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, p);
            Check(g, C("#09334A"), 6.9f, new PointF(21, 30), new PointF(29, 38), new PointF(44, 23));
        }
    }
    private static byte[] Dib(Bitmap b)
    {
        int maskRow = ((b.Width + 31) / 32) * 4;
        using (var stream = new MemoryStream())
        using (var w = new BinaryWriter(stream))
        {
            w.Write(40); w.Write(b.Width); w.Write(b.Height * 2); w.Write((ushort)1); w.Write((ushort)32);
            w.Write(0); w.Write(b.Width * b.Height * 4 + maskRow * b.Height);
            w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = b.Height - 1; y >= 0; y--) for (int x = 0; x < b.Width; x++)
            { var c = b.GetPixel(x, y); w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A); }
            for (int y = b.Height - 1; y >= 0; y--)
            {
                byte[] row = new byte[maskRow];
                for (int x = 0; x < b.Width; x++) if (b.GetPixel(x, y).A == 0) row[x / 8] |= (byte)(0x80 >> (x % 8));
                w.Write(row);
            }
            return stream.ToArray();
        }
    }
    private static void SaveIco(string path, int[] sizes, bool tray)
    {
        var frames = new List<byte[]>();
        foreach (int size in sizes)
        using (var bitmap = Render(size, tray))
        {
            if (size < 256) frames.Add(Dib(bitmap));
            else using (var png = new MemoryStream()) { bitmap.Save(png, ImageFormat.Png); frames.Add(png.ToArray()); }
        }
        using (var stream = File.Create(path))
        using (var w = new BinaryWriter(stream))
        {
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length);
            int offset = 6 + sizes.Length * 16;
            for (int i = 0; i < sizes.Length; i++)
            {
                byte dimension = sizes[i] == 256 ? (byte)0 : (byte)sizes[i];
                w.Write(dimension); w.Write(dimension); w.Write((byte)0); w.Write((byte)0);
                w.Write((ushort)1); w.Write((ushort)32); w.Write(frames[i].Length); w.Write(offset); offset += frames[i].Length;
            }
            foreach (byte[] frame in frames) w.Write(frame);
        }
    }
    private static void SaveSvg(string output)
    {
        string head = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"256\" height=\"256\" viewBox=\"0 0 256 256\">";
        string shield = "M128 45C147 55 170 63 188 68V118C188 155 165 183 128 207C91 183 68 155 68 118V68C86 63 109 55 128 45Z";
        string app = head + "<defs><linearGradient id=\"tile\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\"><stop stop-color=\"#223e67\"/><stop offset=\"1\" stop-color=\"#08142c\"/></linearGradient><linearGradient id=\"mint\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\"><stop stop-color=\"#73f7d2\"/><stop offset=\"1\" stop-color=\"#08bfd3\"/></linearGradient></defs><rect x=\"14\" y=\"14\" width=\"228\" height=\"228\" rx=\"53\" fill=\"url(#tile)\" stroke=\"#93bce3\" stroke-opacity=\".32\" stroke-width=\"1.2\"/><path d=\"" + shield + "\" transform=\"translate(0 7)\" fill=\"#000413\" opacity=\".22\"/><path d=\"" + shield + "\" fill=\"url(#mint)\" stroke=\"#cffff3\" stroke-opacity=\".49\" stroke-width=\"1.4\"/><path d=\"M96 120L119 143L160 99\" fill=\"none\" stroke=\"#0c354b\" stroke-width=\"16\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>";
        string tray = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"64\" height=\"64\" viewBox=\"0 0 64 64\"><path d=\"M32 4C40 8 48 11 56 13V29C56 42 46 52 32 60C18 52 8 42 8 29V13C16 11 24 8 32 4Z\" fill=\"#39dfc1\" stroke=\"#126679\" stroke-width=\"2.3\" stroke-linejoin=\"round\"/><path d=\"M21 30L29 38L44 23\" fill=\"none\" stroke=\"#09334a\" stroke-width=\"6.9\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>";
        File.WriteAllText(Path.Combine(output, "app.svg"), app, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "tray.svg"), tray, new UTF8Encoding(false));
    }
    private static void Text(Graphics g, string text, float size, Color color, float x, float y, bool bold)
    {
        using (var font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(color)) g.DrawString(text, font, brush, x, y);
    }
    private static void Preview(string path)
    {
        using (var b = new Bitmap(1120, 640, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(C("#EEF2F7"));
            Text(g, "ChromeDevToolsAutoAllow", 30, C("#142942"), 36, 28, true);
            Text(g, "Mint shield / 专为 EXE 与系统托盘设计", 16, C("#60758B"), 38, 72, false);
            using (var card = Round(32, 118, 486, 488, 26)) using (var brush = new SolidBrush(C("#E2EAF3"))) g.FillPath(brush, card);
            Text(g, "APPLICATION", 13, C("#526B86"), 58, 141, true);
            using (var icon = Render(288, false)) g.DrawImageUnscaled(icon, 130, 192);
            Text(g, "EXE · 多尺寸应用图标", 20, C("#1B3550"), 156, 521, true);
            Text(g, "海军蓝底 · 薄荷青盾牌 · 清晰确认标记", 13, C("#60758B"), 138, 558, false);
            Color[] bg = { Color.White, C("#111B2E") };
            for (int j = 0; j < 2; j++)
            {
                int top = 118 + j * 258;
                using (var card = Round(546, top, 542, 230, 24)) using (var brush = new SolidBrush(bg[j])) g.FillPath(brush, card);
                Text(g, j == 0 ? "TRAY / 浅色任务栏" : "TRAY / 深色任务栏", 16, j == 0 ? C("#2D4761") : C("#C4D2E3"), 574, top + 25, true);
                int[] sizes = { 16, 20, 24, 32, 48 };
                int[] xs = { 580, 642, 710, 784, 866 };
                for (int i = 0; i < sizes.Length; i++)
                {
                    using (var icon = Render(sizes[i], true)) g.DrawImageUnscaled(icon, xs[i], top + 94 + (48 - sizes[i]) / 2);
                    Text(g, sizes[i] + " px", 12, j == 0 ? C("#688095") : C("#8096AF"), xs[i] - 5, top + 160, false);
                }
                using (var icon = Render(90, true)) g.DrawImageUnscaled(icon, 964, top + 70);
            }
            b.Save(path, ImageFormat.Png);
        }
    }
    private static int Main(string[] args)
    {
        try
        {
            string output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
            SaveIco(Path.Combine(output, "app.ico"), new int[] { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 }, false);
            SaveIco(Path.Combine(output, "tray.ico"), new int[] { 16, 20, 24, 32, 40, 48, 64 }, true);
            using (var b = Render(1024, false)) b.Save(Path.Combine(output, "app-1024.png"), ImageFormat.Png);
            using (var b = Render(256, true)) b.Save(Path.Combine(output, "tray-256.png"), ImageFormat.Png);
            SaveSvg(output); Preview(Path.Combine(output, "icon-preview.png"));
            Console.WriteLine("Generated original EXE/tray icons, multi-size ICO, PNG and SVG assets."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
