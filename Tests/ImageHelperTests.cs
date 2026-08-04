using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace HealthyPet.Tests
{
    public class ImageHelperTests
    {
        public static void RunAll()
        {
            TestFramework.Run("ImageHelper", () =>
            {
                TestCropSolidBorderWhite();
                TestCropTransparentPng();
                TestNoCropNeeded();
                TestNullInput();
                TestSmallImageProtected();
            });
        }

        private static void TestCropSolidBorderWhite()
        {
            // 200x200 全白图，中心 100x100 红块
            using (var bmp = new Bitmap(200, 200, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    g.FillRectangle(Brushes.Red, 50, 50, 100, 100);
                }

                using (var cropped = ImageHelper.AutoCrop(bmp))
                {
                    // 红块从 50 到 150，应裁到约 100x100
                    TestFramework.AssertTrue(cropped.Width <= 110 && cropped.Width >= 95,
                        "白色边框裁掉后宽度≈100，实际=" + cropped.Width);
                    TestFramework.AssertTrue(cropped.Height <= 110 && cropped.Height >= 95,
                        "白色边框裁掉后高度≈100，实际=" + cropped.Height);
                    TestFramework.AssertTrue(cropped.Width < 200, "确实发生了裁剪");
                }
            }
        }

        private static void TestCropTransparentPng()
        {
            // 200x200 透明图，中心 100x100 不透明红块（alpha=255）
            using (var bmp = new Bitmap(200, 200, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.FillRectangle(Brushes.Red, 50, 50, 100, 100);
                }

                using (var cropped = ImageHelper.AutoCrop(bmp))
                {
                    TestFramework.AssertTrue(cropped.Width <= 110 && cropped.Width >= 95,
                        "透明区域裁掉后宽度≈100，实际=" + cropped.Width);
                    TestFramework.AssertTrue(cropped.Height <= 110 && cropped.Height >= 95,
                        "透明区域裁掉后高度≈100，实际=" + cropped.Height);
                }
            }
        }

        private static void TestNoCropNeeded()
        {
            // 全白图，无主体 —— 应保留原图（最小保护）
            using (var bmp = new Bitmap(100, 100, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(bmp))
                    g.Clear(Color.White);

                using (var cropped = ImageHelper.AutoCrop(bmp))
                {
                    TestFramework.AssertEqual(100, cropped.Width, "纯色无主体图保留原宽");
                    TestFramework.AssertEqual(100, cropped.Height, "纯色无主体图保留原高");
                }
            }
        }

        private static void TestNullInput()
        {
            Bitmap result = ImageHelper.AutoCrop(null);
            TestFramework.AssertNull(result, "空输入返回 null");
        }

        private static void TestSmallImageProtected()
        {
            // 极小图（10x10 红块居中），裁掉后若 < 10x10 应保留原图
            using (var bmp = new Bitmap(12, 12, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    g.FillRectangle(Brushes.Blue, 1, 1, 10, 10);
                }
                using (var cropped = ImageHelper.AutoCrop(bmp))
                {
                    // 10x10 主体，最小保护应保留 12x12 原图
                    TestFramework.AssertEqual(12, cropped.Width, "极小主体触发最小保护保留原图");
                }
            }
        }
    }
}
