using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace TrayPingMonitor;

public static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Backward-compatible: creates a plain colored circle icon (used by your old MainApplicationContext).
    /// </summary>
    public static Icon CreateCircleIcon(Color color)
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var rect = new Rectangle(1, 1, 14, 14);
            using var brush = new SolidBrush(color);
            using var pen = new Pen(Color.FromArgb(180, 0, 0, 0), 1);

            g.FillEllipse(brush, rect);
            g.DrawEllipse(pen, rect);
        }

        return ToIconAndDestroyHandle(bmp);
    }

    /// <summary>
    /// New: colored circle background + small centered text (e.g., "23", "150", "1s", "X").
    /// </summary>
    public static Icon CreateStatusIcon(Color bg, string text)
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Background circle
            var rect = new Rectangle(1, 1, 14, 14);
            using var brush = new SolidBrush(bg);
            using var border = new Pen(Color.FromArgb(180, 0, 0, 0), 1);
            g.FillEllipse(brush, rect);
            g.DrawEllipse(border, rect);

            // Text (force single line)
            text = (text ?? "").Trim();
            if (text.Length > 3) text = text[..3];

            // Pick a font size based on length (keeps it readable)
            float fontSize = text.Length switch
            {
                1 => 8.0f,
                2 => 7.0f,
                _ => 6.0f
            };

            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Point);

            var flags = TextFormatFlags.HorizontalCenter
                    | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.SingleLine
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.NoClipping;

            // Shadow
            TextRenderer.DrawText(
                g,
                text,
                font,
                new Rectangle(1, 1, 16, 16),
                Color.FromArgb(220, 0, 0, 0),
                flags);

            // Foreground
            TextRenderer.DrawText(
                g,
                text,
                font,
                new Rectangle(0, 0, 16, 16),
                Color.White,
                flags);
        }

        return ToIconAndDestroyHandle(bmp);
    }


    private static Icon ToIconAndDestroyHandle(Bitmap bmp)
    {
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(hIcon);
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
}
