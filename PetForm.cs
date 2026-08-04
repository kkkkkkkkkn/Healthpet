using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HealthyPet
{
    /// <summary>
    /// 单个桌面宠物窗口——透明背景、可拖拽、随机移动、显示名言、做提醒动作
    /// </summary>
    public class PetForm : Form
    {
        // 图片资源
        private Image _normalImage;
        private Image _dragImage;
        private Image _currentImage;

        // 宠物尺寸
        private Size _petSize;
        private int _displayWidth;
        private int _displayHeight;

        // 拖拽相关
        private bool _isDragging = false;
        private Point _dragOffset;

        // 随机移动相关
        private Timer _moveStepTimer;    // 平滑移动步进定时器（50ms）
        private Timer _moveTargetTimer;  // 换目标定时器（3~8秒）
        private PointF _currentPos;      // 当前位置（浮点数精度）
        private PointF _targetPos;       // 目标位置

        // 离屏位图缓存：仅在图片或尺寸变化时重建，移动时直接复用，降低 50ms 高频 GDI 开销
        private IntPtr _cachedHBitmap = IntPtr.Zero;
        private Image _cachedImageRef = null;
        private int _cachedW, _cachedH;
        private bool _isMoving = false;

        // 名言展示
        private QuoteBubbleForm _quoteBubble;
        private Timer _quoteCloseTimer; // 名言气泡自动关闭计时器（字段化以便正确释放，避免局部变量被 GC 或重复触发）

        // 提醒动作（跳跃动画）
        private Timer _reminderActionTimer;
        private int _reminderActionStep = 0;
        private const int REMINDER_ACTION_STEPS = 20; // 20步 × 50ms = 1秒（上升+下降=2秒）
        private const int MoveIntervalMinMs = 3000;   // 随机移动最小间隔
        private const int MoveIntervalMaxMs = 8001;   // 随机移动最大间隔（Next 上界取不到，故 +1）
        private const int MoveStepIntervalMs = 50;    // 平滑移动/动画每步间隔
        private const int QuoteBubbleDisplayMs = 7000;// 名言气泡自动关闭时长
        private const int PetEdgeMargin = 50;          // 宠物距屏幕边缘的安全边距
        private int _originalTop;

        // 随机数生成器
        private Random _random;

        // 显示模式
        private bool _visible = true;

        /// <summary>双击宠物事件（用于打开设置）</summary>
        public event Action OnDoubleClickPet;

        /// <summary>显示时不激活窗口（不抢焦点）</summary>
        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        /// <summary>窗口样式——不在任务栏显示，不抢焦点，per-pixel alpha 分层窗口</summary>
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED：分层窗口
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE：不激活窗口
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW：工具窗口（不在 Alt+Tab 中）
                return cp;
            }
        }

        // Per-pixel alpha P/Invoke
        [DllImport("user32.dll")]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pptSrc,
            int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("user32.dll")]
        private static extern bool ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [StructLayout(LayoutKind.Sequential)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        private const int AC_SRC_OVER = 0;
        private const int AC_SRC_ALPHA = 1;
        private const int ULW_ALPHA = 2;

        public PetForm(Image normalImage, Image dragImage, Size petSize, Random random)
        {
            _normalImage = normalImage;
            _dragImage = dragImage;
            _currentImage = normalImage;
            _petSize = petSize;
            _random = random;

            // 计算显示尺寸（保持宽高比）
            CalculateDisplaySize();

            // 设置窗口属性——per-pixel alpha 分层窗口，无需 TransparencyKey
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.Black;        // 分层窗口下不显示 BackColor
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(_displayWidth, _displayHeight);

            // 随机初始位置（避开任务栏）
            Screen screen = Screen.PrimaryScreen;
            int x = _random.Next(0, Math.Max(1, screen.WorkingArea.Width - _displayWidth));
            int y = _random.Next(0, Math.Max(1, screen.WorkingArea.Height - _displayHeight));
            this.Location = new Point(x, y);
            _currentPos = new PointF(x, y);

            // 注册鼠标事件（拖拽）
            this.MouseDown += PetForm_MouseDown;
            this.MouseMove += PetForm_MouseMove;
            this.MouseUp += PetForm_MouseUp;
            this.DoubleClick += PetForm_DoubleClick;

            // 随机移动定时器——步进（50ms 平滑移动）
            _moveStepTimer = new Timer();
            _moveStepTimer.Interval = MoveStepIntervalMs;
            _moveStepTimer.Tick += MoveStepTimer_Tick;

            // 随机移动定时器——换目标（3~8秒）
            _moveTargetTimer = new Timer();
            ScheduleNextMove();
            _moveTargetTimer.Tick += MoveTargetTimer_Tick;
            _moveTargetTimer.Start();

            // 位置变化后刷新分层窗口
            this.LocationChanged += (s, e) =>
            {
                _currentPos = new PointF(this.Left, this.Top);
                RefreshVisual();
            };
        }

        /// <summary>
        /// 计算显示尺寸，保持宽高比
        /// </summary>
        private void CalculateDisplaySize()
        {
            if (_normalImage == null)
            {
                _displayWidth = _petSize.Width;
                _displayHeight = _petSize.Height;
                return;
            }

            float ratio = (float)_normalImage.Width / _normalImage.Height;
            int maxDim = Math.Max(_petSize.Width, _petSize.Height);

            if (_normalImage.Width >= _normalImage.Height)
            {
                _displayWidth = maxDim;
                _displayHeight = (int)(maxDim / ratio);
            }
            else
            {
                _displayHeight = maxDim;
                _displayWidth = (int)(maxDim * ratio);
            }

            // 确保最小尺寸
            _displayWidth = Math.Max(_displayWidth, 20);
            _displayHeight = Math.Max(_displayHeight, 20);
        }

        // ============================================================
        // 绘制（分层窗口 per-pixel alpha，彻底消除绿边）
        // ============================================================

        /// <summary>刷新分层窗口——每次位置/图片/尺寸变化后调用</summary>
        private void RefreshVisual()
        {
            if (_currentImage == null) return;
            if (_displayWidth <= 0 || _displayHeight <= 0) return;
            if (!this.IsHandleCreated) return;

            // 仅当图片或显示尺寸变化时才重绘离屏位图、重建缓存 HBITMAP，
            // 平滑移动（每 50ms）时直接复用缓存，避免反复 CreateCompatibleDC/GetHbitmap 的开销。
            bool needRebuild = (_cachedHBitmap == IntPtr.Zero)
                               || (_cachedImageRef != _currentImage)
                               || (_cachedW != _displayWidth)
                               || (_cachedH != _displayHeight);
            if (needRebuild)
            {
                if (_cachedHBitmap != IntPtr.Zero) DeleteObject(_cachedHBitmap);
                using (var bmp = new Bitmap(_displayWidth, _displayHeight))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.Clear(Color.Transparent);
                        g.DrawImage(_currentImage, 0, 0, _displayWidth, _displayHeight);
                    }
                    // 注意：HBITMAP 是从 bmp 拷贝出的独立对象，bmp Dispose 后依然有效，可安全缓存
                    _cachedHBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                }
                _cachedImageRef = _currentImage;
                _cachedW = _displayWidth;
                _cachedH = _displayHeight;
            }

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr oldBitmap = SelectObject(memDc, _cachedHBitmap);

            Size size = new Size(_displayWidth, _displayHeight);
            Point pointSrc = new Point(0, 0);
            Point topPos = new Point(this.Left, this.Top);

            BLENDFUNCTION blend = new BLENDFUNCTION();
            blend.BlendOp = AC_SRC_OVER;
            blend.BlendFlags = 0;
            blend.SourceConstantAlpha = 255;
            blend.AlphaFormat = AC_SRC_ALPHA;

            UpdateLayeredWindow(this.Handle, screenDc, ref topPos, ref size,
                memDc, ref pointSrc, 0, ref blend, ULW_ALPHA);

            // 清理 GDI 资源（缓存的 HBITMAP 保留复用，不在此释放）
            SelectObject(memDc, oldBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }

        /// <summary>不再使用标准 OnPaint，所有绘制由分层窗口接管</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            // 空实现——WS_EX_LAYERED 窗口不需要标准绘制
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 不绘制背景
        }

        // ============================================================
        // 拖拽处理
        // ============================================================

        private void PetForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                _dragOffset = e.Location;

                // 切换到拖拽图片
                if (_dragImage != null)
                {
                    _currentImage = _dragImage;
                    RefreshVisual();
                }

                // 拖拽时暂停随机移动
                _moveTargetTimer.Stop();
                _moveStepTimer.Stop();
                _isMoving = false;
            }
        }

        private void PetForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point newLocation = this.PointToScreen(e.Location);
                newLocation.Offset(-_dragOffset.X, -_dragOffset.Y);
                this.Location = newLocation;
                _currentPos = new PointF(newLocation.X, newLocation.Y);
            }
        }

        private void PetForm_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;

                // 恢复正常图片
                _currentImage = _normalImage;
                RefreshVisual();

                // 恢复随机移动（如果全局开关允许）
                if (_configMovementEnabled)
                {
                    ScheduleNextMove();
                    _moveTargetTimer.Start();
                }
            }
        }

        private void PetForm_DoubleClick(object sender, EventArgs e)
        {
            if (OnDoubleClickPet != null)
                OnDoubleClickPet();
        }

        // ============================================================
        // 随机移动
        // ============================================================

        /// <summary>
        /// 设定下一次随机移动的间隔（3~8 秒），避免在多处重复写魔法数字。
        /// </summary>
        private void ScheduleNextMove()
        {
            _moveTargetTimer.Interval = _random.Next(MoveIntervalMinMs, MoveIntervalMaxMs);
        }

        /// <summary>
        /// 切换随机移动目标（每 3~8 秒触发）
        /// </summary>
        private void MoveTargetTimer_Tick(object sender, EventArgs e)
        {
            if (_isDragging || _isMoving) return;

            // 选择新目标位置（统一使用主屏工作区，避免分层窗口在多屏/高DPI下取错屏幕）
            Screen screen = Screen.PrimaryScreen;
            int margin = PetEdgeMargin;
            float targetX = _random.Next(margin, Math.Max(margin + 1, screen.WorkingArea.Width - _displayWidth - margin));
            float targetY = _random.Next(margin, Math.Max(margin + 1, screen.WorkingArea.Height - _displayHeight - margin));

            _targetPos = new PointF(targetX, targetY);
            _isMoving = true;
            _moveStepTimer.Start();
        }

        /// <summary>
        /// 平滑移动步进（每 50ms 触发）
        /// </summary>
        private void MoveStepTimer_Tick(object sender, EventArgs e)
        {
            if (_isDragging)
            {
                _moveStepTimer.Stop();
                _isMoving = false;
                return;
            }

            float dx = _targetPos.X - _currentPos.X;
            float dy = _targetPos.Y - _currentPos.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);

            // 足够近，停止移动
            if (distance < 2f)
            {
                _currentPos = _targetPos;
                this.Location = new Point((int)_targetPos.X, (int)_targetPos.Y);
                _moveStepTimer.Stop();
                _isMoving = false;

                // 随机下一次移动间隔
                ScheduleNextMove();
                return;
            }

            // 计算步长（平滑移动，速度随距离调整）
            float stepSize = Math.Min(distance, 5f);
            float ratio = stepSize / distance;
            _currentPos = new PointF(
                _currentPos.X + dx * ratio,
                _currentPos.Y + dy * ratio
            );

            this.Location = new Point((int)_currentPos.X, (int)_currentPos.Y);
        }

        // ============================================================
        // 名言展示
        // ============================================================

        /// <summary>
        /// 在宠物上方显示名言气泡
        /// </summary>
        public void ShowQuote(string quote)
        {
            // 关闭之前的气泡
            CloseQuoteBubble();

            // 创建气泡窗口
            _quoteBubble = new QuoteBubbleForm(quote);
            _quoteBubble.Show();

            // 定位气泡在宠物上方
            PositionQuoteBubble();

            // 宠物移动时更新气泡位置
            this.LocationChanged += PetForm_LocationChanged_ForBubble;

            // 7 秒后自动关闭（使用字段化计时器，确保能被正确停止/释放，避免局部变量隐患）
            if (_quoteCloseTimer == null)
                _quoteCloseTimer = new Timer();
            _quoteCloseTimer.Interval = QuoteBubbleDisplayMs;
            _quoteCloseTimer.Tick -= QuoteCloseTimer_Tick; // 防止重复订阅
            _quoteCloseTimer.Tick += QuoteCloseTimer_Tick;
            _quoteCloseTimer.Start();
        }

        private void QuoteCloseTimer_Tick(object sender, EventArgs e)
        {
            // 只关一次；CloseQuoteBubble 内部会停止并注销本计时器
            CloseQuoteBubble();
        }

        private void PetForm_LocationChanged_ForBubble(object sender, EventArgs e)
        {
            PositionQuoteBubble();
        }

        private void PositionQuoteBubble()
        {
            if (_quoteBubble == null || _quoteBubble.IsDisposed) return;

            int bubbleX = this.Left + (this.Width - _quoteBubble.Width) / 2;
            int bubbleY = this.Top - _quoteBubble.Height - 10;

            // 确保气泡在屏幕内（统一使用主屏工作区）
            Screen screen = Screen.PrimaryScreen;
            bubbleX = Math.Max(0, Math.Min(bubbleX, screen.WorkingArea.Width - _quoteBubble.Width));
            bubbleY = Math.Max(0, bubbleY);

            _quoteBubble.Location = new Point(bubbleX, bubbleY);
        }

        private void CloseQuoteBubble()
        {
            // 注意：PetForm_LocationChanged_ForBubble 的订阅必须在此处移除，
            // 与 ShowQuote 中的 += 保持成对，避免重复订阅导致气泡位置重复刷新。
            this.LocationChanged -= PetForm_LocationChanged_ForBubble;

            // 停止并注销自动关闭计时器（避免 Tick 继续触发）
            if (_quoteCloseTimer != null)
            {
                _quoteCloseTimer.Stop();
                _quoteCloseTimer.Tick -= QuoteCloseTimer_Tick;
            }

            if (_quoteBubble != null && !_quoteBubble.IsDisposed)
            {
                _quoteBubble.Close();
                _quoteBubble.Dispose();
                _quoteBubble = null;
            }
        }

        // ============================================================
        // 提醒动作（跳跃动画）
        // ============================================================

        /// <summary>
        /// 执行提醒动作——跳跃效果（约 2 秒）
        /// </summary>
        public void DoReminderAction()
        {
            if (_reminderActionTimer != null && _reminderActionTimer.Enabled)
                return; // 已在执行中

            _originalTop = this.Top;
            _reminderActionStep = 0;

            _reminderActionTimer = new Timer();
            _reminderActionTimer.Interval = MoveStepIntervalMs; // 50ms 每步
            _reminderActionTimer.Tick += ReminderActionTimer_Tick;
            _reminderActionTimer.Start();

            // 暂停随机移动
            _moveTargetTimer.Stop();
            _moveStepTimer.Stop();
            _isMoving = false;
        }

        private void ReminderActionTimer_Tick(object sender, EventArgs e)
        {
            _reminderActionStep++;

            // 总步数 40 步（上升20步 + 下降20步 = 2秒）
            int totalSteps = 40;
            int halfSteps = totalSteps / 2;

            if (_reminderActionStep > totalSteps)
            {
                // 动画结束
                _reminderActionTimer.Stop();
                _reminderActionTimer.Dispose();
                _reminderActionTimer = null;

                this.Top = _originalTop;
                _currentPos = new PointF(this.Left, _originalTop);

                // 恢复随机移动（如果全局开关允许）
                if (_configMovementEnabled)
                {
                    ScheduleNextMove();
                    _moveTargetTimer.Start();
                }
                return;
            }

            // 计算垂直偏移（使用正弦波模拟跳跃）
            // 上升阶段切换为拖拽图，下降阶段切换回正常图
            int offset;
            if (_reminderActionStep <= halfSteps)
            {
                // 上升阶段
                float progress = (float)_reminderActionStep / halfSteps;
                offset = -(int)(Math.Sin(progress * Math.PI / 2) * 30);

                // 跳跃时显示拖拽图
                if (_dragImage != null)
                    _currentImage = _dragImage;
            }
            else
            {
                // 下降阶段
                float progress = (float)(_reminderActionStep - halfSteps) / halfSteps;
                offset = -(int)(Math.Sin((1 - progress) * Math.PI / 2) * 30);

                // 恢复正常图
                _currentImage = _normalImage;
            }

            this.Top = _originalTop + offset;
            RefreshVisual();
        }

        // ============================================================
        // 显示控制
        // ============================================================

        /// <summary>设置宠物是否随机移动（true=动, false=不动）</summary>
        public bool MovementEnabled
        {
            get { return _moveTargetTimer.Enabled; }
            set
            {
                if (value)
                {
                    ScheduleNextMove();
                    _moveTargetTimer.Start();
                }
                else
                {
                    _moveTargetTimer.Stop();
                    _moveStepTimer.Stop();
                    _isMoving = false;
                }
            }
        }

        /// <summary>设置宠物可见性</summary>
        public new bool Visible
        {
            get { return _visible; }
            set
            {
                _visible = value;
                if (value)
                {
                    // 分层窗口必须先生成内容再显示
                    if (!this.IsHandleCreated)
                        CreateHandle();
                    RefreshVisual();
                    base.Show();
                    if (_configMovementEnabled)
                        _moveTargetTimer.Start();
                }
                else
                {
                    _moveTargetTimer.Stop();
                    _moveStepTimer.Stop();
                    _isMoving = false;
                    CloseQuoteBubble();
                    base.Hide();
                }
            }
        }

        /// <summary>句柄创建后立即刷新——确保首次显示时内容就位</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RefreshVisual();
        }

        private bool _configMovementEnabled = true;
        /// <summary>记录全局移动开关状态（由外部设置）</summary>
        public bool ConfigMovementEnabled
        {
            get { return _configMovementEnabled; }
            set
            {
                _configMovementEnabled = value;
                MovementEnabled = value;
            }
        }

        /// <summary>切换显示/隐藏</summary>
        public void ToggleVisibility()
        {
            Visible = !_visible;
        }

        // ============================================================
        // 释放资源
        // ============================================================

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CloseQuoteBubble();

                // 释放缓存的分层窗口位图
                if (_cachedHBitmap != IntPtr.Zero)
                {
                    DeleteObject(_cachedHBitmap);
                    _cachedHBitmap = IntPtr.Zero;
                }

                if (_moveStepTimer != null)
                {
                    _moveStepTimer.Stop();
                    _moveStepTimer.Dispose();
                }
                if (_moveTargetTimer != null)
                {
                    _moveTargetTimer.Stop();
                    _moveTargetTimer.Dispose();
                }
                if (_reminderActionTimer != null)
                {
                    _reminderActionTimer.Stop();
                    _reminderActionTimer.Dispose();
                }
                if (_quoteCloseTimer != null)
                {
                    _quoteCloseTimer.Stop();
                    _quoteCloseTimer.Tick -= QuoteCloseTimer_Tick;
                    _quoteCloseTimer.Dispose();
                    _quoteCloseTimer = null;
                }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 名言气泡窗口——聊天气泡风格，圆角白底 + 底部小三角 + 阴影
    /// </summary>
    public class QuoteBubbleForm : Form
    {
        private Label _textLabel;
        private const int TAIL_HEIGHT = 10;   // 气泡尾巴高度
        private const int CORNER_RADIUS = 12;  // 圆角半径
        private const int PADDING = 14;        // 内边距

        public QuoteBubbleForm(string quote)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;

            // 用颜色键实现透明背景（让气泡外的区域透明）
            this.BackColor = Color.Lime;
            this.TransparencyKey = Color.Lime;

            // 文本标签
            _textLabel = new Label();
            _textLabel.Text = quote;
            _textLabel.Font = new Font("微软雅黑", 11, FontStyle.Regular);
            _textLabel.ForeColor = Color.FromArgb(50, 50, 50);
            _textLabel.BackColor = Color.Transparent;
            _textLabel.Padding = new Padding(PADDING + 2, PADDING - 2, PADDING + 2, PADDING + TAIL_HEIGHT - 2);
            _textLabel.AutoSize = true;
            _textLabel.MaximumSize = new Size(280, 0);

            this.Controls.Add(_textLabel);

            // 根据文字大小调整窗口（保证最小尺寸，避免后续绘制圆角时矩形尺寸为负）
            using (var g = this.CreateGraphics())
            {
                SizeF textSize = g.MeasureString(quote, _textLabel.Font, 280);
                int minW = PADDING * 2 + 40;
                int minH = PADDING * 2 + TAIL_HEIGHT + 30;
                this.Width = Math.Max(minW, (int)textSize.Width + PADDING * 2 + 10);
                this.Height = Math.Max(minH, (int)textSize.Height + PADDING * 2 + TAIL_HEIGHT + 10);
            }

            _textLabel.Location = new Point(0, 0);
            _textLabel.Size = new Size(this.Width, this.Height);

            // 绘制聊天气泡外形
            this.Paint += QuoteBubbleForm_Paint;

            // 不抢焦点
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        }

        private void QuoteBubbleForm_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            int bubbleH = h - TAIL_HEIGHT;
            int tailCenterX = w / 2;
            int tailW = 14;

            // 防御：窗体尺寸异常（如高 DPI / 字体度量异常导致极小或负尺寸）时跳过绘制，
            // 否则 AddRoundedRect 内的 GraphicsPath.AddArc 会因负尺寸抛出 GDI+ 异常，
            // 进而因未被捕获而终止整个消息循环、使程序静默退出。
            if (w <= 8 || h <= TAIL_HEIGHT + 4 || bubbleH <= 4)
                return;

            Rectangle bubbleRect = new Rectangle(2, 2, w - 4, bubbleH - 2);
            // === 阴影 ===
            using (var shadowPath = new GraphicsPath())
            {
                AddRoundedRect(shadowPath, new Rectangle(bubbleRect.X + 2, bubbleRect.Y + 2,
                    bubbleRect.Width, bubbleRect.Height), CORNER_RADIUS);
                // 阴影尾巴
                shadowPath.AddLine(tailCenterX - tailW / 2 + 2, bubbleH + 2,
                    tailCenterX + 3, h - 2);
                shadowPath.AddLine(tailCenterX + 3, h - 2,
                    tailCenterX + tailW / 2 + 5, bubbleH + 2);
                using (var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                {
                    g.FillPath(shadowBrush, shadowPath);
                }
            }

            // === 气泡主体 ===
            using (var bubblePath = new GraphicsPath())
            {
                AddRoundedRect(bubblePath, bubbleRect, CORNER_RADIUS);
                // 尾巴三角形
                bubblePath.AddLine(tailCenterX - tailW / 2, bubbleH,
                    tailCenterX, h - 1);
                bubblePath.AddLine(tailCenterX, h - 1,
                    tailCenterX + tailW / 2, bubbleH);
                bubblePath.CloseFigure();

                // 填充白色
                using (var fillBrush = new SolidBrush(Color.FromArgb(252, 252, 252)))
                {
                    g.FillPath(fillBrush, bubblePath);
                }

                // 边框
                using (var borderPen = new Pen(Color.FromArgb(200, 210, 210), 1.5f))
                {
                    g.DrawPath(borderPen, bubblePath);
                }
            }

            // === 尾部小圆 ===
            using (var tailDotBrush = new SolidBrush(Color.FromArgb(200, 210, 210)))
            {
                g.FillEllipse(tailDotBrush, tailCenterX - 2, bubbleH - 1, 4, 4);
            }
        }

        private static void AddRoundedRect(GraphicsPath path, Rectangle rect, int radius)
        {
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.X + rect.Width - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.X + rect.Width - d, rect.Y + rect.Height - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Y + rect.Height - d, d, d, 90, 90);
            path.CloseFigure();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }
    }
}
