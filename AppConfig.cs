using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace HealthyPet
{
    /// <summary>
    /// 单个桌宠定义——包含自定义图片路径
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    public class PetDefinition
    {
        [System.Runtime.Serialization.DataMember(Order = 1)]
        public string NormalImage { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 2)]
        public string DragImage { get; set; }

        public PetDefinition()
        {
            NormalImage = AppConfig.DefaultPetNormalImage;
            DragImage = AppConfig.DefaultPetDragImage;
        }

        public override string ToString()
        {
            string name = Path.GetFileNameWithoutExtension(NormalImage ?? "猫");
            return string.IsNullOrEmpty(name) ? "未命名桌宠" : name;
        }
    }

    /// <summary>
    /// 应用程序配置模型——所有设置保存在 config.json 中
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    public class AppConfig
    {
        /// <summary>配置文件所在目录，默认应用根目录。可用于测试隔离。</summary>
        private string _configDirectory;

        /// <summary>资源路径常量，集中管理避免散落的魔术字符串。</summary>
        public const string DefaultPetNormalImage = "Data\\pet_normal.png";
        public const string DefaultPetDragImage = "Data\\pet_drag.png";
        public const string DefaultAlertBackgroundImage = "Data\\Pop-up image.png";
        public const string DefaultTrayIcon = "Data\\tray.ico";

        public AppConfig()
        {
            _configDirectory = null;
            InitDefaults();
        }

        /// <summary>测试用：指定配置目录（避免污染应用配置）</summary>
        internal AppConfig(string configDirectory)
        {
            _configDirectory = configDirectory;
            InitDefaults();
        }

        /// <summary>统一初始化所有字段的默认值，避免两个构造函数重复且容易漏改。</summary>
        private void InitDefaults()
        {
            ReminderMode = 0;
            CustomReminderText = "该休息一下啦！";
            PomodoroEnabled = false;
            PomodoroFocusMinutes = 20;
            PomodoroBreakMinutes = 2;
            PomodoroFocusEndText = "专注时间结束，休息一下吧！";
            PomodoroBreakEndText = "休息结束，继续加油！";
            FixedIntervals = new List<int> { 45 };
            AlertRestSeconds = 30;
            Pets = new List<PetDefinition> { new PetDefinition() };
            PetMinSize = 80;
            PetMaxSize = 120;
            AutoStart = false;
            TrayIconPath = DefaultTrayIcon;
            QuoteIntervalMinutes = 20;
            AlertSizeMode = 0;
            AlertBackgroundImage = DefaultAlertBackgroundImage;
            ActionBubbleText = "休息一下吧~";
            PetMovementEnabled = true;
            PetNormalImage = DefaultPetNormalImage;
            PetDragImage = DefaultPetDragImage;
        }

        // ===== 提醒设置 =====
        [DataMember(Order = 1)]
        public int ReminderMode { get; set; }

        [DataMember(Order = 2)]
        public string CustomReminderText { get; set; }

        [DataMember(Order = 3)]
        public bool PomodoroEnabled { get; set; }

        [DataMember(Order = 4)]
        public int PomodoroFocusMinutes { get; set; }

        [DataMember(Order = 5)]
        public int PomodoroBreakMinutes { get; set; }

        [DataMember(Order = 6)]
        public string PomodoroFocusEndText { get; set; }

        [DataMember(Order = 7)]
        public string PomodoroBreakEndText { get; set; }

        [DataMember(Order = 8)]
        public List<int> FixedIntervals { get; set; }

        /// <summary>非番茄钟模式下休息弹窗的时长（秒），默认 30，可在设置中调整</summary>
        [DataMember(Order = 9)]
        public int AlertRestSeconds { get; set; }

        /// <summary>宠物动作提醒时的气泡文字</summary>
        [DataMember(Order = 18)]
        public string ActionBubbleText { get; set; }

        // ===== 桌宠设置 =====
        /// <summary>桌宠列表——每个元素定义一个独立的桌宠</summary>
        [DataMember(Order = 19)]
        public List<PetDefinition> Pets { get; set; }

        [DataMember(Order = 10)]
        public int PetMinSize { get; set; }

        [DataMember(Order = 11)]
        public int PetMaxSize { get; set; }

        /// <summary>全局默认正常图片（向后兼容）</summary>
        [DataMember(Order = 12)]
        public string PetNormalImage { get; set; }

        /// <summary>全局默认拖拽图片（向后兼容）</summary>
        [DataMember(Order = 13)]
        public string PetDragImage { get; set; }

        // ===== 通用设置 =====
        [DataMember(Order = 14)]
        public bool AutoStart { get; set; }

        [DataMember(Order = 15)]
        public string TrayIconPath { get; set; }

        [DataMember(Order = 16)]
        public int QuoteIntervalMinutes { get; set; }

        [DataMember(Order = 17)]
        public int AlertSizeMode { get; set; }

        /// <summary>弹窗背景图片路径</summary>
        [DataMember(Order = 20)]
        public string AlertBackgroundImage { get; set; }

        /// <summary>宠物是否随机移动</summary>
        [DataMember(Order = 21)]
        public bool PetMovementEnabled { get; set; }

        // ============================================================
        // 配置持久化
        // ============================================================

        private string ConfigPath
        {
            get
            {
                string dir = _configDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(dir, "config.json");
            }
        }

        /// <summary>校验配置合法性（供设置界面保存前校验）</summary>
        public bool IsValid()
        {
            if (PetMinSize < 20 || PetMaxSize > 200) return false;
            if (PetMinSize > PetMaxSize) return false;
            if (PomodoroEnabled)
            {
                if (PomodoroFocusMinutes <= 0 || PomodoroBreakMinutes <= 0) return false;
            }
            if (QuoteIntervalMinutes <= 0) return false;
            return true;
        }

        /// <summary>从指定目录加载配置（测试用）</summary>
        internal AppConfig LoadFromDir()
        {
            return LoadFrom(ConfigPath);
        }

        private static AppConfig LoadFrom(string configPath)
        {
            try
            {
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath, Encoding.UTF8);
                    using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(AppConfig));
                        var config = (AppConfig)serializer.ReadObject(ms);

                        // 确保关键字段不为 null
                        if (config.FixedIntervals == null)
                            config.FixedIntervals = new List<int> { 45 };
                        if (config.Pets == null || config.Pets.Count == 0)
                            config.Pets = new List<PetDefinition> { new PetDefinition() };
                        if (string.IsNullOrEmpty(config.CustomReminderText))
                            config.CustomReminderText = "该休息一下啦！";
                        if (string.IsNullOrEmpty(config.ActionBubbleText))
                            config.ActionBubbleText = "休息一下吧~";
                        if (string.IsNullOrEmpty(config.AlertBackgroundImage))
                            config.AlertBackgroundImage = "Data\\Pop-up image.png";
                        if (config.PetMinSize < 20) config.PetMinSize = 20;
                        if (config.PetMaxSize > 200) config.PetMaxSize = 200;
                        if (config.PetMinSize > config.PetMaxSize)
                            config.PetMinSize = config.PetMaxSize;

                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("加载配置失败: " + ex.Message);
            }

            var defaultConfig = new AppConfig();
            defaultConfig.Save();
            return defaultConfig;
        }

        public static AppConfig Load()
        {
            try
            {
                string configPath = new AppConfig().ConfigPath;
                if (File.Exists(configPath))
                {
                    return LoadFrom(configPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("加载配置失败: " + ex.Message);
            }

            var defaultConfig = new AppConfig();
            defaultConfig.Save();
            return defaultConfig;
        }

        public void Save()
        {
            var serializer = new DataContractJsonSerializer(typeof(AppConfig));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, this);
                string json = Encoding.UTF8.GetString(ms.ToArray());
                json = FormatJson(json);
                File.WriteAllText(ConfigPath, json, Encoding.UTF8);
            }
        }

        private static string FormatJson(string json)
        {
            try
            {
                var sb = new StringBuilder();
                int indent = 0;
                bool inString = false;
                bool escape = false;
                foreach (char c in json)
                {
                    if (escape) { sb.Append(c); escape = false; continue; }
                    if (c == '\\') { sb.Append(c); escape = true; continue; }
                    if (c == '"') { inString = !inString; sb.Append(c); continue; }
                    if (inString) { sb.Append(c); continue; }
                    switch (c)
                    {
                        case '{': case '[':
                            sb.Append(c); sb.AppendLine(); indent++;
                            sb.Append(new string(' ', indent * 2)); break;
                        case '}': case ']':
                            sb.AppendLine(); indent--;
                            sb.Append(new string(' ', indent * 2)); sb.Append(c); break;
                        case ',':
                            sb.Append(c); sb.AppendLine();
                            sb.Append(new string(' ', indent * 2)); break;
                        case ':': sb.Append(c); sb.Append(' '); break;
                        default: if (!char.IsWhiteSpace(c)) sb.Append(c); break;
                    }
                }
                return sb.ToString();
            }
            catch
            {
                // 美化失败时退回原始 JSON，保证配置始终可写、可读
                return json;
            }
        }

        public string GetFullPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return "";
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
        }
    }
}
