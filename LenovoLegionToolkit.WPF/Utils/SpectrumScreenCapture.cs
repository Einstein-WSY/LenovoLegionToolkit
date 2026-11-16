using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// Spectrum 灯效的屏幕取色实现：抓取主屏画面并缩放到目标尺寸，转换为 RGB 颜色矩阵。
/// </summary>
public class SpectrumScreenCapture : SpectrumKeyboardBacklightController.IScreenCapture
{
    private const PixelFormat PIXEL_FORMAT = PixelFormat.Format32bppRgb;

    /// <summary>
    /// 捕获主屏并填充颜色缓冲区。
    /// </summary>
    /// <param name="buffer">输出的 RGB 颜色矩阵。</param>
    /// <param name="width">目标宽度。</param>
    /// <param name="height">目标高度。</param>
    /// <param name="token">取消令牌。</param>
    public void CaptureScreen(ref RGBColor[,] buffer, int width, int height, CancellationToken token)
    {
        var screen = Screen.PrimaryScreen?.Bounds ?? default;

        using var targetImage = new Bitmap(width, height, PIXEL_FORMAT);

        using (var image = new Bitmap(screen.Width, screen.Height, PIXEL_FORMAT))
        {
            using (var graphics = Graphics.FromImage(image))
                graphics.CopyFromScreen(screen.Left, screen.Top, 0, 0, screen.Size);

            token.ThrowIfCancellationRequested();

            // 使用双三次插值把整屏缩放到灯效所需的低分辨率
            using var targetGraphics = Graphics.FromImage(targetImage);
            targetGraphics.InterpolationMode = InterpolationMode.Bicubic;
            targetGraphics.DrawImage(image, new Rectangle(0, 0, width, height));
        }

        token.ThrowIfCancellationRequested();

        // 逐像素读取并写入颜色矩阵，期间持续响应取消
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var pixel = targetImage.GetPixel(x, y);
                buffer[x, y] = new RGBColor(pixel.R, pixel.G, pixel.B);

                token.ThrowIfCancellationRequested();
            }
        }
    }
}
