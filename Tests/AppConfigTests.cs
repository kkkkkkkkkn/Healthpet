using System;
using System.IO;

namespace HealthyPet.Tests
{
    public class AppConfigTests
    {
        public static void RunAll()
        {
            TestFramework.Run("AppConfig", () =>
            {
                TestSaveLoadRoundTrip();
                TestDefaults();
                TestBackwardCompatibleNormalImage();
                TestIsValid();
                TestGetFullPath();
            });
        }

        private static string TempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "hp_cfg_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void TestSaveLoadRoundTrip()
        {
            string dir = TempDir();
            try
            {
                var cfg = new AppConfig(dir);
                cfg.ReminderMode = 1;
                cfg.CustomReminderText = "测试提醒";
                cfg.PomodoroEnabled = true;
                cfg.PomodoroFocusMinutes = 25;
                cfg.PomodoroBreakMinutes = 5;
                cfg.PetMinSize = 80;
                cfg.PetMaxSize = 200;
                cfg.PetMovementEnabled = false;
                cfg.AlertSizeMode = 2;
                cfg.QuoteIntervalMinutes = 45;
                cfg.Save();

                var loaded = new AppConfig(dir).LoadFromDir();
                TestFramework.AssertEqual(1, loaded.ReminderMode, "ReminderMode 往返一致");
                TestFramework.AssertEqual("测试提醒", loaded.CustomReminderText, "CustomReminderText 往返一致");
                TestFramework.AssertTrue(loaded.PomodoroEnabled, "PomodoroEnabled 往返一致");
                TestFramework.AssertEqual(25, loaded.PomodoroFocusMinutes, "FocusMinutes 往返一致");
                TestFramework.AssertEqual(80, loaded.PetMinSize, "PetMinSize 往返一致");
                TestFramework.AssertTrue(loaded.PetMovementEnabled == false, "PetMovementEnabled 往返一致");
                TestFramework.AssertEqual(45, loaded.QuoteIntervalMinutes, "QuoteInterval 往返一致");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        private static void TestDefaults()
        {
            var cfg = new AppConfig();
            TestFramework.AssertEqual(0, cfg.ReminderMode, "默认 ReminderMode=0");
            TestFramework.AssertTrue(cfg.PomodoroEnabled == false, "默认 番茄钟关闭");
            TestFramework.AssertEqual(20, cfg.PomodoroFocusMinutes, "默认 专注20分");
            TestFramework.AssertEqual(2, cfg.PomodoroBreakMinutes, "默认 休息2分");
            TestFramework.AssertEqual(80, cfg.PetMinSize, "默认 最小尺寸80");
            TestFramework.AssertEqual(120, cfg.PetMaxSize, "默认 最大尺寸120");
            TestFramework.AssertEqual(20, cfg.QuoteIntervalMinutes, "默认 名言间隔20分");
            TestFramework.AssertTrue(cfg.Pets != null && cfg.Pets.Count == 1, "默认 1只宠物");
            TestFramework.AssertEqual(0, cfg.AlertSizeMode, "默认 弹窗尺寸模式0");
            TestFramework.AssertEqual("Data\\Pop-up image.png", cfg.AlertBackgroundImage, "默认 弹窗背景图路径");
            TestFramework.AssertEqual("Data\\pet_normal.png", cfg.PetNormalImage, "默认 正常图路径");
            TestFramework.AssertEqual("Data\\pet_drag.png", cfg.PetDragImage, "默认 拖拽图路径");
        }

        private static void TestBackwardCompatibleNormalImage()
        {
            // 只设置全局 PetNormalImage，Pets 为空时应自动生成默认 Pets
            string dir = TempDir();
            try
            {
                var cfg = new AppConfig(dir);
                cfg.Pets = new System.Collections.Generic.List<PetDefinition>();
                cfg.PetNormalImage = "Data\\mycat.png";
                cfg.Save();

                var loaded = new AppConfig(dir).LoadFromDir();
                TestFramework.AssertTrue(loaded.Pets != null && loaded.Pets.Count >= 1, "旧格式生成 Pets");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        private static void TestIsValid()
        {
            var cfg = new AppConfig();
            TestFramework.AssertTrue(cfg.IsValid(), "默认配置有效");

            cfg.PetMinSize = 999;
            cfg.PetMaxSize = 10;
            TestFramework.AssertFalse(cfg.IsValid(), "Min>Max 无效");

            cfg.PetMinSize = 50;
            cfg.PetMaxSize = 50;
            TestFramework.AssertTrue(cfg.IsValid(), "Min==Max 有效");

            cfg.PetMinSize = 80;
            cfg.PetMaxSize = 120;
            cfg.PomodoroEnabled = true;
            cfg.PomodoroFocusMinutes = 0;
            TestFramework.AssertFalse(cfg.IsValid(), "专注时长<=0 无效");
        }

        private static void TestGetFullPath()
        {
            var cfg = new AppConfig();
            string full = cfg.GetFullPath("Data\\a.png");
            TestFramework.AssertTrue(full.EndsWith("Data\\a.png"), "GetFullPath 拼接相对路径");
            TestFramework.AssertEqual("", cfg.GetFullPath(""), "空路径返回空");
        }
    }
}
