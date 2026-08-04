using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace HealthyPet
{
    /// <summary>
    /// 设置窗口——包含提醒设置、桌宠设置、通用设置三个选项卡
    /// </summary>
    public class SettingsForm : Form
    {
        private AppConfig _config;
        private AppConfig _originalConfig; // 用于取消时恢复

        /// <summary>
        /// 设置界面统一使用的 UI 字体：基于系统默认字体放大 1pt。
        /// 进程级单例、只读、绝不被 Dispose——
        /// 此前用“每次 new Font”会在窗体 Dispose 后 native 句柄失效，
        /// 重新打开设置时 GroupBox.DisplayRectangle 调 Font.GetHeight() 抛 “参数无效” 崩溃。
        /// 这里只创建一次并长期持有，规避重复 new/Dispose 的句柄生命周期陷阱。
        /// </summary>
        private static readonly Font UiFont = new Font(
            SystemFonts.DefaultFont.FontFamily,
            SystemFonts.DefaultFont.SizeInPoints + 1f,
            SystemFonts.DefaultFont.Style);


        private TabControl _tabControl;

        // ==== 提醒设置 Tab ====
        private RadioButton _rbAlertMode;
        private RadioButton _rbPetActionMode;
        private TextBox _txtReminderText;
        private CheckBox _chkPomodoro;
        private NumericUpDown _nudFocusMinutes;
        private NumericUpDown _nudBreakMinutes;
        private TextBox _txtFocusEndText;
        private TextBox _txtBreakEndText;
        private NumericUpDown _nudFixedInterval; // 单一固定间隔（分钟）
        private NumericUpDown _nudAlertRest;      // 非番茄钟休息时长（秒）
        private RadioButton _rbFullScreen;
        private RadioButton _rbFitImage;            // 自适应图片大小
        private TextBox _txtActionBubble;

        // ==== 桌宠设置 Tab ====
        private ListBox _lstPets;
        private NumericUpDown _nudMinSize;
        private NumericUpDown _nudMaxSize;
        private TextBox _txtPetDefaultNormal;
        private TextBox _txtPetDefaultDrag;
        private CheckBox _chkPetMovement;

        // ==== 通用设置 Tab ====
        private CheckBox _chkAutoStart;
        private TextBox _txtTrayIcon;
        private Button _btnBrowseIcon;
        private PictureBox _picIconPreview;
        private NumericUpDown _nudQuoteInterval;
        private TextBox _txtAlertBg;
        private Button _btnBrowseAlertBg;
        private PictureBox _picAlertBgPreview;

        public SettingsForm(AppConfig config)
        {
            _config = config;
            // 保存原始配置副本用于取消恢复
            _originalConfig = new AppConfig
            {
                ReminderMode = config.ReminderMode,
                CustomReminderText = config.CustomReminderText,
                PomodoroEnabled = config.PomodoroEnabled,
                PomodoroFocusMinutes = config.PomodoroFocusMinutes,
                PomodoroBreakMinutes = config.PomodoroBreakMinutes,
                PomodoroFocusEndText = config.PomodoroFocusEndText,
                PomodoroBreakEndText = config.PomodoroBreakEndText,
                FixedIntervals = new List<int>(config.FixedIntervals ?? new List<int>()),
                ActionBubbleText = config.ActionBubbleText,
                PetMinSize = config.PetMinSize,
                PetMaxSize = config.PetMaxSize,
                AutoStart = config.AutoStart,
                TrayIconPath = config.TrayIconPath,
                QuoteIntervalMinutes = config.QuoteIntervalMinutes,
                AlertSizeMode = config.AlertSizeMode,
                AlertBackgroundImage = config.AlertBackgroundImage,
                PetMovementEnabled = config.PetMovementEnabled,
            };

            InitializeUI();
            LoadConfigToUI();
        }

        /// <summary>
        /// 初始化设置界面
        /// </summary>
        private void InitializeUI()
        {
            this.Text = "健康守护桌宠 - 设置";
            this.ClientSize = new Size(610, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            // 窗体字体：使用进程级静态只读字体（基于系统默认字体放大 1pt）。
            // 该字体只创建一次且永不 Dispose，彻底规避重复 new/Dispose 导致的
            // “参数无效” 崩溃，同时保持界面字号比系统默认大 1pt。
            this.Font = UiFont;

            // 主选项卡控件——不用 Dock，直接绝对定位
            _tabControl = new TabControl();
            _tabControl.Location = new Point(5, 5);
            _tabControl.Size = new Size(600, 460);
            _tabControl.Padding = new Point(6, 4);

            // 创建三个选项卡
            var tabReminder = new TabPage("提醒设置");
            var tabPet = new TabPage("桌宠设置");
            var tabGeneral = new TabPage("通用设置");

            BuildReminderTab(tabReminder);
            BuildPetTab(tabPet);
            BuildGeneralTab(tabGeneral);

            _tabControl.TabPages.Add(tabReminder);
            _tabControl.TabPages.Add(tabPet);
            _tabControl.TabPages.Add(tabGeneral);

            this.Controls.Add(_tabControl);

            // 底部按钮——直接放在窗体上，不使用 Panel
            // 保存并退出按钮
            var btnOK = new Button();
            btnOK.Text = "保存并退出";
            btnOK.Size = new Size(130, 35);
            btnOK.Location = new Point(365, 475);
            btnOK.Click += BtnOK_Click;

            // 取消按钮
            var btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.Size = new Size(100, 35);
            btnCancel.Location = new Point(505, 475);
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);

            // 设置回车键 = 确定，ESC = 取消
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        // ============================================================
        // 提醒设置 Tab
        // ============================================================
        private void BuildReminderTab(TabPage tab)
        {
            tab.Padding = new Padding(15);
            int y = 15;
            int labelWidth = 120;

            // 提醒方式
            var grpMode = new GroupBox();
            grpMode.Text = "提醒方式";
            grpMode.Location = new Point(10, y);
            grpMode.Size = new Size(570, 50);

            _rbAlertMode = new RadioButton();
            _rbAlertMode.Text = "弹窗提醒";
            _rbAlertMode.Location = new Point(15, 20);
            _rbAlertMode.AutoSize = true;

            _rbPetActionMode = new RadioButton();
            _rbPetActionMode.Text = "宠物动作提醒";
            _rbPetActionMode.Location = new Point(150, 20);
            _rbPetActionMode.AutoSize = true;

            grpMode.Controls.Add(_rbAlertMode);
            grpMode.Controls.Add(_rbPetActionMode);
            tab.Controls.Add(grpMode);
            y += 60;

            // 弹窗大小
            var grpAlertSize = new GroupBox();
            grpAlertSize.Text = "弹窗大小";
            grpAlertSize.Location = new Point(10, y);
            grpAlertSize.Size = new Size(570, 50);

            _rbFullScreen = new RadioButton();
            _rbFullScreen.Text = "全屏";
            _rbFullScreen.Location = new Point(15, 20);
            _rbFullScreen.AutoSize = true;

            _rbFitImage = new RadioButton();
            _rbFitImage.Text = "按图片自适应大小";
            _rbFitImage.Location = new Point(120, 20);
            _rbFitImage.AutoSize = true;

            grpAlertSize.Controls.Add(_rbFullScreen);
            grpAlertSize.Controls.Add(_rbFitImage);
            tab.Controls.Add(grpAlertSize);
            y += 60;

            // 自定义提醒文字
            var lblText = new Label();
            lblText.Text = "提醒文字：";
            lblText.Location = new Point(15, y);
            lblText.Size = new Size(labelWidth, 25);
            tab.Controls.Add(lblText);

            _txtReminderText = new TextBox();
            _txtReminderText.Location = new Point(15 + labelWidth, y);
            _txtReminderText.Size = new Size(430, 23);
            tab.Controls.Add(_txtReminderText);
            y += 35;

            // 番茄钟模式
            var grpPomodoro = new GroupBox();
            grpPomodoro.Text = "番茄钟模式";
            grpPomodoro.Location = new Point(10, y);
            grpPomodoro.Size = new Size(570, 110);

            _chkPomodoro = new CheckBox();
            _chkPomodoro.Text = "开启番茄钟";
            _chkPomodoro.Location = new Point(15, 22);
            _chkPomodoro.AutoSize = true;
            _chkPomodoro.CheckedChanged += (s, e) =>
            {
                bool en = _chkPomodoro.Checked;
                _nudFocusMinutes.Enabled = en;
                _nudBreakMinutes.Enabled = en;
                _txtFocusEndText.Enabled = en;
                _txtBreakEndText.Enabled = en;
            };

            var lblFocus = new Label();
            lblFocus.Text = "专注时长(分)：";
            lblFocus.Location = new Point(35, 48);
            lblFocus.AutoSize = true;
            _nudFocusMinutes = new NumericUpDown();
            _nudFocusMinutes.Location = new Point(140, 45);
            _nudFocusMinutes.Size = new Size(60, 23);
            _nudFocusMinutes.Minimum = 1;
            _nudFocusMinutes.Maximum = 120;
            _nudFocusMinutes.Value = 25;

            var lblBreak = new Label();
            lblBreak.Text = "休息时长(分)：";
            lblBreak.Location = new Point(220, 48);
            lblBreak.AutoSize = true;
            _nudBreakMinutes = new NumericUpDown();
            _nudBreakMinutes.Location = new Point(325, 45);
            _nudBreakMinutes.Size = new Size(60, 23);
            _nudBreakMinutes.Minimum = 1;
            _nudBreakMinutes.Maximum = 60;
            _nudBreakMinutes.Value = 5;

            var lblFocusEnd = new Label();
            lblFocusEnd.Text = "专注结束文字：";
            lblFocusEnd.Location = new Point(20, 78);
            lblFocusEnd.AutoSize = true;
            _txtFocusEndText = new TextBox();
            _txtFocusEndText.Location = new Point(120, 75);
            _txtFocusEndText.Size = new Size(200, 23);

            var lblBreakEnd = new Label();
            lblBreakEnd.Text = "休息结束文字：";
            lblBreakEnd.Location = new Point(335, 78);
            lblBreakEnd.AutoSize = true;
            _txtBreakEndText = new TextBox();
            _txtBreakEndText.Location = new Point(435, 75);
            _txtBreakEndText.Size = new Size(120, 23);

            grpPomodoro.Controls.Add(_chkPomodoro);
            grpPomodoro.Controls.Add(lblFocus);
            grpPomodoro.Controls.Add(_nudFocusMinutes);
            grpPomodoro.Controls.Add(lblBreak);
            grpPomodoro.Controls.Add(_nudBreakMinutes);
            grpPomodoro.Controls.Add(lblFocusEnd);
            grpPomodoro.Controls.Add(_txtFocusEndText);
            grpPomodoro.Controls.Add(lblBreakEnd);
            grpPomodoro.Controls.Add(_txtBreakEndText);

            tab.Controls.Add(grpPomodoro);
            y += 120;

            // 固定间隔任务
            var grpIntervals = new GroupBox();
            grpIntervals.Text = "固定间隔提醒";
            grpIntervals.Location = new Point(10, y);
            // 缩小组高度：两个输入框放在同一行即可，不再纵向堆叠
            grpIntervals.Size = new Size(570, 70);

            // 左侧：固定间隔
            _nudFixedInterval = new NumericUpDown();
            _nudFixedInterval.Location = new Point(15, 25);
            _nudFixedInterval.Size = new Size(80, 26);
            _nudFixedInterval.Minimum = 1;
            _nudFixedInterval.Maximum = 480;
            _nudFixedInterval.Value = 45;

            var lblUnit = new Label();
            lblUnit.Text = "分钟";
            lblUnit.Location = new Point(100, 29);
            lblUnit.AutoSize = true;

            // 右侧：休息时长——与固定间隔输入框同一行
            var lblAlertRest = new Label();
            lblAlertRest.Text = "休息时长(秒)：";
            lblAlertRest.Location = new Point(180, 29);
            lblAlertRest.AutoSize = true;
            _nudAlertRest = new NumericUpDown();
            _nudAlertRest.Location = new Point(290, 25);
            // 加大宽高 + 左对齐：修复放大 1pt 字号后单字符"1"被右对齐 + 上下箭头按钮遮挡的显示问题
            _nudAlertRest.Size = new Size(90, 26);
            _nudAlertRest.Minimum = 5;
            _nudAlertRest.Maximum = 600;
            _nudAlertRest.Value = 30;
            _nudAlertRest.TextAlign = HorizontalAlignment.Left;
            var lblAlertRestUnit = new Label();
            lblAlertRestUnit.Text = "秒";
            lblAlertRestUnit.Location = new Point(385, 29);
            lblAlertRestUnit.AutoSize = true;

            grpIntervals.Controls.Add(_nudFixedInterval);
            grpIntervals.Controls.Add(lblUnit);
            grpIntervals.Controls.Add(lblAlertRest);
            grpIntervals.Controls.Add(_nudAlertRest);
            grpIntervals.Controls.Add(lblAlertRestUnit);

            tab.Controls.Add(grpIntervals);
            y += 40;

            // 动作气泡文字
            var lblActionBubble = new Label();
            lblActionBubble.Text = "宠物动作气泡文字：";
            lblActionBubble.Location = new Point(15, y);
            lblActionBubble.Size = new Size(140, 25);
            tab.Controls.Add(lblActionBubble);

            _txtActionBubble = new TextBox();
            _txtActionBubble.Location = new Point(155, y);
            _txtActionBubble.Size = new Size(420, 23);
            tab.Controls.Add(_txtActionBubble);
        }

        // ============================================================
        // 桌宠设置 Tab
        // ============================================================
        private void BuildPetTab(TabPage tab)
        {
            tab.Padding = new Padding(15);
            int y = 15;
            int labelWidth = 100;

            // 宠物列表管理
            var grpPets = new GroupBox();
            grpPets.Text = "桌宠管理（每行 = 一只独立桌宠）";
            grpPets.Location = new Point(10, y);
            grpPets.Size = new Size(570, 170);

            _lstPets = new ListBox();
            _lstPets.Location = new Point(15, 22);
            _lstPets.Size = new Size(300, 130);

            var btnAddPet = new Button();
            btnAddPet.Text = "添加桌宠";
            btnAddPet.Location = new Point(330, 25);
            btnAddPet.Size = new Size(100, 30);
            btnAddPet.Click += BtnAddPet_Click;

            var btnRemovePet = new Button();
            btnRemovePet.Text = "删除选中";
            btnRemovePet.Location = new Point(440, 25);
            btnRemovePet.Size = new Size(100, 30);
            btnRemovePet.Click += BtnRemovePet_Click;

            var btnEditPet = new Button();
            btnEditPet.Text = "编辑图片";
            btnEditPet.Location = new Point(330, 65);
            btnEditPet.Size = new Size(100, 30);
            btnEditPet.Click += BtnEditPet_Click;

            grpPets.Controls.Add(_lstPets);
            grpPets.Controls.Add(btnAddPet);
            grpPets.Controls.Add(btnRemovePet);
            grpPets.Controls.Add(btnEditPet);
            tab.Controls.Add(grpPets);
            y += 180;

            // 宠物尺寸范围
            var lblMinSize = new Label();
            lblMinSize.Text = "最小尺寸(px)：";
            lblMinSize.Location = new Point(15, y);
            lblMinSize.Size = new Size(labelWidth, 25);
            tab.Controls.Add(lblMinSize);

            _nudMinSize = new NumericUpDown();
            _nudMinSize.Location = new Point(15 + labelWidth, y);
            _nudMinSize.Size = new Size(70, 23);
            _nudMinSize.Minimum = 20;
            _nudMinSize.Maximum = 200;
            tab.Controls.Add(_nudMinSize);

            var lblMaxSize = new Label();
            lblMaxSize.Text = "最大尺寸(px)：";
            lblMaxSize.Location = new Point(220, y);
            lblMaxSize.Size = new Size(labelWidth, 25);
            tab.Controls.Add(lblMaxSize);

            _nudMaxSize = new NumericUpDown();
            _nudMaxSize.Location = new Point(320, y);
            _nudMaxSize.Size = new Size(70, 23);
            _nudMaxSize.Minimum = 20;
            _nudMaxSize.Maximum = 200;
            tab.Controls.Add(_nudMaxSize);
            y += 40;

            // 默认图片路径
            var lblDefaultNorm = new Label();
            lblDefaultNorm.Text = "默认正常图：";
            lblDefaultNorm.Location = new Point(15, y);
            lblDefaultNorm.Size = new Size(labelWidth, 25);
            tab.Controls.Add(lblDefaultNorm);

            _txtPetDefaultNormal = new TextBox();
            _txtPetDefaultNormal.Location = new Point(115, y);
            _txtPetDefaultNormal.Size = new Size(300, 23);
            _txtPetDefaultNormal.ReadOnly = true;
            tab.Controls.Add(_txtPetDefaultNormal);

            var btnBrowseDefNorm = new Button();
            btnBrowseDefNorm.Text = "浏览";
            btnBrowseDefNorm.Location = new Point(420, y);
            btnBrowseDefNorm.Size = new Size(60, 25);
            btnBrowseDefNorm.Click += (s, e) =>
            {
                BrowseImageFile(_txtPetDefaultNormal.Text, path => _txtPetDefaultNormal.Text = path);
            };
            tab.Controls.Add(btnBrowseDefNorm);
            y += 30;

            var lblDefaultDrag = new Label();
            lblDefaultDrag.Text = "默认拖拽图：";
            lblDefaultDrag.Location = new Point(15, y);
            lblDefaultDrag.Size = new Size(labelWidth, 25);
            tab.Controls.Add(lblDefaultDrag);

            _txtPetDefaultDrag = new TextBox();
            _txtPetDefaultDrag.Location = new Point(115, y);
            _txtPetDefaultDrag.Size = new Size(300, 23);
            _txtPetDefaultDrag.ReadOnly = true;
            tab.Controls.Add(_txtPetDefaultDrag);

            var btnBrowseDefDrag = new Button();
            btnBrowseDefDrag.Text = "浏览";
            btnBrowseDefDrag.Location = new Point(420, y);
            btnBrowseDefDrag.Size = new Size(60, 25);
            btnBrowseDefDrag.Click += (s, e) =>
            {
                BrowseImageFile(_txtPetDefaultDrag.Text, path => _txtPetDefaultDrag.Text = path);
            };
            tab.Controls.Add(btnBrowseDefDrag);
            y += 40;

            var btnResetDef = new Button();
            btnResetDef.Text = "重置为默认";
            btnResetDef.Location = new Point(15, y);
            btnResetDef.Size = new Size(100, 28);
            btnResetDef.Click += (s, e) =>
            {
                _txtPetDefaultNormal.Text = "Data\\pet_normal.png";
                _txtPetDefaultDrag.Text = "Data\\pet_drag.png";
            };
            tab.Controls.Add(btnResetDef);
            y += 35;

            // 宠物随机移动开关
            _chkPetMovement = new CheckBox();
            _chkPetMovement.Text = "允许宠物随机移动（取消则静止不动）";
            _chkPetMovement.Location = new Point(15, y);
            _chkPetMovement.AutoSize = true;
            tab.Controls.Add(_chkPetMovement);
        }

        // ============================================================
        // 通用设置 Tab
        // ============================================================
        private void BuildGeneralTab(TabPage tab)
        {
            tab.Padding = new Padding(15);
            int y = 15;
            int labelWidth = 120;

            // 开机自启
            _chkAutoStart = new CheckBox();
            _chkAutoStart.Text = "开机自动启动";
            _chkAutoStart.Location = new Point(15, y);
            _chkAutoStart.AutoSize = true;
            tab.Controls.Add(_chkAutoStart);
            y += 35;

            // 托盘图标
            var lblIcon = new Label();
            lblIcon.Text = "托盘图标：";
            lblIcon.Location = new Point(15, y);
            lblIcon.Size = new Size(labelWidth, 25);
            tab.Controls.Add(lblIcon);

            _txtTrayIcon = new TextBox();
            _txtTrayIcon.Location = new Point(15 + labelWidth, y);
            _txtTrayIcon.Size = new Size(300, 23);
            _txtTrayIcon.ReadOnly = true;
            tab.Controls.Add(_txtTrayIcon);

            _btnBrowseIcon = new Button();
            _btnBrowseIcon.Text = "浏览...";
            _btnBrowseIcon.Location = new Point(15 + labelWidth + 310, y);
            _btnBrowseIcon.Size = new Size(70, 25);
            _btnBrowseIcon.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Title = "选择托盘图标 (.ico)";
                    dlg.Filter = "图标文件|*.ico";
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        _txtTrayIcon.Text = dlg.FileName;
                        if (File.Exists(dlg.FileName))
                            _picIconPreview.Image = new Icon(dlg.FileName, 32, 32).ToBitmap();
                    }
                }
            };
            tab.Controls.Add(_btnBrowseIcon);

            _picIconPreview = new PictureBox();
            _picIconPreview.Location = new Point(15 + labelWidth + 310 + 80, y - 5);
            _picIconPreview.Size = new Size(32, 32);
            _picIconPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _picIconPreview.BorderStyle = BorderStyle.FixedSingle;
            tab.Controls.Add(_picIconPreview);
            y += 40;

            // 名言展示间隔
            var lblQuote = new Label();
            lblQuote.Text = "鸡汤展示间隔(分)：";
            lblQuote.Location = new Point(15, y);
            lblQuote.Size = new Size(labelWidth + 20, 25);
            tab.Controls.Add(lblQuote);

            _nudQuoteInterval = new NumericUpDown();
            _nudQuoteInterval.Location = new Point(15 + labelWidth + 20, y);
            _nudQuoteInterval.Size = new Size(80, 23);
            _nudQuoteInterval.Minimum = 1;
            _nudQuoteInterval.Maximum = 120;
            tab.Controls.Add(_nudQuoteInterval);
            y += 40;

            // 弹窗背景图
            var lblAlertBg = new Label();
            lblAlertBg.Text = "弹窗背景图：";
            lblAlertBg.Location = new Point(15, y);
            lblAlertBg.Size = new Size(labelWidth, 25);
            tab.Controls.Add(lblAlertBg);

            _txtAlertBg = new TextBox();
            _txtAlertBg.Location = new Point(15 + labelWidth, y);
            _txtAlertBg.Size = new Size(300, 23);
            _txtAlertBg.ReadOnly = true;
            tab.Controls.Add(_txtAlertBg);

            _btnBrowseAlertBg = new Button();
            _btnBrowseAlertBg.Text = "浏览...";
            _btnBrowseAlertBg.Location = new Point(15 + labelWidth + 310, y);
            _btnBrowseAlertBg.Size = new Size(70, 25);
            _btnBrowseAlertBg.Click += (s, e) =>
            {
                BrowseImageFile(_txtAlertBg.Text, path =>
                {
                    _txtAlertBg.Text = path;
                    // 预览
                    try
                    {
                        if (File.Exists(path))
                            _picAlertBgPreview.Image = Image.FromFile(path);
                    }
                    catch { }
                });
            };
            tab.Controls.Add(_btnBrowseAlertBg);

            _picAlertBgPreview = new PictureBox();
            _picAlertBgPreview.Location = new Point(15 + labelWidth + 310 + 80, y - 5);
            _picAlertBgPreview.Size = new Size(32, 32);
            _picAlertBgPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _picAlertBgPreview.BorderStyle = BorderStyle.FixedSingle;
            tab.Controls.Add(_picAlertBgPreview);
        }

        // ============================================================
        // 数据绑定
        // ============================================================

        /// <summary>
        /// 将配置加载到 UI 控件
        /// </summary>
        private void LoadConfigToUI()
        {
            // 提醒设置
            _rbAlertMode.Checked = _config.ReminderMode == 0;
            _rbPetActionMode.Checked = _config.ReminderMode == 1;
            _rbFullScreen.Checked = _config.AlertSizeMode == 0;
            _rbFitImage.Checked = _config.AlertSizeMode == 1;
            _txtReminderText.Text = _config.CustomReminderText;
            _txtActionBubble.Text = _config.ActionBubbleText ?? "休息一下吧~";
            _chkPomodoro.Checked = _config.PomodoroEnabled;
            _nudFocusMinutes.Value = _config.PomodoroFocusMinutes;
            _nudBreakMinutes.Value = _config.PomodoroBreakMinutes;
            _txtFocusEndText.Text = _config.PomodoroFocusEndText;
            _txtBreakEndText.Text = _config.PomodoroBreakEndText;

            _nudFocusMinutes.Enabled = _config.PomodoroEnabled;
            _nudBreakMinutes.Enabled = _config.PomodoroEnabled;
            _txtFocusEndText.Enabled = _config.PomodoroEnabled;
            _txtBreakEndText.Enabled = _config.PomodoroEnabled;

            int interval = (_config.FixedIntervals != null && _config.FixedIntervals.Count > 0)
                ? _config.FixedIntervals[0]
                : 45;
            _nudFixedInterval.Value = Math.Max(_nudFixedInterval.Minimum, Math.Min(_nudFixedInterval.Maximum, interval));

            // 非番茄钟休息时长
            _nudAlertRest.Value = Math.Max(_nudAlertRest.Minimum, Math.Min(_nudAlertRest.Maximum, _config.AlertRestSeconds));

            // 桌宠设置
            _nudMinSize.Value = Math.Max(20, Math.Min(200, _config.PetMinSize));
            _nudMaxSize.Value = Math.Max(20, Math.Min(200, _config.PetMaxSize));

            // 宠物列表
            _lstPets.Items.Clear();
            if (_config.Pets != null)
                foreach (var def in _config.Pets)
                    _lstPets.Items.Add(def);

            // 默认图片路径（用于新增宠物）
            _txtPetDefaultNormal.Text = _config.PetNormalImage ?? "Data\\pet_normal.png";
            _txtPetDefaultDrag.Text = _config.PetDragImage ?? "Data\\pet_drag.png";
            _chkPetMovement.Checked = _config.PetMovementEnabled;

            // 通用设置
            _chkAutoStart.Checked = _config.AutoStart;
            _txtTrayIcon.Text = _config.TrayIconPath;
            _nudQuoteInterval.Value = Math.Max(1, Math.Min(120, _config.QuoteIntervalMinutes));
            _txtAlertBg.Text = _config.AlertBackgroundImage ?? "Data\\background.jpg";

            string iconPath = _config.GetFullPath(_config.TrayIconPath);
            if (File.Exists(iconPath))
            {
                try { _picIconPreview.Image = new Icon(iconPath, 32, 32).ToBitmap(); }
                catch { }
            }
            string bgPath = _config.GetFullPath(_config.AlertBackgroundImage);
            if (File.Exists(bgPath))
            {
                try { _picAlertBgPreview.Image = Image.FromFile(bgPath); }
                catch { }
            }
        }

        /// <summary>
        /// 从 UI 控件收集配置
        /// </summary>
        private void SaveUIToConfig()
        {
            _config.ReminderMode = _rbAlertMode.Checked ? 0 : 1;
            _config.AlertSizeMode = _rbFullScreen.Checked ? 0 : 1; // 1 = 按图片自适应大小
            _config.CustomReminderText = _txtReminderText.Text.Trim();
            _config.ActionBubbleText = _txtActionBubble.Text.Trim();
            _config.PomodoroEnabled = _chkPomodoro.Checked;
            _config.PomodoroFocusMinutes = (int)_nudFocusMinutes.Value;
            _config.PomodoroBreakMinutes = (int)_nudBreakMinutes.Value;
            _config.PomodoroFocusEndText = _txtFocusEndText.Text.Trim();
            _config.PomodoroBreakEndText = _txtBreakEndText.Text.Trim();

            _config.FixedIntervals = new List<int> { (int)_nudFixedInterval.Value };
            _config.AlertRestSeconds = (int)_nudAlertRest.Value;

            // 从列表读取宠物定义
            _config.Pets = new List<PetDefinition>();
            foreach (var item in _lstPets.Items)
            {
                var def = item as PetDefinition;
                if (def != null)
                    _config.Pets.Add(def);
            }
            // 至少保留一个
            if (_config.Pets.Count == 0)
                _config.Pets.Add(new PetDefinition());

            _config.PetNormalImage = _txtPetDefaultNormal.Text.Trim();
            _config.PetDragImage = _txtPetDefaultDrag.Text.Trim();
            _config.PetMinSize = (int)_nudMinSize.Value;
            _config.PetMaxSize = (int)_nudMaxSize.Value;
            _config.PetMovementEnabled = _chkPetMovement.Checked;

            _config.AutoStart = _chkAutoStart.Checked;
            _config.TrayIconPath = _txtTrayIcon.Text.Trim();
            _config.QuoteIntervalMinutes = (int)_nudQuoteInterval.Value;
            _config.AlertBackgroundImage = _txtAlertBg.Text.Trim();
        }

        // ============================================================
        // 事件处理
        // ============================================================

        /// <summary>强制所有 NumericUpDown 控件验证输入值（防止用户直接输入后值未更新）</summary>
        private void ForceValidateAllInputs()
        {
            // 让当前焦点控件先失去焦点，触发 Validating 事件，确保 NumericUpDown.Value 已更新
            this.ActiveControl = null;

            // 手动触发所有 NumericUpDown 的验证
            ForceNudValidate(_nudFocusMinutes);
            ForceNudValidate(_nudBreakMinutes);
            ForceNudValidate(_nudMinSize);
            ForceNudValidate(_nudMaxSize);
            ForceNudValidate(_nudFixedInterval);
            ForceNudValidate(_nudAlertRest);
            ForceNudValidate(_nudQuoteInterval);
        }

        private void ForceNudValidate(NumericUpDown nud)
        {
            try
            {
                // 解析 Text 属性确保数值已提交
                decimal val;
                if (decimal.TryParse(nud.Text, out val))
                {
                    if (val < nud.Minimum) val = nud.Minimum;
                    if (val > nud.Maximum) val = nud.Maximum;
                    nud.Value = val;
                }
            }
            catch { }
        }

        /// <summary>确定按钮——保存配置并关闭窗口</summary>
        private void BtnOK_Click(object sender, EventArgs e)
        {
            // 强制读取用户输入
            ForceValidateAllInputs();

            // 从 UI 读值到配置
            SaveUIToConfig();

            // 写入文件
            try
            {
                _config.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 开机自启
            HandleAutoStart(_config.AutoStart);

            // 通知 MainApp
            if (OnConfigSaved != null)
                OnConfigSaved(_config);

            // 显示保存结果
            string msg = string.Format(
                "设置已保存！\n\n" +
                "桌宠数量: {0} 只\n" +
                "名言间隔: {1} 分钟\n" +
                "固定间隔: {2} 分钟",
                _lstPets.Items.Count,
                _config.QuoteIntervalMinutes,
                _config.FixedIntervals != null && _config.FixedIntervals.Count > 0
                    ? string.Join(", ", _config.FixedIntervals) : "无"
            );
            MessageBox.Show(msg, "保存并退出", MessageBoxButtons.OK, MessageBoxIcon.Information);

            this.Close();
        }

        /// <summary>配置保存事件（MainApp 监听此事件来刷新设置）</summary>
        public event Action<AppConfig> OnConfigSaved;

        // ==== 宠物管理事件 ====

        private void BtnAddPet_Click(object sender, EventArgs e)
        {
            var def = new PetDefinition
            {
                NormalImage = _txtPetDefaultNormal.Text.Trim(),
                DragImage = _txtPetDefaultDrag.Text.Trim()
            };
            _lstPets.Items.Add(def);
            _lstPets.SelectedIndex = _lstPets.Items.Count - 1;
        }

        private void BtnRemovePet_Click(object sender, EventArgs e)
        {
            if (_lstPets.SelectedIndex >= 0)
            {
                _lstPets.Items.RemoveAt(_lstPets.SelectedIndex);
            }
        }

        private void BtnEditPet_Click(object sender, EventArgs e)
        {
            if (_lstPets.SelectedIndex < 0)
            {
                MessageBox.Show("请先选中一个桌宠", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var def = _lstPets.SelectedItem as PetDefinition;
            if (def == null) return;

            // 选择正常图片
            string normalPath = def.NormalImage;
            if (BrowseImageFileDialog("选择正常状态图片", out normalPath))
                def.NormalImage = normalPath;

            // 选择拖拽图片
            string dragPath = def.DragImage;
            if (BrowseImageFileDialog("选择拖拽状态图片", out dragPath))
                def.DragImage = dragPath;

            // 刷新列表显示
            int idx = _lstPets.SelectedIndex;
            _lstPets.Items[idx] = def;
            _lstPets.SelectedIndex = idx;
        }

        private void BrowseImageFile(string currentPath, Action<string> onSelected)
        {
            string path = currentPath;
            if (BrowseImageFileDialog("选择图片文件", out path))
                onSelected(path);
        }

        private bool BrowseImageFileDialog(string title, out string path)
        {
            path = "";
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = title;
                dlg.Filter = "PNG 图片|*.png|所有图片|*.png;*.jpg;*.jpeg;*.bmp";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    path = dlg.FileName;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 处理开机自启——在用户启动文件夹创建/删除快捷方式
        /// </summary>
        private void HandleAutoStart(bool enable)
        {
            try
            {
                string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupPath, "健康守护桌宠.lnk");

                if (enable)
                {
                    // 创建快捷方式
                    string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HealthyPet.exe");
                    CreateShortcut(shortcutPath, exePath);
                }
                else
                {
                    // 删除快捷方式
                    if (File.Exists(shortcutPath))
                        File.Delete(shortcutPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("开机自启设置失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 使用 PowerShell 创建 Windows 快捷方式（无需 COM 引用）
        /// </summary>
        private void CreateShortcut(string shortcutPath, string targetPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(targetPath);
                // 转义单引号，防止 PowerShell 注入
                string safeShortcut = shortcutPath.Replace("'", "''");
                string safeTarget = targetPath.Replace("'", "''");
                string safeDir = dir.Replace("'", "''");

                string script = string.Format(
                    "-Command \"$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('{0}'); $s.TargetPath = '{1}'; $s.WorkingDirectory = '{2}'; $s.Description = '健康守护桌宠'; $s.Save()\"",
                    safeShortcut, safeTarget, safeDir);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = script,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    p.WaitForExit(5000);
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("创建快捷方式失败");
            }
        }

        /// <summary>
        /// 重写 Dispose：在释放窗体资源前，先把 this.Font 置空，
        /// 避免 base.Dispose 把进程级共享字体 UiFont 一起释放，
        /// 否则下一次打开设置时 UiFont 已是失效句柄，
        /// GroupBox.DisplayRectangle 调 Font.GetHeight() 会抛 “参数无效” 崩溃。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 解除对共享字体 UiFont 的引用，使其逃过本次 Dispose
                if (ReferenceEquals(this.Font, UiFont))
                    this.Font = null;
            }
            base.Dispose(disposing);
        }
    }
}
