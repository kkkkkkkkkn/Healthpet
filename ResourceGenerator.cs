using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace HealthyPet
{
    /// <summary>
    /// 资源生成器——首次运行时自动生成默认图片和名言文件
    /// </summary>
    public static class ResourceGenerator
    {
        /// <summary>
        /// 确保所有默认资源文件存在，不存在则自动生成。
        /// 优先尝试从网络下载更好的图片，失败则用本地绘制。
        /// </summary>
        public static void EnsureResourcesExist()
        {
            string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);

            string normalPath = Path.Combine(dataDir, "pet_normal.png");
            string dragPath = Path.Combine(dataDir, "pet_drag.png");
            string bgPath = Path.Combine(dataDir, "background.jpg");
            string iconPath = Path.Combine(dataDir, "tray.ico");
            string quotesPath = Path.Combine(dataDir, "quotes.txt");

            // 尝试下载更好的宠物图片（失败则用本地生成）
            if (!File.Exists(normalPath))
            {
                if (!TryDownloadImage(
                    "https://cdn.jsdelivr.net/gh/twitter/twemoji@latest/assets/72x72/1f431.png",
                    normalPath))
                {
                    GeneratePetNormal(normalPath);
                }
            }

            if (!File.Exists(dragPath))
            {
                if (!TryDownloadImage(
                    "https://cdn.jsdelivr.net/gh/twitter/twemoji@latest/assets/72x72/1f640.png",
                    dragPath))
                {
                    GeneratePetDrag(dragPath);
                }
            }

            if (!File.Exists(bgPath))
                GenerateBackground(bgPath);

            if (!File.Exists(iconPath))
                GenerateTrayIcon(iconPath);

            if (!File.Exists(quotesPath))
                GenerateQuotes(quotesPath);
        }

        /// <summary>
        /// 随附的《剑来》名言文件（与 exe 同目录）。存在则优先用它作为名言库。
        /// </summary>
        private static string JianLaiQuotesFile
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "名言名句.md"); }
        }

        /// <summary>
        /// 尝试用 PowerShell 从 URL 下载图片，返回是否成功
        /// </summary>
        private static bool TryDownloadImage(string url, string savePath)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = string.Format(
                        "-NoProfile -Command \"[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri '{0}' -OutFile '{1}' -TimeoutSec 10\"",
                        url.Replace("'", "''"), savePath.Replace("'", "''")),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    p.WaitForExit(8000);
                    return File.Exists(savePath) && new FileInfo(savePath).Length > 200;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 生成默认正常状态宠物图片——精致 Q 版橘猫（圆润可爱风）
        /// </summary>
        private static void GeneratePetNormal(string path)
        {
            int size = 256;
            using (var bmp = new Bitmap(size, size))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float cx = size / 2f;
                float cy = size * 0.55f;

                // 配色——温暖柔和的橘猫色调
                Color furLight = Color.FromArgb(255, 200, 140);
                Color furMid   = Color.FromArgb(245, 175, 110);
                Color furDark  = Color.FromArgb(210, 140, 75);
                Color furBelly = Color.FromArgb(255, 240, 220);
                Color earInner = Color.FromArgb(255, 190, 175);
                Color noseC    = Color.FromArgb(255, 140, 120);
                Color eyeIris  = Color.FromArgb(100, 180, 90);
                Color outline  = Color.FromArgb(160, 105, 60);

                // === 地面阴影 ===
                using (var shadowBrush = new SolidBrush(Color.FromArgb(35, 0, 0, 0)))
                {
                    g.FillEllipse(shadowBrush, cx - 55, cy + 60, 110, 22);
                }

                // === 尾巴（卷在身体右侧） ===
                DrawFurryTail(g, cx + 32, cy + 20, cx + 65, cy - 15, furMid, furDark, 12);

                // === 身体（圆润梨形）===
                using (var bodyPath = new GraphicsPath())
                {
                    bodyPath.AddEllipse(cx - 46, cy - 5, 92, 82);
                    using (var bodyBrush = new PathGradientBrush(bodyPath))
                    {
                        bodyBrush.CenterColor = furLight;
                        bodyBrush.SurroundColors = new Color[] { furMid };
                        bodyBrush.CenterPoint = new PointF(cx, cy + 15);
                        g.FillPath(bodyBrush, bodyPath);
                    }
                    using (var bodyPen = new Pen(outline, 2.8f))
                    {
                        bodyPen.LineJoin = LineJoin.Round;
                        g.DrawPath(bodyPen, bodyPath);
                    }
                }

                // === 肚皮毛（浅色椭圆）===
                using (var bellyBrush = new SolidBrush(furBelly))
                {
                    g.FillEllipse(bellyBrush, cx - 24, cy + 10, 48, 46);
                }

                // === 前爪 ===
                DrawPaw(g, cx - 30, cy + 42, 30, 24, furMid, furBelly, outline);
                DrawPaw(g, cx + 2, cy + 42, 30, 24, furMid, furBelly, outline);

                // === 头部（大圆脸）===
                using (var headPath = new GraphicsPath())
                {
                    headPath.AddEllipse(cx - 44, cy - 58, 88, 78);
                    using (var headBrush = new PathGradientBrush(headPath))
                    {
                        headBrush.CenterColor = furLight;
                        headBrush.SurroundColors = new Color[] { furMid };
                        headBrush.CenterPoint = new PointF(cx, cy - 30);
                        g.FillPath(headBrush, headPath);
                    }
                    using (var headPen = new Pen(outline, 2.8f))
                    {
                        headPen.LineJoin = LineJoin.Round;
                        g.DrawPath(headPen, headPath);
                    }
                }

                // === 耳朵 ===
                DrawBetterEar(g, cx - 29, cy - 60, -12, -45, furMid, earInner, outline, false);
                DrawBetterEar(g, cx + 29, cy - 60, 12, -45, furMid, earInner, outline, true);

                // === 额头「M」纹 ===
                DrawForeheadM(g, cx, cy - 48, furDark);

                // === 大眼睛（日系可爱风）===
                DrawKawaiiEye(g, cx - 18, cy - 28, 20, 24, eyeIris, outline);
                DrawKawaiiEye(g, cx + 18, cy - 28, 20, 24, eyeIris, outline);

                // === 鼻子 ===
                using (var noseBrush = new SolidBrush(noseC))
                using (var nosePen = new Pen(outline, 1.5f))
                {
                    var nosePath = new GraphicsPath();
                    nosePath.AddEllipse(cx - 6, cy - 5, 12, 8);
                    g.FillPath(noseBrush, nosePath);
                    g.DrawPath(nosePen, nosePath);
                }

                // === 嘴巴 ===
                using (var mouthPen = new Pen(outline, 1.5f))
                {
                    mouthPen.LineJoin = LineJoin.Round;
                    g.DrawLine(mouthPen, cx, cy + 3, cx - 7, cy + 12);
                    g.DrawLine(mouthPen, cx, cy + 3, cx + 7, cy + 12);
                    g.DrawCurve(mouthPen, new Point[] {
                        new Point((int)cx - 7, (int)cy + 12),
                        new Point((int)cx - 3, (int)cy + 9),
                        new Point((int)cx + 3, (int)cy + 9),
                        new Point((int)cx + 7, (int)cy + 12)
                    });
                }

                // === 胡须 ===
                DrawWhiskers(g, cx, cy + 2, outline);

                // === 腮红 ===
                using (var blushBrush = new SolidBrush(Color.FromArgb(70, 255, 150, 150)))
                {
                    g.FillEllipse(blushBrush, cx - 34, cy - 2, 18, 11);
                    g.FillEllipse(blushBrush, cx + 16, cy - 2, 18, 11);
                }

                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        /// <summary>
        /// 生成拖拽状态宠物图片——被拎起的橘猫（四肢下垂 + 惊讶表情）
        /// </summary>
        private static void GeneratePetDrag(string path)
        {
            int size = 256;
            using (var bmp = new Bitmap(size, size + 40))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float cx = size / 2f;
                float cy = 65f;

                Color furLight = Color.FromArgb(255, 200, 140);
                Color furMid   = Color.FromArgb(245, 175, 110);
                Color furDark  = Color.FromArgb(210, 140, 75);
                Color furBelly = Color.FromArgb(255, 240, 220);
                Color earInner = Color.FromArgb(255, 190, 175);
                Color noseC    = Color.FromArgb(255, 140, 120);
                Color eyeIris  = Color.FromArgb(100, 180, 90);
                Color outline  = Color.FromArgb(160, 105, 60);

                // === 拎起指示（虚线三角 + 光点）===
                using (var grabPen = new Pen(Color.FromArgb(140, 160, 170), 2f))
                {
                    grabPen.DashStyle = DashStyle.Dash;
                    g.DrawLine(grabPen, cx - 18, 18, cx, 42);
                    g.DrawLine(grabPen, cx + 18, 18, cx, 42);
                    g.DrawLine(grabPen, cx - 18, 18, cx + 18, 18);
                }
                using (var glowBrush = new SolidBrush(Color.FromArgb(255, 255, 160)))
                {
                    g.FillEllipse(glowBrush, cx - 22, 10, 44, 10);
                }

                // === 身体（拉长）===
                using (var bodyPath = new GraphicsPath())
                {
                    bodyPath.AddEllipse(cx - 38, cy + 12, 76, 95);
                    using (var bodyBrush = new PathGradientBrush(bodyPath))
                    {
                        bodyBrush.CenterColor = furLight;
                        bodyBrush.SurroundColors = new Color[] { furMid };
                        bodyBrush.CenterPoint = new PointF(cx, cy + 40);
                        g.FillPath(bodyBrush, bodyPath);
                    }
                    using (var bodyPen = new Pen(outline, 2.8f))
                    {
                        bodyPen.LineJoin = LineJoin.Round;
                        g.DrawPath(bodyPen, bodyPath);
                    }
                }

                // === 肚皮 ===
                using (var bellyBrush = new SolidBrush(furBelly))
                {
                    g.FillEllipse(bellyBrush, cx - 20, cy + 25, 40, 55);
                }

                // === 下垂尾巴 ===
                DrawFurryTail(g, cx + 28, cy + 50, cx + 50, cy + 110, furMid, furDark, 10);

                // === 头部 ===
                using (var headPath = new GraphicsPath())
                {
                    headPath.AddEllipse(cx - 36, cy - 38, 72, 62);
                    using (var headBrush = new PathGradientBrush(headPath))
                    {
                        headBrush.CenterColor = furLight;
                        headBrush.SurroundColors = new Color[] { furMid };
                        headBrush.CenterPoint = new PointF(cx, cy - 18);
                        g.FillPath(headBrush, headPath);
                    }
                    using (var headPen = new Pen(outline, 2.8f))
                    {
                        headPen.LineJoin = LineJoin.Round;
                        g.DrawPath(headPen, headPath);
                    }
                }

                // === 耳朵（向两侧耷拉）===
                DrawBetterEar(g, cx - 22, cy - 35, -8, -32, furMid, earInner, outline, false);
                DrawBetterEar(g, cx + 22, cy - 35, 8, -32, furMid, earInner, outline, true);

                // === 额头纹 ===
                DrawForeheadM(g, cx, cy - 32, furDark);

                // === 惊讶大圆眼 ===
                DrawDragKawaiiEye(g, cx - 16, cy - 18, 21, outline);
                DrawDragKawaiiEye(g, cx + 16, cy - 18, 21, outline);

                // === 鼻子 ===
                using (var noseBrush = new SolidBrush(noseC))
                using (var nosePen = new Pen(outline, 1.5f))
                {
                    g.FillEllipse(noseBrush, cx - 5, cy + 2, 10, 6);
                    g.DrawEllipse(nosePen, cx - 5, cy + 2, 10, 6);
                }

                // === 张大的嘴 ===
                using (var mouthBrush = new SolidBrush(Color.FromArgb(180, 75, 55)))
                using (var mouthPen = new Pen(outline, 1.5f))
                {
                    g.FillEllipse(mouthBrush, cx - 9, cy + 10, 18, 14);
                    g.DrawEllipse(mouthPen, cx - 9, cy + 10, 18, 14);
                }

                // === 胡须 ===
                DrawDragWhiskers(g, cx, cy + 3, outline);

                // === 下垂爪子 ===
                DrawPaw(g, cx - 28, cy + 75, 26, 24, furMid, furBelly, outline);
                DrawPaw(g, cx + 4, cy + 75, 26, 24, furMid, furBelly, outline);

                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        // ============================================================
        // 绘制辅助方法
        // ============================================================

        /// <summary>绘制蓬松尾巴</summary>
        private static void DrawFurryTail(Graphics g, float x1, float y1, float x2, float y2,
            Color main, Color tip, float width)
        {
            using (var tailPen = new Pen(main, width))
            {
                tailPen.EndCap = LineCap.Round;
                tailPen.StartCap = LineCap.Round;
                g.DrawLine(tailPen, x1, y1, x2, y2);
            }
            // 尾巴尖
            using (var tipPen = new Pen(tip, width * 0.55f))
            {
                tipPen.EndCap = LineCap.Round;
                float tx = x1 + (x2 - x1) * 0.7f;
                float ty = y1 + (y2 - y1) * 0.7f;
                g.DrawLine(tipPen, tx, ty, x2, y2);
            }
        }

        /// <summary>绘制爪子和肉垫</summary>
        private static void DrawPaw(Graphics g, float x, float y, float w, float h,
            Color fur, Color belly, Color outline)
        {
            using (var pawBrush = new SolidBrush(fur))
            using (var pawPen = new Pen(outline, 2.2f))
            {
                g.FillEllipse(pawBrush, x, y, w, h);
                g.DrawEllipse(pawPen, x, y, w, h);
            }
            // 粉色肉垫
            float cx = x + w / 2;
            float cy = y + h * 0.55f;
            float pr = w * 0.13f;
            using (var beanBrush = new SolidBrush(Color.FromArgb(255, 180, 170)))
            {
                g.FillEllipse(beanBrush, cx - pr * 2.2f, cy - pr * 0.5f, pr * 2, pr * 1.6f);
                g.FillEllipse(beanBrush, cx + pr * 0.2f, cy - pr * 0.5f, pr * 2, pr * 1.6f);
                g.FillEllipse(beanBrush, cx - pr, cy + pr * 0.3f, pr * 2, pr * 1.5f);
            }
        }

        /// <summary>绘制猫耳朵</summary>
        private static void DrawBetterEar(Graphics g, float bx, float by, float tipX, float tipY,
            Color fur, Color inner, Color outline, bool isRight)
        {
            float dx = isRight ? 1 : -1;
            PointF[] earPts = {
                new PointF(bx, by),
                new PointF(bx + tipX, by + tipY),
                new PointF(bx + dx * 22, by + 12)
            };
            using (var earBrush = new SolidBrush(fur))
            using (var earPen = new Pen(outline, 2.6f))
            {
                earPen.LineJoin = LineJoin.Round;
                g.FillPolygon(earBrush, earPts);
                g.DrawPolygon(earPen, earPts);
            }
            // 内耳
            PointF[] innerPts = {
                new PointF(bx + dx * 4, by + 5),
                new PointF(bx + tipX * 0.5f, by + tipY * 0.5f + 2),
                new PointF(bx + dx * 12, by + 11)
            };
            using (var innerBrush = new SolidBrush(inner))
            {
                g.FillPolygon(innerBrush, innerPts);
            }
        }

        /// <summary>绘制额头「M」条纹</summary>
        private static void DrawForeheadM(Graphics g, float cx, float topY, Color color)
        {
            using (var pen = new Pen(color, 2.4f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float mw = 10;
                float mh = 12;
                g.DrawLine(pen, cx - mw, topY + mh, cx - mw * 0.5f, topY);
                g.DrawLine(pen, cx - mw * 0.5f, topY, cx, topY + mh * 0.6f);
                g.DrawLine(pen, cx, topY + mh * 0.6f, cx + mw * 0.5f, topY);
                g.DrawLine(pen, cx + mw * 0.5f, topY, cx + mw, topY + mh);
            }
        }

        /// <summary>绘制可爱大眼睛（正常状态）</summary>
        private static void DrawKawaiiEye(Graphics g, float ex, float ey, float ew, float eh,
            Color iris, Color outline)
        {
            // 眼白
            using (var whiteBrush = new SolidBrush(Color.White))
            using (var eyePen = new Pen(outline, 1.6f))
            {
                g.FillEllipse(whiteBrush, ex - ew / 2, ey - eh / 2, ew, eh);
                g.DrawEllipse(eyePen, ex - ew / 2, ey - eh / 2, ew, eh);
            }
            // 虹膜渐变
            float ir = ew * 0.42f;
            float ih = eh * 0.55f;
            var irisRect = new RectangleF(ex - ir, ey - ih * 0.3f, ir * 2, ih * 2);
            using (var irisBrush = new LinearGradientBrush(irisRect,
                Color.FromArgb(80, 190, 100), Color.FromArgb(50, 140, 70), 45f))
            {
                g.FillEllipse(irisBrush, irisRect);
            }
            // 瞳孔
            using (var pupilBrush = new SolidBrush(Color.FromArgb(20, 40, 25)))
            {
                g.FillEllipse(pupilBrush, ex - ir * 0.35f, ey - ih * 0.05f, ir * 0.7f, ih * 0.8f);
            }
            // 高光
            using (var hlBrush = new SolidBrush(Color.White))
            {
                g.FillEllipse(hlBrush, ex - ir * 0.5f, ey - ih * 0.65f, ir * 0.65f, ih * 0.6f);
                g.FillEllipse(hlBrush, ex + ir * 0.15f, ey + ih * 0.15f, ir * 0.25f, ih * 0.25f);
            }
        }

        /// <summary>绘制惊讶大圆眼（拖拽状态）</summary>
        private static void DrawDragKawaiiEye(Graphics g, float ex, float ey, float size, Color outline)
        {
            float r = size / 2;
            // 眼白
            using (var whiteBrush = new SolidBrush(Color.White))
            using (var eyePen = new Pen(outline, 1.6f))
            {
                g.FillEllipse(whiteBrush, ex - r, ey - r, size, size);
                g.DrawEllipse(eyePen, ex - r, ey - r, size, size);
            }
            // 小瞳孔
            using (var pupilBrush = new SolidBrush(Color.FromArgb(20, 40, 25)))
            {
                g.FillEllipse(pupilBrush, ex - r * 0.25f, ey - r * 0.15f, r * 0.55f, r * 0.55f);
            }
            // 高光
            using (var hlBrush = new SolidBrush(Color.White))
            {
                g.FillEllipse(hlBrush, ex - r * 0.5f, ey - r * 0.6f, r * 0.5f, r * 0.45f);
            }
        }

        /// <summary>绘制胡须（正常状态）</summary>
        private static void DrawWhiskers(Graphics g, float cx, float cy, Color color)
        {
            using (var pen = new Pen(Color.FromArgb(210, 210, 210), 1.3f))
            {
                // 左侧
                g.DrawLine(pen, cx - 22, cy - 3, cx - 52, cy - 8);
                g.DrawLine(pen, cx - 22, cy + 2, cx - 52, cy + 5);
                g.DrawLine(pen, cx - 22, cy + 7, cx - 48, cy + 14);
                // 右侧
                g.DrawLine(pen, cx + 22, cy - 3, cx + 52, cy - 8);
                g.DrawLine(pen, cx + 22, cy + 2, cx + 52, cy + 5);
                g.DrawLine(pen, cx + 22, cy + 7, cx + 48, cy + 14);
            }
        }

        /// <summary>绘制胡须（拖拽状态—散乱）</summary>
        private static void DrawDragWhiskers(Graphics g, float cx, float cy, Color color)
        {
            using (var pen = new Pen(Color.FromArgb(200, 200, 200), 1.1f))
            {
                g.DrawLine(pen, cx - 18, cy + 0, cx - 46, cy - 12);
                g.DrawLine(pen, cx - 18, cy + 6, cx - 43, cy + 18);
                g.DrawLine(pen, cx + 18, cy + 0, cx + 46, cy - 12);
                g.DrawLine(pen, cx + 18, cy + 6, cx + 43, cy + 18);
            }
        }

        /// <summary>
        /// 生成优美的风景背景图
        /// </summary>
        private static void GenerateBackground(string path)
        {
            int w = 800, h = 600;
            using (var bmp = new Bitmap(w, h))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // === 天空渐变（黄昏暖色调）===
                using (var skyBrush = new LinearGradientBrush(
                    new Point(0, 0), new Point(0, h / 2 + 20),
                    Color.FromArgb(100, 160, 210), Color.FromArgb(240, 210, 160)))
                {
                    g.FillRectangle(skyBrush, 0, 0, w, h / 2 + 20);
                }

                // === 太阳光晕 ===
                using (var glowBrush = new SolidBrush(Color.FromArgb(40, 255, 230, 150)))
                {
                    g.FillEllipse(glowBrush, w - 200, 30, 180, 180);
                }
                using (var sunBrush = new SolidBrush(Color.FromArgb(255, 250, 220)))
                {
                    g.FillEllipse(sunBrush, w - 150, 55, 80, 80);
                }

                // === 云朵 ===
                using (var cloudBrush = new SolidBrush(Color.FromArgb(210, 255, 255, 255)))
                {
                    DrawCloud(g, cloudBrush, 100, 70, 1f);
                    DrawCloud(g, cloudBrush, 320, 45, 0.75f);
                    DrawCloud(g, cloudBrush, 540, 85, 0.9f);
                    DrawCloud(g, cloudBrush, 200, 115, 0.55f);
                }

                // === 远山（多层）===
                using (var mtBrush1 = new SolidBrush(Color.FromArgb(140, 180, 150)))
                {
                    PointF[] mt1 = {
                        new PointF(0, h/2+10), new PointF(80, h/3+10), new PointF(200, h/2+5),
                        new PointF(350, h/3-15), new PointF(520, h/2-5), new PointF(680, h/3+5),
                        new PointF(w, h/2-15), new PointF(w, h), new PointF(0, h)
                    };
                    g.FillPolygon(mtBrush1, mt1);
                }
                using (var mtBrush2 = new SolidBrush(Color.FromArgb(110, 155, 120)))
                {
                    PointF[] mt2 = {
                        new PointF(0, h/2+30), new PointF(150, h/3+40), new PointF(300, h/2+20),
                        new PointF(450, h/3+10), new PointF(600, h/2+15), new PointF(750, h/3+30),
                        new PointF(w, h/2+10), new PointF(w, h), new PointF(0, h)
                    };
                    g.FillPolygon(mtBrush2, mt2);
                }

                // === 草地渐变 ===
                var grassRect = new Rectangle(0, h / 2 + 35, w, h / 2 - 35);
                using (var grassBrush = new LinearGradientBrush(grassRect,
                    Color.FromArgb(130, 190, 90), Color.FromArgb(65, 135, 45), 90f))
                {
                    g.FillRectangle(grassBrush, grassRect);
                }

                // === 小路 ===
                using (var pathBrush = new SolidBrush(Color.FromArgb(200, 190, 150)))
                {
                    PointF[] pathPts = {
                        new PointF(w/2-18, h), new PointF(w/2-12, h/2+50),
                        new PointF(w/2+12, h/2+50), new PointF(w/2+18, h)
                    };
                    g.FillPolygon(pathBrush, pathPts);
                }

                // === 树木（简笔画）===
                DrawTree(g, 80, h - 60, 40);
                DrawTree(g, 620, h - 50, 50);
                DrawTree(g, 720, h - 70, 35);

                // === 花朵 ===
                DrawFlower(g, 120, h - 70);
                DrawFlower(g, 280, h - 100);
                DrawFlower(g, 480, h - 80);
                DrawFlower(g, 580, h - 110);
                DrawFlower(g, 680, h - 90);
                DrawFlower(g, 350, h - 130);

                // === 蝴蝶 ===
                DrawButterfly(g, 250, 280);
                DrawButterfly(g, 550, 250);

                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);
            }
        }

        private static void DrawCloud(Graphics g, Brush brush, int x, int y, float scale)
        {
            int r = (int)(32 * scale);
            g.FillEllipse(brush, x, y, r * 2, (int)(r * 0.7f));
            g.FillEllipse(brush, x + r / 2, y - r / 3, (int)(r * 1.6f), (int)(r * 0.8f));
            g.FillEllipse(brush, x + r, y, (int)(r * 1.3f), (int)(r * 0.65f));
        }

        private static void DrawTree(Graphics g, int x, int y, int h)
        {
            // 树干
            using (var trunkBrush = new SolidBrush(Color.FromArgb(120, 90, 60)))
            {
                g.FillRectangle(trunkBrush, x - 5, y - h, 10, h);
            }
            // 树冠
            int cr = h / 2 + 5;
            using (var crownBrush = new SolidBrush(Color.FromArgb(80, 150, 65)))
            {
                g.FillEllipse(crownBrush, x - cr, y - h - cr + 5, cr * 2, cr * 2);
            }
            using (var crownHL = new SolidBrush(Color.FromArgb(100, 175, 85)))
            {
                g.FillEllipse(crownHL, x - cr + cr / 3, y - h - cr, cr, cr);
            }
        }

        private static void DrawFlower(Graphics g, int x, int y)
        {
            int r = 7;
            var rand = new Random(x + y);
            Color[] colors = { Color.FromArgb(255, 90, 90), Color.FromArgb(255, 200, 60),
                               Color.FromArgb(255, 150, 80), Color.FromArgb(255, 120, 160) };
            Color c = colors[rand.Next(colors.Length)];

            using (var petalBrush = new SolidBrush(c))
            using (var centerBrush = new SolidBrush(Color.FromArgb(255, 240, 100)))
            {
                for (int i = 0; i < 5; i++)
                {
                    double angle = i * Math.PI * 2 / 5;
                    int px = x + (int)(r * Math.Cos(angle));
                    int py = y + (int)(r * Math.Sin(angle));
                    g.FillEllipse(petalBrush, px - r / 2, py - r / 2, r, r);
                }
                g.FillEllipse(centerBrush, x - 3, y - 3, 6, 6);
            }
        }

        private static void DrawButterfly(Graphics g, int x, int y)
        {
            Color wing = Color.FromArgb(200, 160, 100);
            using (var wingBrush = new SolidBrush(wing))
            {
                g.FillEllipse(wingBrush, x - 6, y - 5, 8, 12);
                g.FillEllipse(wingBrush, x + 2, y - 5, 8, 12);
            }
            using (var bodyPen = new Pen(Color.FromArgb(80, 60, 40), 1.2f))
            {
                g.DrawLine(bodyPen, x, y - 4, x, y + 4);
            }
        }

        /// <summary>
        /// 生成托盘图标（绿色心形图标）
        /// </summary>
        private static void GenerateTrayIcon(string path)
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // 绿色心形
                using (var heartBrush = new SolidBrush(Color.FromArgb(100, 200, 80)))
                using (var heartPen = new Pen(Color.FromArgb(60, 150, 40), 1))
                {
                    // 用两个圆和一个三角形拼心形
                    g.FillEllipse(heartBrush, 2, 2, 14, 14);
                    g.FillEllipse(heartBrush, 16, 2, 14, 14);
                    PointF[] triangle = {
                        new PointF(3, 10), new PointF(29, 10), new PointF(16, 28)
                    };
                    g.FillPolygon(heartBrush, triangle);
                }

                // 白色十字（医疗标志）
                using (var crossPen = new Pen(Color.White, 2.5f))
                {
                    g.DrawLine(crossPen, 16, 7, 16, 20);
                    g.DrawLine(crossPen, 10, 13, 22, 13);
                }

                // 转为 Icon 保存
                IntPtr hIcon = bmp.GetHicon();
                using (var icon = Icon.FromHandle(hIcon))
                {
                    using (var fs = new FileStream(path, FileMode.Create))
                    {
                        icon.Save(fs);
                    }
                }
            }
        }

        /// <summary>
        /// 生成名言库。默认名言 = 随附的《剑来》名言（名言名句.md） + 内置精选名言。
        /// 两者都会写入，旧的内置名言始终保留。若《剑来》文件不存在，则只用内置。
        /// </summary>
        private static void GenerateQuotes(string path)
        {
            var all = new List<string>();

            // 1) 随附的《剑来》名言（若有）
            string source = JianLaiQuotesFile;
            if (File.Exists(source))
            {
                try
                {
                    foreach (string line in File.ReadAllLines(source, System.Text.Encoding.UTF8))
                    {
                        string t = line.Trim();
                        if (!string.IsNullOrEmpty(t) && !t.StartsWith("//"))
                            all.Add(t);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("读取名言文件失败: " + ex.Message);
                }
            }

            // 2) 内置精选名言（始终保留）
            string[] builtin = {
                // === 停下来，想一想 ===
                "你那么着急，是要赶去哪呢？终点只有一个，慢点走。",
                "忙碌是一种懒惰——懒得思考自己为什么而忙。——蒂姆·费里斯",
                "我们不是活了几十年，而是把同一天重复了几万遍。",
                "当你忙着低头赶路时，别忘了抬头看星空。",
                "人这一生，最不该辜负的是自己的身体和心情。",
                "所有你熬夜透支的，身体都会一笔笔记下来。",
                "休息不是偷懒，是在给灵魂充电。",
                "真正的效率，是知道什么时候该停下来。",
                "你以为在努力，其实可能只是在原地打转。",
                "一张一弛，文武之道也。——《礼记》",

                // === 斯多葛智慧 ===
                "我们无法控制风向，但可以调整船帆。",
                "困扰你的不是事情本身，而是你对它的看法。——爱比克泰德",
                "你所焦虑的事情中，90% 根本不会发生。",
                "接受无法改变的，改变可以改变的，并拥有分辨二者的智慧。",
                "一个人真正的财富，是他不需要的东西的多少。",
                "生气是拿别人的错误惩罚自己。",
                "如果你的内心足够平静，外界的噪音就只是风声。",
                "不要为明天忧虑，今天的难处今天当就够了。",
                "人生就像骑自行车，想保持平衡就得往前走。——爱因斯坦",
                "所谓成熟，就是习惯了任何人的忽冷忽热，看淡了任何人的渐行渐远。",

                // === 东方智慧 ===
                "上善若水，水善利万物而不争。——《道德经》",
                "大音希声，大象无形。——老子",
                "菩提本无树，明镜亦非台。本来无一物，何处惹尘埃。——惠能",
                "不以物喜，不以己悲。——范仲淹",
                "采菊东篱下，悠然见南山。——陶渊明",
                "此心安处是吾乡。——苏轼",
                "宠辱不惊，看庭前花开花落；去留无意，望天上云卷云舒。",
                "行到水穷处，坐看云起时。——王维",
                "人生如逆旅，我亦是行人。——苏轼",
                "世间所有的相遇，都是久别重逢。——《一代宗师》",

                // === 猫的哲学 ===
                "学学猫吧：该晒太阳晒太阳，该伸懒腰伸懒腰。",
                "猫咪从不焦虑明天，因为它们活在当下——你也该试试。",
                "一只猫教会我的事：被爱不需要理由，拒绝也不需要解释。",
                "不管今天多糟糕，找个舒服的地方蜷一会，世界就会变好。",
                "猫咪从不在意别人怎么评价它，你也应该这样。",
                "你看那只猫——它什么也不做，却让整个房间都有了温度。",
                "一喵一世界，一睡一整天。这何尝不是一种智慧？",
                "猫不会为打翻的牛奶哭泣，你也不必为过去懊悔。",
                "养猫的人都知道：最柔软的生物，有着最锋利的边界感。",
                "摸鱼是一门艺术，猫是这门艺术的大师。",

                // === 西哲金句 ===
                "未经审视的人生不值得过。——苏格拉底",
                "我思故我在。——笛卡尔",
                "凡是不能杀死我的，必将使我更强大。——尼采",
                "人是生而自由的，却无往不在枷锁之中。——卢梭",
                "存在先于本质。——萨特",
                "我们最深的恐惧不是自己不够好，而是自己拥有无穷的力量。",
                "一个人知道自己为什么而活，就可以忍受任何一种生活。——尼采",
                "真正的发现之旅不在于寻找新风景，而在于拥有新的眼睛。——普鲁斯特",
                "勇气不是没有恐惧，而是判断有些事比恐惧更重要。",
                "把每一天当作生命中的最后一天来过。——乔布斯",

                // === 诗与远方 ===
                "生活不止眼前的苟且，还有诗和远方。",
                "满地都是六便士，他却抬头看见了月亮。——毛姆《月亮与六便士》",
                "世界那么大，我想去看看。",
                "身体和灵魂，总有一个要在路上。",
                "愿你出走半生，归来仍是少年。",
                "每个人心中都有一团火，路过的人只看到了烟。——梵高",
                "要么读书，要么旅行，身体和灵魂总有一个在路上。",
                "一生温暖纯良，不舍爱与自由。",
                "万物皆有裂痕，那是光照进来的地方。——莱昂纳德·科恩",
                "愿你被这个世界温柔以待，也愿你对这个世界报以温柔。",

                // === 有趣的人生观察 ===
                "如果你觉得世界欠你什么，想想你出生时什么都没有——连衣服都没有。",
                "人类发明了闹钟，然后又发明了贪睡按钮。这完美总结了人性。",
                "最贵的东西：免费的东西、别人的时间、和你的注意力。",
                "当你说'最后一次刷手机'的时候，你的大脑已经笑了。",
                "拖延症：花三个小时整理桌面，就为了逃避十分钟的工作。",
                "成年人的标志：在超市看到喜欢的零食，想想还是算了。",
                "睡眠是最好的冥想。——达赖喇嘛",
                "你有多久没有什么都不做，只是发呆了？",
                "每天最幸福的时刻：关掉电脑的那一刻。",
                "如果你从错误中学习，那错误就是最便宜的学费。",

                // === 专注与深度 ===
                "深度工作是一种稀缺能力，也是你对抗平庸的武器。",
                "你在手机上划过的每一分钟，都有人在用那一分钟变强。",
                "成功人士和普通人的差别，在于他们保护自己注意力的能力。",
                "专注不是说你做了什么，而是你选择不做什么。",
                "信息时代最大的谎言：多任务处理让你更高效。",
                "别让算法决定你该看什么，你决定算法该给你什么。",
                "高质量的一小时，胜过心不在焉的一整天。",
                "你关注什么，就成为什么。",
                "在这个分心的时代，安静是一种超能力。",
                "每当你想打开手机时，深呼吸三次——你可能就不想打开了。",

                // === 人际关系 ===
                "和什么样的人在一起，你就会成为什么样的人。",
                "朋友不需要多，真心就够。圈子不需要大，舒服就好。",
                "一段好的关系，是两个人待在一起不说话也不尴尬。",
                "不要试图改变别人，除非他们自己愿意改变。",
                "学会说'不'，你的'好'才有价值。",
                "你不可能让所有人都喜欢你，所以做自己就好。",
                "最好的关系，是彼此成就，而不是彼此消耗。",
                "听比说重要，问比答难得。",
                "有同理心的人，走到哪里都不会孤单。",
                "善良不是软弱，边界感不是冷漠。",

                // === 成长与改变 ===
                "种一棵树最好的时间是十年前，其次是现在。",
                "你不需要很厉害才能开始，但你需要开始才会很厉害。",
                "失败不是成功之母，反思才是。",
                "一个人最大的敌人，是昨天的自己。",
                "舒适区很舒服，但那里什么都不会生长。",
                "不要拿你的第一年，去比别人的第十年。",
                "每一个大神都曾经是菜鸟，区别在于他们没有停下。",
                "成长就是不断地发现从前的自己是个傻瓜。——罗素（戏仿）",
                "你学到的每一样东西，都会在未来的某个时刻派上用场。",
                "每天进步 1%，一年后你会是现在的 37 倍。",

                // === 身体与健康 ===
                "身体是灵魂的圣殿，不是临时的帐篷。",
                "每一口食物都是一次选择——让身体感激，还是让身体忍耐？",
                "运动产生的多巴胺，是身体送你的免费礼物。",
                "当你觉得走不动的时候，其实你才走了 40%。——跑步者的秘密",
                "睡眠不是浪费时间，是投资明天。",
                "你的脊椎支撑了你一整天，现在该站起来活动了。",
                "世界上最便宜的长寿药：多喝水、多走路、少生气。",
                "眼睛酸了吗？看看窗外吧，绿色是眼睛最爱的颜色。",
                "你值多少钱都不如你值多少健康。",
                "肩颈酸痛不是勋章，是身体在求救。",

                // === 快乐与满足 ===
                "幸福不是拥有你想要的，而是想要你已经拥有的。",
                "快乐不在于事情本身，而在于你看待它的方式。",
                "人生不如意十之八九，常想一二。",
                "做一个简单的人，享受简单的快乐。",
                "感恩是最快的快乐通道——现在就想想今天发生的三件好事。",
                "快乐是一种选择，和境遇无关。",
                "小孩子为什么快乐？因为他们不记仇、不纠结、不比较。",
                "幸福的三要素：有事做、有人爱、有所期待。",
                "你今天笑了吗？没笑的话，深吸一口气，再笑一次。",
                "不要等到拥有了一切才开始享受，你现在就可以。",

                // === 短而有力 ===
                "Less is more. ——密斯·凡·德罗",
                "Live in the moment.",
                "Do what you can, with what you have, where you are.",
                "一切都会过去。——所罗门王的戒指铭文",
                "人生苦短，及时行乐。",
                "但行好事，莫问前程。",
                "念念不忘，必有回响。",
                "万物之中，希望至美。——《肖申克的救赎》",
                "Carpe Diem ——活在当下，抓住今天。",
                "慢慢来，比较快。",

                // === 科技时代特供 ===
                "点赞不会让你更有价值，你自己才是。",
                "在信息爆炸的时代，屏蔽力比记忆力更重要。",
                "手机没电了可以充，人没电了只能休息。",
                "你的朋友圈不是你的生活——别忘了抬头看看真实的世界。",
                "算法不会告诉你什么时候该关掉屏幕，只能你自己决定。",
                "真正的高科技，是能让自己定时离线。",
                "你看到的每一条推送，都有人在背后精心设计让你上瘾。",
                "人类最伟大的发明不是电脑，是关机的勇气。",
                "别让你的注意力成为别人商业模式里的燃料。",
                "最好的 APP 是窗外的那片天空，免费、无限更新、永不卡顿。",

                // === 最后几句温柔的话 ===
                "你已经做得很好了。真的。",
                "不需要一直坚强，累的时候可以停下来。",
                "你的存在本身，就是给这个世界最好的礼物。",
                "今天可能不太顺，但明天太阳照常升起。",
                "你值得被好好对待——第一个应该这样对你的人，是你自己。",
                "如果你正在经历困难时期，记住：这也将过去。",
                "这世界变得更好了，因为你在其中。",
            };

            // 合并：随附《剑来》名言 + 内置精选名言
            all.AddRange(builtin);

            File.WriteAllText(path, string.Join(Environment.NewLine, all), System.Text.Encoding.UTF8);
        }
    }
}
