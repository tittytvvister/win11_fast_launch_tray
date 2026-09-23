using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal sealed class IconFrame
{
    public int Size { get; set; }
    public byte[] Data { get; set; }
}

internal static class GenerateFastLaunchIcon
{
    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static IconFrame CreateFrame(int size)
    {
        using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);

            int margin = Math.Max(1, size / 32);
            int radius = Math.Max(2, size * 7 / 32);
            using (GraphicsPath background = RoundedRectangle(
                new Rectangle(margin, margin, size - margin * 2, size - margin * 2), radius))
            using (SolidBrush blue = new SolidBrush(Color.FromArgb(0, 120, 212)))
                graphics.FillPath(blue, background);

            int tileSize = Math.Max(3, size * 6 / 32);
            int tileRadius = Math.Max(1, size * 2 / 32);
            int[] positions = { size * 8 / 32, size * 18 / 32 };
            using (SolidBrush white = new SolidBrush(Color.White))
            {
                foreach (int y in positions)
                {
                    foreach (int x in positions)
                    {
                        using (GraphicsPath tile = RoundedRectangle(
                            new Rectangle(x, y, tileSize, tileSize), tileRadius))
                            graphics.FillPath(white, tile);
                    }
                }
            }

            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return new IconFrame { Size = size, Data = stream.ToArray() };
            }
        }
    }

    private static void WriteIcon(string path, IList<IconFrame> frames)
    {
        using (FileStream stream = File.Create(path))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)frames.Count);

            int offset = 6 + frames.Count * 16;
            foreach (IconFrame frame in frames)
            {
                writer.Write((byte)(frame.Size >= 256 ? 0 : frame.Size));
                writer.Write((byte)(frame.Size >= 256 ? 0 : frame.Size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)frame.Data.Length);
                writer.Write((uint)offset);
                offset += frame.Data.Length;
            }

            foreach (IconFrame frame in frames)
                writer.Write(frame.Data);
        }
    }

    public static int Main(string[] args)
    {
        if (args.Length != 1)
            return 2;

        int[] sizes = { 16, 20, 24, 32, 48, 64, 256 };
        List<IconFrame> frames = new List<IconFrame>();
        foreach (int size in sizes)
            frames.Add(CreateFrame(size));

        WriteIcon(args[0], frames);
        return 0;
    }
}
