using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace VAdapter.Automation.Ocr;

/// <summary>スクリーン領域のキャプチャ。</summary>
public static class ScreenCapture
{
    /// <summary>
    /// 指定スクリーン矩形をキャプチャして PNG バイト列で返す。
    /// <paramref name="scale"/> を 1 より大きくすると拡大して返す（OCR 精度向上のため）。
    /// </summary>
    public static byte[] CaptureRegionPng(int screenX, int screenY, int width, int height, double scale = 1.0)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException("キャプチャ領域のサイズが不正です。");

        using var source = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(source))
        {
            g.CopyFromScreen(screenX, screenY, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        }

        Bitmap output = source;
        Bitmap? scaled = null;
        if (scale > 1.0)
        {
            int sw = (int)Math.Round(width * scale);
            int sh = (int)Math.Round(height * scale);
            scaled = new Bitmap(sw, sh, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, sw, sh));
            }
            output = scaled;
        }

        try
        {
            using var ms = new MemoryStream();
            output.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        finally
        {
            scaled?.Dispose();
        }
    }
}
