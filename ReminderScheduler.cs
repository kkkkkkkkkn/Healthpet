using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace HealthyPet
{
    /// <summary>
    /// 提醒调度器——管理定时任务、番茄钟循环，触发提醒
    /// </summary>
    public class ReminderScheduler : IDisposable
    {
        private Timer _timer;                   // 主计时器（每秒触发）
        private AppConfig _config;
        private QuoteManager _quoteManager;
        private List<PetForm> _petForms;

        // 固定间隔任务的已过秒数
        private List<int> _intervalElapsedSeconds;

        // 番茄钟状态
        private enum PomodoroState { Idle, Focusing, OnBreak }
        private PomodoroState _pomodoroState = PomodoroState.Idle;
        private int _pomodoroElapsedSeconds = 0;

        // 名言展示计时器
        private int _quoteElapsedSeconds = 0;

        /// <summary>提醒触发事件</summary>
        public event Action<bool, string> OnReminderTriggered; // bool=是否弹窗, string=提醒文字

        /// <summary>番茄钟状态变更事件（用于UI更新）</summary>
        public event Action<string, int, int> OnPomodoroTick; // state描述, 已过秒, 总秒

        /// <summary>名言展示事件——每只宠物一条，内容各不相同</summary>
        public event Action<string[]> OnShowQuote;

        /// <summary>宠物动作提醒事件</summary>
        public event Action OnPetActionReminder;

        /// <summary>状态更新事件（每秒触发，用于托盘提示显示倒计时）</summary>
        public event Action<string> OnStatusUpdate;

        public ReminderScheduler(AppConfig config, QuoteManager quoteManager, List<PetForm> petForms)
        {
            _config = config;
            _quoteManager = quoteManager;
            _petForms = petForms;
            Paused = false;

            _intervalElapsedSeconds = new List<int>();
            ResetIntervalTimers();

            // 番茄钟启用时立即进入专注状态（首次 Tick 前也应有正确状态）
            _pomodoroState = _config != null && _config.PomodoroEnabled
                ? PomodoroState.Focusing
                : PomodoroState.Idle;

            // 计时器在 Start() 中启动，便于测试时跳过真实计时
        }

        /// <summary>启动内部计时器（每秒触发）。测试可通过 TickForTest 手动驱动。</summary>
        public void Start()
        {
            if (_timer == null)
            {
                _timer = new Timer();
                _timer.Interval = 1000; // 每秒触发
                _timer.Tick += MainTimer_Tick;
            }
            _timer.Start();
        }

        /// <summary>
        /// 重新加载配置并重置全部计时状态（保证新设置立即生效）
        /// </summary>
        public void ReloadConfig(AppConfig config, List<PetForm> newPetForms)
        {
            _config = config;
            _petForms = newPetForms;

            // 重置所有累积时间，确保新设置立即生效
            _quoteElapsedSeconds = 0;
            _pomodoroElapsedSeconds = 0;
            ResetIntervalTimers();

            if (!config.PomodoroEnabled)
            {
                _pomodoroState = PomodoroState.Idle;
            }
        }

        /// <summary>
        /// 暂停/恢复提醒
        /// </summary>
        public bool Paused { get; set; }

        /// <summary>
        /// 番茄钟是否正在运行
        /// </summary>
        public bool IsPomodoroRunning
        {
            get { return _pomodoroState != PomodoroState.Idle; }
        }

        /// <summary>
        /// 主计时器触发——每秒检查所有任务
        /// </summary>
        private void MainTimer_Tick(object sender, EventArgs e)
        {
            TickOnce();
        }

        /// <summary>执行一次调度循环（1秒的逻辑）。供计时器与单元测试共用。</summary>
        internal void TickOnce()
        {
            if (Paused) return;

            // 1. 检查固定间隔任务
            CheckFixedIntervals();

            // 2. 检查番茄钟
            if (_config.PomodoroEnabled)
            {
                CheckPomodoro();
            }

            // 3. 检查名言展示
            CheckQuoteDisplay();

            // 4. 每秒更新状态（托盘提示用）
            if (OnStatusUpdate != null)
                OnStatusUpdate(GetStatusText());
        }

        /// <summary>测试辅助方法：手动推进一个调度周期</summary>
        public void TickForTest()
        {
            TickOnce();
        }

        /// <summary>
        /// 获取当前调度状态文本（用于托盘提示显示）
        /// </summary>
        public string GetStatusText()
        {
            if (Paused)
                return "健康守护桌宠 - 已暂停";

            var parts = new List<string>();
            parts.Add("健康守护桌宠");

            // 名言倒计时
            if (_config.QuoteIntervalMinutes > 0)
            {
                int remain = Math.Max(0, _config.QuoteIntervalMinutes * 60 - _quoteElapsedSeconds);
                parts.Add(string.Format("名言:{0}:{1:D2}", remain / 60, remain % 60));
            }

            // 番茄钟
            if (_config.PomodoroEnabled)
            {
                int total = 0;
                string label = "";
                if (_pomodoroState == PomodoroState.Focusing)
                {
                    total = _config.PomodoroFocusMinutes * 60;
                    label = "专注";
                }
                else if (_pomodoroState == PomodoroState.OnBreak)
                {
                    total = _config.PomodoroBreakMinutes * 60;
                    label = "休息";
                }
                else
                {
                    // Idle：番茄钟已启用但尚未开始（如刚重置）
                    total = _config.PomodoroFocusMinutes * 60;
                    label = "待开始";
                }
                int remain = Math.Max(0, total - _pomodoroElapsedSeconds);
                parts.Add(string.Format("{0}:{1}:{2:D2}", label, remain / 60, remain % 60));
            }

            // 固定间隔
            bool singleFixed = _config.FixedIntervals.Count == 1;
            for (int i = 0; i < _config.FixedIntervals.Count && i < _intervalElapsedSeconds.Count; i++)
            {
                int total = _config.FixedIntervals[i] * 60;
                int remain = Math.Max(0, total - _intervalElapsedSeconds[i]);
                // 单个间隔时不显示编号（避免"定时1"的尴尬措辞）；多个间隔时保留编号以区分
                string prefix = singleFixed ? "间隔" : string.Format("定时{0}", i + 1);
                parts.Add(string.Format("{0}:{1}:{2:D2}", prefix, remain / 60, remain % 60));
            }

            return string.Join(" | ", parts);
        }

        /// <summary>
        /// 检查固定间隔任务是否到期
        /// </summary>
        private void CheckFixedIntervals()
        {
            if (_config.FixedIntervals == null || _config.FixedIntervals.Count == 0)
                return;

            for (int i = 0; i < _config.FixedIntervals.Count; i++)
            {
                if (i >= _intervalElapsedSeconds.Count)
                    break;

                int intervalMinutes = _config.FixedIntervals[i];
                if (intervalMinutes <= 0) continue;

                _intervalElapsedSeconds[i]++;

                // 到达间隔时间
                if (_intervalElapsedSeconds[i] >= intervalMinutes * 60)
                {
                    _intervalElapsedSeconds[i] = 0; // 重置计数
                    TriggerReminder(_config.CustomReminderText);
                }
            }
        }

        /// <summary>
        /// 番茄钟状态机
        /// </summary>
        private void CheckPomodoro()
        {
            _pomodoroElapsedSeconds++;

            switch (_pomodoroState)
            {
                case PomodoroState.Idle:
                    // 自动开始专注
                    _pomodoroState = PomodoroState.Focusing;
                    _pomodoroElapsedSeconds = 0;
                    break;

                case PomodoroState.Focusing:
                    // 通知UI更新
                    if (OnPomodoroTick != null)
                        OnPomodoroTick("专注中",
                            _pomodoroElapsedSeconds, _config.PomodoroFocusMinutes * 60);

                    // 专注时间结束
                    if (_pomodoroElapsedSeconds >= _config.PomodoroFocusMinutes * 60)
                    {
                        _pomodoroElapsedSeconds = 0;
                        _pomodoroState = PomodoroState.OnBreak;
                        TriggerReminder(_config.PomodoroFocusEndText);
                    }
                    break;

                case PomodoroState.OnBreak:
                    // 通知UI更新
                    if (OnPomodoroTick != null)
                        OnPomodoroTick("休息中",
                            _pomodoroElapsedSeconds, _config.PomodoroBreakMinutes * 60);

                    // 休息时间结束
                    if (_pomodoroElapsedSeconds >= _config.PomodoroBreakMinutes * 60)
                    {
                        _pomodoroElapsedSeconds = 0;
                        _pomodoroState = PomodoroState.Focusing; // 回到专注
                        TriggerReminder(_config.PomodoroBreakEndText);
                    }
                    break;
            }
        }

        /// <summary>
        /// 检查是否该展示名言——每只宠物各取一条不重复的名言
        /// </summary>
        private void CheckQuoteDisplay()
        {
            if (_config.QuoteIntervalMinutes <= 0) return;

            _quoteElapsedSeconds++;

            if (_quoteElapsedSeconds >= _config.QuoteIntervalMinutes * 60)
            {
                _quoteElapsedSeconds = 0;
                int petCount = _petForms != null ? _petForms.Count : 1;
                string[] quotes = _quoteManager.GetRandomQuotes(petCount);
                if (OnShowQuote != null)
                    OnShowQuote(quotes);
            }
        }

        /// <summary>
        /// 触发提醒
        /// </summary>
        private void TriggerReminder(string text)
        {
            if (_config.ReminderMode == 0)
            {
                // 弹窗提醒
                if (OnReminderTriggered != null)
                    OnReminderTriggered(true, text);
            }
            else
            {
                // 宠物动作提醒
                if (OnPetActionReminder != null)
                    OnPetActionReminder();
            }
        }

        /// <summary>
        /// 重置固定间隔计时器
        /// </summary>
        private void ResetIntervalTimers()
        {
            _intervalElapsedSeconds.Clear();
            if (_config.FixedIntervals != null)
            {
                for (int i = 0; i < _config.FixedIntervals.Count; i++)
                {
                    _intervalElapsedSeconds.Add(0);
                }
            }
        }

        /// <summary>
        /// 手动重置番茄钟
        /// </summary>
        public void ResetPomodoro()
        {
            _pomodoroState = PomodoroState.Idle;
            _pomodoroElapsedSeconds = 0;
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }
    }
}
