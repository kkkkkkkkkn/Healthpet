using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace HealthyPet
{
    /// <summary>
    /// 半透明提醒弹窗——环形倒计时 + 跳过按钮
    /// 不点跳过就只能等倒计时结束自动关闭
    /// 全部布局按遮罩面板尺寸百分比自适应
    /// </summary>
    public class AlertForm : Form
    {
        private Timer _countdownTimer;
        private Label _messageLabel;
        private Label _countdownLabel;
        private Panel _overlayPanel;
        private int _totalRestSeconds;

        // "跳过休息"按钮区域（直接在遮罩面板上自绘，避免子控件透明/绘制问题）
        private Rectangle _btnSkipBounds;
        private bool _btnSkipHovered;
        private Font _btnSkipFont;
        private int _remainingSeconds;
        private float _ringProgress = 1f;

        // 缓存的最新尺寸（供 Paint 用）
        private int _ringCenterX;
        private int _ringCenterY;
        private int _ringOuterR;
        private int _ringThickness;
        private int _barY;
        private int _barW;
        private int _barH;

        public AlertForm(AppConfig config, string message, int restSeconds)
        {
            _totalRestSeconds = Math.Max(1, restSeconds);
            _remainingSeconds = _totalRestSeconds;
            _ringProgress = 1f;

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.DoubleBuffered = true;

            // 注意：不要设置 Opacity，否则会触发 WS_EX_LAYERED 分层窗口，
            // 与子控件（Panel/Label/Button）双缓冲不同步，导致弹窗持续闪烁。
            // 半透明效果改由遮罩面板自身的 Alpha 背景绘制实现（见 OverlayPanel_Paint）。
            // this.Opacity = 0.93; // 已移除，避免闪烁

            // 使用主屏幕的工作区进行居中计算，避免多屏/构造期窗体未绑定时定位错乱
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;

            // 背景图片（保持原始比例，居中显示）
            string bgPath = config.GetFullPath(config.AlertBackgroundImage);
            if (!File.Exists(bgPath))
                bgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Pop-up image.png");

            Image bgImage = null;
            if (File.Exists(bgPath))
            {
                try
                {
                    // 使用克隆方式加载，避免 Image.FromFile 长期占用文件句柄
                    using (var fs = new FileStream(bgPath, FileMode.Open, FileAccess.Read))
                    {
                        var tmp = Image.FromStream(fs);
                        bgImage = new Bitmap(tmp);
                        tmp.Dispose();
                    }
                }
                catch { bgImage = null; }
            }

            if (bgImage != null)
            {
                this.BackgroundImage = bgImage;
                this.BackgroundImageLayout = ImageLayout.Zoom;

                // 窗口大小：全屏 or 按图片比例自适应（无白边）
                if (config.AlertSizeMode == 0)
                {
                    this.WindowState = FormWindowState.Maximized;
                }
                else
                {
                    // 弹框等比例缩放，面积约为屏幕工作区的 1/6，宽高比与图片一致
                    double targetArea = wa.Width * wa.Height / 6.0;
                    double imgRatio = (double)bgImage.Width / bgImage.Height;

                    int h = (int)Math.Sqrt(targetArea / imgRatio);
                    int w = (int)(h * imgRatio);

                    // 限制最小/最大尺寸（最小 240×160，最大 工作区短边的 80%）
                    int maxSide = (int)(Math.Min(wa.Width, wa.Height) * 0.8);
                    if (w > maxSide) { w = maxSide; h = (int)(w / imgRatio); }
                    if (h > maxSide) { h = maxSide; w = (int)(h * imgRatio); }
                    if (w < 240) { w = 240; h = (int)(w / imgRatio); }
                    if (h < 160) { h = 160; w = (int)(h * imgRatio); }

                    this.Size = new Size(w, h);
                    // 显式居中（不依赖 StartPosition，更可靠）
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = new Point(
                        wa.X + (wa.Width - w) / 2,
                        wa.Y + (wa.Height - h) / 2);
                }
            }
            else
            {
                // 无背景图：用深色调，避免白色边框
                this.BackColor = Color.FromArgb(25, 32, 40);
                if (config.AlertSizeMode == 0)
                {
                    this.WindowState = FormWindowState.Maximized;
                }
                else
                {
                    this.Size = new Size(720, 460);
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = new Point(
                        wa.X + (wa.Width - this.Width) / 2,
                        wa.Y + (wa.Height - this.Height) / 2);
                }
            }

            // === 中央遮罩面板 ===
            _overlayPanel = new Panel();
            _overlayPanel.BackColor = Color.Transparent;
            _overlayPanel.Paint += OverlayPanel_Paint;
            _overlayPanel.MouseClick += OverlayPanel_MouseClick;
            _overlayPanel.MouseMove += OverlayPanel_MouseMove;
            _overlayPanel.MouseLeave += (s, e) =>
            {
                if (_btnSkipHovered)
                {
                    _btnSkipHovered = false;
                    _overlayPanel.Cursor = Cursors.Default;
                    _overlayPanel.Invalidate(_btnSkipBounds);
                }
            };
            // Panel 默认不公开 DoubleBuffered，通过反射开启，避免自定义绘制闪烁
            var dbProp = typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (dbProp != null)
                dbProp.SetValue(_overlayPanel, true, null);
            this.Controls.Add(_overlayPanel);

            // === 标题文字 ===
            _messageLabel = new Label();
            _messageLabel.Text = message;
            _messageLabel.Font = new Font("微软雅黑", 14, FontStyle.Regular);
            _messageLabel.ForeColor = Color.FromArgb(220, 210, 180);
            _messageLabel.BackColor = Color.Transparent;
            _messageLabel.TextAlign = ContentAlignment.MiddleCenter;
            _overlayPanel.Controls.Add(_messageLabel);

            // === 倒计时数字 ===
            _countdownLabel = new Label();
            _countdownLabel.Font = new Font("Consolas", 38, FontStyle.Bold);
            _countdownLabel.ForeColor = Color.White;
            _countdownLabel.BackColor = Color.Transparent;
            _countdownLabel.TextAlign = ContentAlignment.MiddleCenter;
            UpdateCountdownDisplay();
            _overlayPanel.Controls.Add(_countdownLabel);

            // "跳过休息"按钮区域直接在 _overlayPanel 上绘制（见 OverlayPanel_Paint / MouseClick），
            // 不再使用子控件，彻底避免 Button/Label/Panels 在透明分层窗体下的各种绘制异常。

            // === 倒计时定时器 ===
            _countdownTimer = new Timer();
            _countdownTimer.Interval = 1000;
            _countdownTimer.Tick += CountdownTimer_Tick;
            _countdownTimer.Start();

            this.Resize += (s, e) => LayoutControls();
            this.Load += (s, e) => LayoutControls();
        }

        // 已移除 WS_EX_COMPOSITED：
        // 1. 当前弹窗未设置 Opacity，不是分层窗口，不需要该扩展样式来合成子控件；
        // 2. WS_EX_COMPOSITED 与透明背景子控件（Label/Panel）组合时，在某些 DPI/主题下
        //    会导致子控件绘制异常，表现为红色边框错误占位符。
        // 防闪烁改用 Form 自身 DoubleBuffered + Panel 反射开启 DoubleBuffered 即可。

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            _remainingSeconds--;
            if (_remainingSeconds <= 0)
            {
                _countdownTimer.Stop();
                this.Close();
                return;
            }
            _ringProgress = (float)_remainingSeconds / _totalRestSeconds;
            UpdateCountdownDisplay();
            _overlayPanel.Invalidate();
        }

        private void UpdateCountdownDisplay()
        {
            int mins = _remainingSeconds / 60;
            int secs = _remainingSeconds % 60;
            _countdownLabel.Text = string.Format("{0:D2}:{1:D2}", mins, secs);
        }

        private void UpdateHintLabel()
        {
            // 提示文字（剩余 %）已移除，保留空方法避免引用报错
        }

        // ============================================================
        // 布局——所有尺寸按遮罩面板宽度的百分比计算
        // ============================================================

        private void LayoutControls()
        {
            int cw = this.ClientSize.Width;
            int ch = this.ClientSize.Height;

            // 遮罩面板大小：跟随窗口，最小 340x300，最大有限制
            int panelW = Math.Min(380, Math.Max(340, cw - 60));
            int panelH = Math.Min(360, Math.Max(300, ch - 80));
            _overlayPanel.Size = new Size(panelW, panelH);
            _overlayPanel.Location = new Point((cw - panelW) / 2, (ch - panelH) / 2);

            int pw = _overlayPanel.ClientSize.Width;
            int ph = _overlayPanel.ClientSize.Height;

            // === 比例定义（基于实际面板尺寸）===
            int sidePad = pw / 12;                // 左右内边距
            int hPad    = ph / 16;                // 上下内边距

            // 标题区：高 = 顶部 13%
            int titleH = Math.Max(28, ph * 13 / 100);
            _messageLabel.Location = new Point(sidePad, hPad);
            _messageLabel.Size = new Size(pw - sidePad * 2, titleH);
            // 标题字号：14 起步，根据面板宽度调整
            _messageLabel.Font = ClampFont(pw, 12f, 14f, 18f);

            // 环区：高 = 中段 55%，位于标题下方
            int ringAreaH = ph * 55 / 100;
            int ringAreaTop = hPad + titleH + ph / 32;

            // 环圆心
            _ringCenterX = pw / 2;
            _ringCenterY = ringAreaTop + ringAreaH / 2;

            // 环半径 = 较小一边的 38%
            _ringOuterR = Math.Min(pw, ringAreaH) * 38 / 100;
            _ringOuterR = Math.Max(40, _ringOuterR);   // 下限保护
            _ringThickness = Math.Max(6, _ringOuterR / 10);  // 环粗 = 半径/10

            // 倒计时数字：居中叠在环上
            int cdH = _ringOuterR * 12 / 10; // 高度的 120%
            _countdownLabel.Location = new Point(sidePad, _ringCenterY - cdH / 2);
            _countdownLabel.Size = new Size(pw - sidePad * 2, cdH);
            // 倒计时字号按环直径缩放：18~56pt
            float cdFontSize = Math.Max(18f, Math.Min(56f, _ringOuterR * 0.55f));
            _countdownLabel.Font = new Font("Consolas", cdFontSize, FontStyle.Bold);

            // 进度条区
            _barY = ringAreaTop + ringAreaH + ph / 40;
            _barW = pw - sidePad * 2;
            _barH = Math.Max(3, ph / 80);

            // 提示文字已移除，跳过按钮区域紧跟进度条下方

            // 跳过按钮区域——固定大小即可
            int btnW = Math.Min(140, Math.Max(80, pw - sidePad * 2));
            int btnH = Math.Max(32, Math.Min(60, ph * 9 / 100));
            int btnY = ph - btnH - hPad;
            _btnSkipBounds = new Rectangle((pw - btnW) / 2, btnY, btnW, btnH);

            // 字号按按钮宽度缩放
            float btnFontSize = Math.Max(9f, Math.Min(13f, btnW / 11f));
            _btnSkipFont = new Font("微软雅黑", btnFontSize, FontStyle.Regular);
        }

        /// <summary>根据宽度返回字号（在 [min, max] 之间线性插值，宽 280→min, 480→max）</summary>
        private static Font ClampFont(int width, float minSize, float midSize, float maxSize)
        {
            float size;
            if (width < 280)
                size = minSize;
            else if (width > 480)
                size = maxSize;
            else
                size = midSize;
            return new Font("微软雅黑", size, FontStyle.Regular);
        }

        // ============================================================
        // 绘制
        // ============================================================

        private void OverlayPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int w = _overlayPanel.ClientSize.Width;
            int h = _overlayPanel.ClientSize.Height;
            int r = Math.Min(20, h / 18);   // 圆角半径自适应

            // === 面板深色填充 ===
            using (var path = new GraphicsPath())
            {
                AddRoundedRect(path, new Rectangle(0, 0, w - 1, h - 1), r);

                using (var bgBrush = new SolidBrush(Color.FromArgb(170, 15, 18, 25)))
                {
                    g.FillPath(bgBrush, path);
                }
                using (var borderPen = new Pen(Color.FromArgb(45, 255, 255, 255), 1f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // === 环形进度 ===
            Rectangle ringRect = new Rectangle(
                _ringCenterX - _ringOuterR, _ringCenterY - _ringOuterR,
                _ringOuterR * 2, _ringOuterR * 2);

            // 底轨
            using (var trackPen = new Pen(Color.FromArgb(50, 255, 255, 255), _ringThickness))
            {
                g.DrawArc(trackPen, ringRect, 0, 360);
            }

            // 进度弧
            float sweepAngle = 360f * _ringProgress;
            if (sweepAngle > 0.5f)
            {
                Color progColor = _ringProgress > 0.5f
                    ? Color.FromArgb(255, 80, 210, 180)
                    : Color.FromArgb(255, 240, 190, 80);
                using (var progressPen = new Pen(progColor, _ringThickness))
                {
                    progressPen.StartCap = LineCap.Round;
                    progressPen.EndCap = LineCap.Round;
                    g.DrawArc(progressPen, ringRect, -90, sweepAngle);
                }

                // 头端光点
                if (_ringProgress > 0.02f)
                {
                    double endAngle = (-90 + sweepAngle) * Math.PI / 180.0;
                    float dotX = _ringCenterX + (float)(_ringOuterR * Math.Cos(endAngle));
                    float dotY = _ringCenterY + (float)(_ringOuterR * Math.Sin(endAngle));
                    using (var dotBrush = new SolidBrush(progColor))
                    {
                        float dSize = _ringThickness + 2;
                        g.FillEllipse(dotBrush, dotX - dSize / 2f, dotY - dSize / 2f, dSize, dSize);
                    }
                }
            }

            // === 线性进度条 ===
            if (_barW > 0 && _barH > 0)
            {
                Rectangle barTrack = new Rectangle(_ringCenterX - _barW / 2, _barY, _barW, _barH);
                using (var barTrackBrush = new SolidBrush(Color.FromArgb(50, 255, 255, 255)))
                {
                    FillRoundedRect(g, barTrack, _barH / 2, barTrackBrush);
                }

                int fillW = (int)(_barW * _ringProgress);
                if (fillW > 0)
                {
                    Rectangle barFill = new Rectangle(barTrack.X, _barY, fillW, _barH);
                    Color barColor = _ringProgress > 0.5f
                        ? Color.FromArgb(200, 80, 210, 180)
                        : Color.FromArgb(200, 240, 190, 80);
                    using (var barFillBrush = new SolidBrush(barColor))
                    {
                        FillRoundedRect(g, barFill, _barH / 2, barFillBrush);
                    }
                }
            }

            // === 跳过休息按钮 ===
            DrawSkipButton(g);
        }

        /// <summary>在 OverlayPanel 上绘制"跳过休息"按钮</summary>
        private void DrawSkipButton(Graphics g)
        {
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int bw = _btnSkipBounds.Width;
                int bh = _btnSkipBounds.Height;

                // 防御：尺寸不合法时跳过绘制，避免 GraphicsPath 因负/零尺寸抛出 GDI+ 异常。
                if (bw <= 4 || bh <= 4) return;

                int radius = Math.Min(bh / 3, bw / 3);   // 圆角随按钮大小，并限制不超过半边

                Color bg = _btnSkipHovered
                    ? Color.FromArgb(100, 90, 90, 95)
                    : Color.FromArgb(70, 55, 55, 60);
                using (var bgBrush = new SolidBrush(bg))
                {
                    FillRoundedRect(g, _btnSkipBounds, radius, bgBrush);
                }

                // 文字
                const string btnText = "\u23ED  跳过休息";
                Color textColor = _btnSkipHovered ? Color.White : Color.FromArgb(180, 180, 185);
                using (var textBrush = new SolidBrush(textColor))
                using (var sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(btnText, _btnSkipFont ?? _messageLabel.Font, textBrush,
                        new RectangleF(_btnSkipBounds.X, _btnSkipBounds.Y, bw, bh), sf);
                }
            }
            catch (Exception ex)
            {
                // Paint 事件绝不能抛出异常，否则 WinForms 会画红色边框错误占位符。
                try
                {
                    string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                    File.AppendAllText(logPath, string.Format("[{0}] DrawSkipButton failed: {1}{2}", DateTime.Now, ex, Environment.NewLine));
                }
                catch { }
            }
        }

        private void OverlayPanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (_btnSkipBounds.Contains(e.Location))
            {
                if (_countdownTimer != null) _countdownTimer.Stop();
                this.Close();
            }
        }

        private void OverlayPanel_MouseMove(object sender, MouseEventArgs e)
        {
            bool hovered = _btnSkipBounds.Contains(e.Location);
            if (hovered != _btnSkipHovered)
            {
                _btnSkipHovered = hovered;
                _overlayPanel.Cursor = hovered ? Cursors.Hand : Cursors.Default;
                _overlayPanel.Invalidate(_btnSkipBounds);
            }
        }

        // ============================================================
        // 辅助方法
        // ============================================================

        private static void AddRoundedRect(GraphicsPath path, Rectangle rect, int radius)
        {
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
        }

        private static void FillRoundedRect(Graphics g, Rectangle rect, int radius, Brush brush)
        {
            using (var path = new GraphicsPath())
            {
                AddRoundedRect(path, rect, radius);
                g.FillPath(brush, path);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_countdownTimer != null) { _countdownTimer.Stop(); _countdownTimer.Dispose(); }
                // 释放背景图，避免频繁弹窗累积 GDI 对象泄漏
                if (this.BackgroundImage != null) { this.BackgroundImage.Dispose(); this.BackgroundImage = null; }
                // 注意：不再手动 Dispose 字体（_btnSkipFont 等），
                // 改由 WinForms 标准属性设置器管理生命周期，通过 GC finalizer 回收。
                // 此前手动 Dispose 会导致父控件 Font 被错误释放，进而污染
                // 同一 GDI+ 字体族的其他对象，触发后续窗体 Font.GetHeight() 抛"参数无效"。
            }
            base.Dispose(disposing);
        }
    }
}
