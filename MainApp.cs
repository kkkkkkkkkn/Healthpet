using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace HealthyPet
{
    /// <summary>
    /// 主应用程序控制器——管理托盘图标、宠物实例、提醒调度
    /// </summary>
    public class MainApp : IDisposable
    {
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _menuShowPets;
        private ToolStripMenuItem _menuPauseReminder;
        private ToolStripMenuItem _menuFreezePet;

        private AppConfig _config;
        private QuoteManager _quoteManager;
        private ReminderScheduler _scheduler;
        private List<PetForm> _petForms;

        private bool _petsVisible = true;
        private bool _remindersPaused = false;
        private bool _petMovementEnabled = true;
        private SettingsForm _currentSettingsForm;

        /// <summary>
        /// 初始化应用程序——创建托盘图标、加载配置、启动宠物和调度器
        /// </summary>
        public void Initialize()
        {
            // 1. 确保默认资源存在
            ResourceGenerator.EnsureResourcesExist();

            // 2. 加载配置
            _config = AppConfig.Load();

            // 3. 加载名言
            _quoteManager = new QuoteManager();
            string quotesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "quotes.txt");
            _quoteManager.Load(quotesPath);

            // 4. 创建托盘图标
            CreateTrayIcon();

            // 5. 创建宠物实例
            _petForms = new List<PetForm>();
            CreatePets();

            // 6. 启动提醒调度器
            StartScheduler();

            // 7. 处理开机自启
            if (_config.AutoStart)
            {
                // 已经在设置时处理了，这里做一次验证
            }
        }

        // ============================================================
        // 托盘图标与右键菜单
        // ============================================================

        private void CreateTrayIcon()
        {
            _trayMenu = new ContextMenuStrip();

            // 菜单项：打开设置
            var menuSettings = new ToolStripMenuItem("打开设置");
            menuSettings.Click += (s, e) => OpenSettings();
            _trayMenu.Items.Add(menuSettings);

            _trayMenu.Items.Add(new ToolStripSeparator());

            // 菜单项：显示/隐藏宠物
            _menuShowPets = new ToolStripMenuItem("隐藏宠物");
            _menuShowPets.Click += (s, e) => TogglePetsVisibility();
            _trayMenu.Items.Add(_menuShowPets);

            // 菜单项：暂停/恢复提醒
            _menuPauseReminder = new ToolStripMenuItem("暂停提醒");
            _menuPauseReminder.Click += (s, e) => ToggleRemindersPause();
            _trayMenu.Items.Add(_menuPauseReminder);

            // 菜单项：冻结/解冻宠物移动
            _petMovementEnabled = _config.PetMovementEnabled;
            _menuFreezePet = new ToolStripMenuItem(_petMovementEnabled ? "冻结宠物" : "解冻宠物");
            _menuFreezePet.Click += (s, e) => TogglePetMovement();
            _trayMenu.Items.Add(_menuFreezePet);

            _trayMenu.Items.Add(new ToolStripSeparator());

            // 菜单项：退出
            var menuExit = new ToolStripMenuItem("退出");
            menuExit.Click += (s, e) => ExitApplication();
            _trayMenu.Items.Add(menuExit);

            // 创建 NotifyIcon
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = "健康守护桌宠";
            _notifyIcon.ContextMenuStrip = _trayMenu;
            _notifyIcon.Visible = true;

            // 加载托盘图标
            LoadTrayIcon();

            // 双击托盘图标 → 打开设置
            _notifyIcon.DoubleClick += (s, e) => OpenSettings();
        }

        private void LoadTrayIcon()
        {
            try
            {
                string iconPath = _config.GetFullPath(_config.TrayIconPath);
                if (File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    // 回退：使用应用图标
                    _notifyIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }
        }

        // ============================================================
        // 宠物管理
        // ============================================================

        /// <summary>
        /// 根据配置的 Pets 列表创建宠物实例（每只可用不同图片）
        /// </summary>
        private void CreatePets()
        {
            DestroyAllPets();

            var pets = _config.Pets;
            if (pets == null || pets.Count == 0)
                pets = new List<PetDefinition> { new PetDefinition() };

            Random baseRand = new Random();

            foreach (var def in pets)
            {
                // 加载每只宠物的独立图片，失败则用默认图
                Image normalImage = LoadPetImage(def.NormalImage);
                Image dragImage = LoadPetImage(def.DragImage);
                if (normalImage == null)
                    normalImage = LoadPetImage("Data\\pet_normal.png");
                if (dragImage == null)
                    dragImage = LoadPetImage("Data\\pet_drag.png");

                int size = baseRand.Next(_config.PetMinSize, _config.PetMaxSize + 1);
                var petSize = new Size(size, size);

                // 每只宠物用一个派生自公共种子的独立 Random，避免同毫秒种子导致同质化
                var pet = new PetForm(normalImage, dragImage, petSize, new Random(baseRand.Next()));
                pet.OnDoubleClickPet += () => OpenSettings();
                pet.ConfigMovementEnabled = _petMovementEnabled;

                if (_petsVisible)
                    pet.Show();

                _petForms.Add(pet);
            }
        }

        private Image LoadPetImage(string relativePath)
        {
            try
            {
                string path = _config.GetFullPath(relativePath);
                if (!File.Exists(path))
                    return null;

                // 使用无锁方式加载（克隆字节，避免 Image.FromFile 长期占用文件）
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    var img = Image.FromStream(fs);
                    // 克隆为独立 Bitmap，脱离文件流依赖
                    var clone = new Bitmap(img);
                    img.Dispose();

                    // 自动裁剪空白边缘，只保留主体内容
                    try
                    {
                        var cropped = ImageHelper.AutoCrop(clone);
                        if (cropped != null && cropped != clone)
                        {
                            clone.Dispose();
                            return cropped;
                        }
                        return clone;
                    }
                    catch
                    {
                        // 裁剪失败时退回原图
                        return clone;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("加载宠物图片失败: " + relativePath, ex);
                return null;
            }
        }

        /// <summary>
        /// 销毁所有宠物实例
        /// </summary>
        private void DestroyAllPets()
        {
            if (_petForms == null) return;

            foreach (var pet in _petForms)
            {
                try
                {
                    pet.Close();
                    pet.Dispose();
                }
                catch { }
            }
            _petForms.Clear();
        }

        /// <summary>
        /// 判断两次配置之间，是否发生了影响宠物外观/数量的变更。
        /// 仅以下字段变化时宠物才需要重建：宠物列表（数量或每只的图片路径）、
        /// 尺寸范围、动作气泡文字。其余（提醒方式、间隔、番茄钟、名言等）不影响宠物本身。
        /// </summary>
        private static bool PetConfigChanged(List<PetDefinition> oldPets, List<PetDefinition> newPets)
        {
            int oldMin = oldPets != null ? oldPets.Count : 0;
            int newMin = newPets != null ? newPets.Count : 0;
            // 若任一为 null，视为需要重建
            if (oldPets == null || newPets == null) return true;
            if (oldPets.Count != newPets.Count) return true;

            for (int i = 0; i < oldPets.Count; i++)
            {
                if (oldPets[i].NormalImage != newPets[i].NormalImage) return true;
                if (oldPets[i].DragImage != newPets[i].DragImage) return true;
            }
            return false;
        }

        /// <summary>
        /// 切换宠物可见性
        /// </summary>
        private void TogglePetsVisibility()
        {
            _petsVisible = !_petsVisible;

            if (_petForms != null)
            {
                foreach (var pet in _petForms)
                {
                    pet.Visible = _petsVisible;
                }
            }

            _menuShowPets.Text = _petsVisible ? "隐藏宠物" : "显示宠物";
        }

        // ============================================================
        // 提醒调度器
        // ============================================================

        private void StartScheduler()
        {
            _scheduler = new ReminderScheduler(_config, _quoteManager, _petForms);

            // 弹窗提醒事件
            _scheduler.OnReminderTriggered += (isAlert, text) =>
            {
                if (isAlert)
                {
                    // 在主线程上创建弹窗
                    ShowAlertForm(text);
                }
            };

            // 宠物动作提醒事件
            _scheduler.OnPetActionReminder += () =>
            {
                DoPetActionReminder();
            };

            // 名言展示事件——每只宠物各显示不同名言
            _scheduler.OnShowQuote += (quotes) =>
            {
                ShowQuoteOnAllPets(quotes);
            };

            // 番茄钟状态更新（供其他用途使用）
            _scheduler.OnPomodoroTick += (state, elapsed, total) =>
            {
                // 状态已由 OnStatusUpdate 统一管理
            };

            // 每秒刷新托盘提示——鼠标悬停托盘图标即可看到所有倒计时
            _scheduler.OnStatusUpdate += (status) =>
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Text = status;
                }
            };

            // 启动调度器（每秒触发提醒/番茄钟/名言检查）
            _scheduler.Start();
        }

        /// <summary>
        /// 显示弹窗提醒（在主线程上），带倒计时
        /// </summary>
        private void ShowAlertForm(string text)
        {
            // 计算休息倒计时秒数
            // 番茄钟模式：使用设置的休息时长；否则使用可配置的 AlertRestSeconds（默认 30 秒）
            int restSeconds = _config.PomodoroEnabled
                ? _config.PomodoroBreakMinutes * 60
                : Math.Max(5, _config.AlertRestSeconds);

            // 如果尚未创建任何宠物，直接创建弹窗（始终在 UI 线程）
            if (_petForms == null || _petForms.Count == 0)
            {
                try
                {
                    var alertForm = new AlertForm(_config, text, restSeconds);
                    alertForm.Show();
                }
                catch (Exception ex)
                {
                    LogError("显示弹窗失败", ex);
                }
                return;
            }

            // 需要确保在 UI 线程上创建窗口
            if (_petForms[0].InvokeRequired)
            {
                _petForms[0].BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var alertForm = new AlertForm(_config, text, restSeconds);
                        alertForm.Show();
                    }
                    catch (Exception ex)
                    {
                        LogError("显示弹窗失败", ex);
                    }
                }));
            }
            else
            {
                try
                {
                    var alertForm = new AlertForm(_config, text, restSeconds);
                    alertForm.Show();
                }
                catch (Exception ex)
                {
                    LogError("显示弹窗失败", ex);
                }
            }
        }

        /// <summary>
        /// 触发所有宠物的提醒动作 + 显示动作气泡文字
        /// </summary>
        private void DoPetActionReminder()
        {
            string actionText = _config.ActionBubbleText;
            if (string.IsNullOrEmpty(actionText))
                actionText = "休息一下吧~";

            foreach (var pet in _petForms)
            {
                try
                {
                    if (pet.InvokeRequired)
                    {
                        pet.BeginInvoke(new Action(() =>
                        {
                            pet.DoReminderAction();
                            pet.ShowQuote(actionText);
                        }));
                    }
                    else
                    {
                        pet.DoReminderAction();
                        pet.ShowQuote(actionText);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 在所有宠物上显示名言——每只各显示不同内容
        /// </summary>
        private void ShowQuoteOnAllPets(string[] quotes)
        {
            if (quotes == null || quotes.Length == 0) return;
            if (_petForms == null) return;

            for (int i = 0; i < _petForms.Count; i++)
            {
                var pet = _petForms[i];
                string quote = quotes[i % quotes.Length];
                try
                {
                    if (pet.Visible)
                    {
                        if (pet.InvokeRequired)
                        {
                            var q = quote;
                            pet.BeginInvoke(new Action(() => pet.ShowQuote(q)));
                        }
                        else
                        {
                            pet.ShowQuote(quote);
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 暂停/恢复提醒
        /// </summary>
        private void ToggleRemindersPause()
        {
            _remindersPaused = !_remindersPaused;

            if (_scheduler != null)
                _scheduler.Paused = _remindersPaused;

            _menuPauseReminder.Text = _remindersPaused ? "恢复提醒" : "暂停提醒";
        }

        /// <summary>
        /// 冻结/解冻所有宠物的随机移动
        /// </summary>
        private void TogglePetMovement()
        {
            _petMovementEnabled = !_petMovementEnabled;

            foreach (var pet in _petForms)
            {
                try
                {
                    if (pet.InvokeRequired)
                        pet.BeginInvoke(new Action(() => pet.ConfigMovementEnabled = _petMovementEnabled));
                    else
                        pet.ConfigMovementEnabled = _petMovementEnabled;
                }
                catch { }
            }

            _menuFreezePet.Text = _petMovementEnabled ? "冻结宠物" : "解冻宠物";

            // 同步到配置
            _config.PetMovementEnabled = _petMovementEnabled;
            try { _config.Save(); } catch { }
        }

        // ============================================================
        // 设置窗口
        // ============================================================

        /// <summary>
        /// 打开设置窗口（如果已打开则激活）
        /// </summary>
        private void OpenSettings()
        {
            if (_currentSettingsForm != null && !_currentSettingsForm.IsDisposed)
            {
                _currentSettingsForm.Activate();
                _currentSettingsForm.BringToFront();
                return;
            }

            // 重新加载最新配置（可能被外部修改）
            _config = AppConfig.Load();

            // 捕获保存前的宠物快照，用于判断本次保存是否改变了宠物（数量/图片）。
            // 注意：设置窗口会原地修改同一个 _config 对象，保存回调里 _config 与
            // newConfig 已是同一引用，无法再用 PetConfigChanged(_config, newConfig)
            // 区分新旧，因此必须在此处先保存一份“旧”快照。
            var oldPetsSnapshot = (_config.Pets != null)
                ? _config.Pets.Select(p => new PetDefinition
                {
                    NormalImage = p.NormalImage,
                    DragImage = p.DragImage
                }).ToList()
                : null;

            _currentSettingsForm = new SettingsForm(_config);
            _currentSettingsForm.OnConfigSaved += (newConfig) =>
            {
                // 判断本次配置变更是否涉及宠物外观/数量：仅当影响宠物时才重建宠物，
                // 否则只刷新调度器，避免改个间隔就让所有宠物复位、动画/气泡被打断。
                // 注：尺寸范围、动作气泡文字变化也需要重建才能即时生效，一并纳入判断。
                bool petsAffected = PetConfigChanged(oldPetsSnapshot, newConfig.Pets)
                    || _config.PetMinSize != newConfig.PetMinSize
                    || _config.PetMaxSize != newConfig.PetMaxSize
                    || _config.ActionBubbleText != newConfig.ActionBubbleText;

                _config = newConfig;

                // 刷新托盘图标
                LoadTrayIcon();

                // 同步宠物移动状态
                _petMovementEnabled = _config.PetMovementEnabled;
                _menuFreezePet.Text = _petMovementEnabled ? "冻结宠物" : "解冻宠物";

                if (petsAffected)
                {
                    // 宠物相关配置变化：重建宠物（销毁并重新创建）
                    CreatePets();
                }
                else
                {
                    // 仅提醒相关配置变化：保持现有宠物，仅同步移动开关
                    if (_petForms != null)
                    {
                        foreach (var pet in _petForms)
                        {
                            try { pet.MovementEnabled = _config.PetMovementEnabled; } catch { }
                        }
                    }
                }

                // 更新调度器（传入新宠物列表，重置所有计时器）
                if (_scheduler != null)
                    _scheduler.ReloadConfig(_config, _petForms);
            };

            _currentSettingsForm.FormClosed += (s, e) =>
            {
                _currentSettingsForm.Dispose();
                _currentSettingsForm = null;
            };

            _currentSettingsForm.Show();
        }

        // ============================================================
        // 退出
        // ============================================================

        /// <summary>
        /// 退出应用程序——释放所有资源
        /// </summary>
        private void ExitApplication()
        {
            // 关闭设置窗口
            if (_currentSettingsForm != null && !_currentSettingsForm.IsDisposed)
            {
                _currentSettingsForm.Close();
                _currentSettingsForm.Dispose();
            }

            // 停止并释放调度器
            if (_scheduler != null)
            {
                _scheduler.Dispose();
                _scheduler = null;
            }

            // 销毁所有宠物窗口
            DestroyAllPets();

            // 释放托盘图标
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            // 退出消息循环
            Application.Exit();
        }

        public void Dispose()
        {
            ExitApplication();
        }

        /// <summary>
        /// 日志文件大小上限（字节）。超过后将其归档为 error.old.log，避免无限增长。
        /// </summary>
        private const long MaxLogSizeBytes = 1024 * 1024; // 1MB

        /// <summary>
        /// 统一的错误日志记录（写入应用目录下的 error.log），带滚动归档。
        /// </summary>
        private void LogError(string message, Exception ex)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");

                // 日志滚动：超过上限时，将当前日志归档为 error.old.log（只保留一份历史）
                if (File.Exists(logPath))
                {
                    try
                    {
                        var fi = new FileInfo(logPath);
                        if (fi.Length > MaxLogSizeBytes)
                        {
                            string oldPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.old.log");
                            if (File.Exists(oldPath)) File.Delete(oldPath);
                            File.Move(logPath, oldPath);
                        }
                    }
                    catch { /* 归档失败不影响写入新日志 */ }
                }

                string line = string.Format("[{0}] {1}: {2}\n{3}",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    message,
                    ex != null ? ex.Message : "",
                    ex != null ? ex.StackTrace : "");
                File.AppendAllText(logPath, line + "\n");
            }
            catch { /* 日志写入失败不应影响主流程 */ }
        }
    }
}
