using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
class IconBuilder
{
    static void Main(string[] args)
    {
        var images = new List<byte[]>(); int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        Directory.CreateDirectory(args[0]);
        foreach (int size in sizes) using (var b = new Bitmap(size, size)) {
            using (Graphics g = Graphics.FromImage(b)) {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.ScaleTransform(size / 256f, size / 256f);
                using (var bg = new SolidBrush(Color.FromArgb(25, 33, 43))) { g.FillEllipse(bg, 8, 8, 240, 240); }
                using (var teal = new Pen(Color.FromArgb(81, 220, 198), 14)) using (var light = new Pen(Color.FromArgb(221, 234, 238), 13)) {
                    teal.StartCap = teal.EndCap = LineCap.Round;
                    g.DrawRectangle(light, 72, 72, 112, 112);
                    for (int n = 94; n <= 162; n += 34) { g.DrawLine(light, n, 50, n, 67); g.DrawLine(light, n, 189, n, 206); }
                    g.DrawLine(teal, 28, 128, 105, 128); g.DrawLine(teal, 151, 128, 228, 128);
                    using (var cut = new Pen(Color.FromArgb(25, 33, 43), 30)) g.DrawLine(cut, 151, 94, 105, 162);
                    g.DrawLine(teal, 149, 91, 107, 165);
                }
            }
            if (size == 256) b.Save(Path.Combine(args[0], "nlns.png"), ImageFormat.Png);
            using (var ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); images.Add(ms.ToArray()); }
        }
        using (var w = new BinaryWriter(File.Create(Path.Combine(args[0], "nlns.ico")))) {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length); int offset = 6 + sizes.Length * 16;
            for (int i = 0; i < sizes.Length; i++) { w.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); w.Write((short)0); w.Write((short)1); w.Write((short)32); w.Write(images[i].Length); w.Write(offset); offset += images[i].Length; }
            foreach (byte[] bytes in images) w.Write(bytes);
        }
    }
}
