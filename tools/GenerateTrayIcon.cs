using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;

internal static class GenerateTrayIcon
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

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

    public static int Main(string[] args)
    {
        if (args.Length != 1)
            return 2;

        using (Bitmap bitmap = new Bitmap(32, 32))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using (GraphicsPath background = RoundedRectangle(new Rectangle(1, 1, 30, 30), 7))
            using (SolidBrush blue = new SolidBrush(Color.FromArgb(0, 120, 212)))
                graphics.FillPath(blue, background);

            using (SolidBrush white = new SolidBrush(Color.White))
            {
                int[] positions = { 8, 18 };
                foreach (int y in positions)
                    foreach (int x in positions)
                        using (GraphicsPath tile = RoundedRectangle(new Rectangle(x, y, 6, 6), 2))
                            graphics.FillPath(white, tile);
            }

            IntPtr handle = bitmap.GetHicon();
            try
            {
                using (Icon icon = Icon.FromHandle(handle))
                using (FileStream stream = File.Create(args[0]))
                    icon.Save(stream);
            }
            finally
            {
                DestroyIcon(handle);
            }
        }

        return 0;
    }
}
