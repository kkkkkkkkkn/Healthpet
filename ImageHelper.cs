using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace HealthyPet
{
    /// <summary>
    /// 图片工具类 —— 自动裁剪空白边缘，只保留主体内容
    /// </summary>
    public static class ImageHelper
    {
        /// <summary>
        /// 自动裁剪图片四周的空白/纯色边缘
        /// PNG：以透明像素为裁剪依据
        /// JPG/BMP等不透明格式：以四角颜色为"背景色"裁剪
        /// </summary>
        public static Bitmap AutoCrop(Bitmap original)
        {
            if (original == null) return null;

            int w = original.Width;
            int h = original.Height;

            // 检测图片是否有 alpha 通道
            bool hasAlpha = (original.PixelFormat == PixelFormat.Format32bppArgb ||
                             original.PixelFormat == PixelFormat.Format32bppPArgb ||
                             original.PixelFormat == PixelFormat.Format64bppArgb ||
                             original.PixelFormat == PixelFormat.Format64bppPArgb);

            // 锁定位图，读取像素数据
            Rectangle rect = new Rectangle(0, 0, w, h);
            BitmapData bmpData = original.LockBits(rect, ImageLockMode.ReadOnly, original.PixelFormat);

            int bytesPerPixel = Image.GetPixelFormatSize(original.PixelFormat) / 8;
            int stride = bmpData.Stride;
            byte[] pixels = new byte[stride * h];
            Marshal.Copy(bmpData.Scan0, pixels, 0, pixels.Length);

            // 先解锁，后续裁剪需要原图处于未锁定状态
            original.UnlockBits(bmpData);

            int left, top, right, bottom;

            if (hasAlpha)
            {
                int alphaOffset = bytesPerPixel - 1; // alpha 在最后一字节
                FindBoundsAlpha(pixels, w, h, bytesPerPixel, stride, alphaOffset,
                    out left, out top, out right, out bottom);
            }
            else
            {
                FindBoundsSolid(pixels, w, h, bytesPerPixel, stride,
                    out left, out top, out right, out bottom);
            }

            return DoCrop(original, left, top, right, bottom);
        }

        /// <summary>
        /// 透明图片：按 alpha 通道找边界
        /// </summary>
        private static void FindBoundsAlpha(byte[] pixels, int w, int h,
            int bpp, int stride, int alphaOffset,
            out int left, out int top, out int right, out int bottom)
        {
            const int alphaThreshold = 10;

            // 从上往下
            top = 0;
            for (int y = 0; y < h; y++)
            {
                if (!IsRowTransparent(pixels, y, w, bpp, stride, alphaOffset, alphaThreshold))
                {
                    top = y;
                    break;
                }
                top = y;
            }

            // 从下往上
            bottom = h - 1;
            for (int y = h - 1; y >= 0; y--)
            {
                if (!IsRowTransparent(pixels, y, w, bpp, stride, alphaOffset, alphaThreshold))
                {
                    bottom = y;
                    break;
                }
                bottom = y;
            }

            // 从左往右
            left = 0;
            for (int x = 0; x < w; x++)
            {
                if (!IsColumnTransparent(pixels, x, h, bpp, stride, alphaOffset, alphaThreshold, top, bottom))
                {
                    left = x;
                    break;
                }
                left = x;
            }

            // 从右往左
            right = w - 1;
            for (int x = w - 1; x >= 0; x--)
            {
                if (!IsColumnTransparent(pixels, x, h, bpp, stride, alphaOffset, alphaThreshold, top, bottom))
                {
                    right = x;
                    break;
                }
                right = x;
            }
        }

        private static bool IsRowTransparent(byte[] pixels, int y, int w,
            int bpp, int stride, int alphaOffset, int threshold)
        {
            int rowStart = y * stride;
            // 采样加速：每隔几个像素检查一次
            int step = Math.Max(1, w / 50);
            for (int x = 0; x < w; x += step)
            {
                int idx = rowStart + x * bpp + alphaOffset;
                if (pixels[idx] > threshold)
                    return false;
            }
            // 如果采样没过，再精细扫描确认
            for (int x = 0; x < w; x++)
            {
                int idx = rowStart + x * bpp + alphaOffset;
                if (pixels[idx] > threshold)
                    return false;
            }
            return true;
        }

        private static bool IsColumnTransparent(byte[] pixels, int x, int h,
            int bpp, int stride, int alphaOffset, int threshold, int top, int bottom)
        {
            int colOffset = x * bpp;
            for (int y = top; y <= bottom; y++)
            {
                int idx = y * stride + colOffset + alphaOffset;
                if (pixels[idx] > threshold)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 不透明图片：按背景色找边界
        /// </summary>
        private static void FindBoundsSolid(byte[] pixels, int w, int h,
            int bpp, int stride,
            out int left, out int top, out int right, out int bottom)
        {
            int tolerance = 30;

            // 取四角平均色作为背景色
            byte bgR, bgG, bgB;
            GetAvgCornerColor(pixels, w, h, bpp, stride, out bgR, out bgG, out bgB);

            // 从上往下
            top = 0;
            for (int y = 0; y < h; y++)
            {
                if (!IsRowBackground(pixels, y, w, bpp, stride, bgR, bgG, bgB, tolerance))
                {
                    top = y;
                    break;
                }
                top = y;
            }

            // 从下往上
            bottom = h - 1;
            for (int y = h - 1; y >= 0; y--)
            {
                if (!IsRowBackground(pixels, y, w, bpp, stride, bgR, bgG, bgB, tolerance))
                {
                    bottom = y;
                    break;
                }
                bottom = y;
            }

            // 从左往右
            left = 0;
            for (int x = 0; x < w; x++)
            {
                if (!IsColumnBackground(pixels, x, h, bpp, stride, bgR, bgG, bgB, tolerance, top, bottom))
                {
                    left = x;
                    break;
                }
                left = x;
            }

            // 从右往左
            right = w - 1;
            for (int x = w - 1; x >= 0; x--)
            {
                if (!IsColumnBackground(pixels, x, h, bpp, stride, bgR, bgG, bgB, tolerance, top, bottom))
                {
                    right = x;
                    break;
                }
                right = x;
            }
        }

        private static void GetAvgCornerColor(byte[] pixels, int w, int h,
            int bpp, int stride, out byte bgR, out byte bgG, out byte bgB)
        {
            int rSum = 0, gSum = 0, bSum = 0;
            int[][] corners = new int[][] {
                new int[] { 0, 0 },
                new int[] { w - 1, 0 },
                new int[] { 0, h - 1 },
                new int[] { w - 1, h - 1 }
            };

            foreach (int[] corner in corners)
            {
                int cx = corner[0], cy = corner[1];
                int idx = cy * stride + cx * bpp;
                // Format24bppRgb: B,G,R; Format32bppRgb: B,G,R,X
                bSum += pixels[idx + 0];
                gSum += pixels[idx + 1];
                rSum += pixels[idx + 2];
            }

            bgR = (byte)(rSum / 4);
            bgG = (byte)(gSum / 4);
            bgB = (byte)(bSum / 4);
        }

        private static bool IsRowBackground(byte[] pixels, int y, int w,
            int bpp, int stride, byte bgR, byte bgG, byte bgB, int tolerance)
        {
            int rowStart = y * stride;
            // 采样加速
            int step = Math.Max(1, w / 50);
            for (int x = 0; x < w; x += step)
            {
                int idx = rowStart + x * bpp;
                if (ColorDiff(pixels[idx + 2], pixels[idx + 1], pixels[idx + 0],
                    bgR, bgG, bgB) > tolerance)
                    return false;
            }
            // 精细扫描确认
            for (int x = 0; x < w; x++)
            {
                int idx = rowStart + x * bpp;
                if (ColorDiff(pixels[idx + 2], pixels[idx + 1], pixels[idx + 0],
                    bgR, bgG, bgB) > tolerance)
                    return false;
            }
            return true;
        }

        private static bool IsColumnBackground(byte[] pixels, int x, int h,
            int bpp, int stride, byte bgR, byte bgG, byte bgB, int tolerance,
            int top, int bottom)
        {
            int colOffset = x * bpp;
            for (int y = top; y <= bottom; y++)
            {
                int idx = y * stride + colOffset;
                if (ColorDiff(pixels[idx + 2], pixels[idx + 1], pixels[idx + 0],
                    bgR, bgG, bgB) > tolerance)
                    return false;
            }
            return true;
        }

        private static int ColorDiff(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            return Math.Max(Math.Abs(r1 - r2), Math.Max(Math.Abs(g1 - g2), Math.Abs(b1 - b2)));
        }

        /// <summary>
        /// 执行裁剪，带最小尺寸保护
        /// </summary>
        private static Bitmap DoCrop(Bitmap original, int left, int top, int right, int bottom)
        {
            int cropW = right - left + 1;
            int cropH = bottom - top + 1;

            // 最小保护：至少保留 10x10
            if (cropW < 10 || cropH < 10)
                return new Bitmap(original);

            // 如果没裁剪什么（边缘裁掉 < 5px），保留原图
            if (left < 5 && top < 5 &&
                right > original.Width - 6 && bottom > original.Height - 6)
                return new Bitmap(original);

            // 执行裁剪
            Rectangle cropRect = new Rectangle(left, top, cropW, cropH);
            Bitmap cropped = new Bitmap(cropW, cropH, original.PixelFormat);
            using (Graphics g = Graphics.FromImage(cropped))
            {
                g.DrawImage(original,
                    new Rectangle(0, 0, cropW, cropH),
                    cropRect,
                    GraphicsUnit.Pixel);
            }
            return cropped;
        }
    }
}
