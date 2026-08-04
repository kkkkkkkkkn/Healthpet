using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HealthyPet.Tests
{
    public class ReminderSchedulerTests
    {
        public static void RunAll()
        {
            TestFramework.Run("ReminderScheduler", () =>
            {
                TestStatusFocusing();
                TestStatusBreak();
                TestStatusIdleNoCrash();
                TestPause();
                TestPomodoroStateTransition();
            });
        }

        private static AppConfig MakeConfig()
        {
            string dir = Path.Combine(Path.GetTempPath(), "hp_sched_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var cfg = new AppConfig(dir);
            cfg.PomodoroEnabled = true;
            cfg.PomodoroFocusMinutes = 1;
            cfg.PomodoroBreakMinutes = 1;
            return cfg;
        }

        private static ReminderScheduler MakeScheduler(AppConfig cfg)
        {
            var sched = new ReminderScheduler(cfg, null, new List<PetForm>());
            return sched;
        }

        private static void TestStatusFocusing()
        {
            var cfg = MakeConfig();
            using (var sched = MakeScheduler(cfg))
            {
                // 启动后应处于 Focusing 状态
                string status = sched.GetStatusText();
                TestFramework.AssertTrue(status.Contains("专注"), "聚焦状态显示'专注'");
            }
        }

        private static void TestStatusBreak()
        {
            var cfg = MakeConfig();
            using (var sched = MakeScheduler(cfg))
            {
                // 推进超过专注时长进入休息
                // 1分钟 = 60秒，Tick 60次
                for (int i = 0; i < 65; i++)
                    sched.TickForTest();
                string status = sched.GetStatusText();
                TestFramework.AssertTrue(status.Contains("休息"), "休息状态显示'休息'");
            }
        }

        private static void TestStatusIdleNoCrash()
        {
            // 启用番茄钟后构造即进入 Focusing；GetStatusText 不应崩溃且包含'专注'
            var cfg = MakeConfig();
            using (var sched = MakeScheduler(cfg))
            {
                string status = sched.GetStatusText();
                TestFramework.AssertNotNull(status, "GetStatusText 返回非空");
                TestFramework.AssertFalse(string.IsNullOrWhiteSpace(status), "GetStatusText 不返回空白");
                TestFramework.AssertTrue(status.Contains("专注"), "启用番茄钟后显示'专注'");
            }
        }

        private static void TestPause()
        {
            var cfg = MakeConfig();
            using (var sched = MakeScheduler(cfg))
            {
                sched.Paused = true;
                // 暂停时 Tick 不累计（通过多次 tick 后状态文本不前进来验证不崩溃即可）
                for (int i = 0; i < 10; i++)
                    sched.TickForTest();
                string status = sched.GetStatusText();
                TestFramework.AssertNotNull(status, "暂停状态 GetStatusText 不崩溃");
                sched.Paused = false;
            }
        }

        private static void TestPomodoroStateTransition()
        {
            var cfg = MakeConfig();
            using (var sched = MakeScheduler(cfg))
            {
                // 完整一个番茄钟周期：聚焦 -> 休息 -> 聚焦
                for (int i = 0; i < 65; i++) sched.TickForTest(); // -> 休息
                string afterFocus = sched.GetStatusText();
                for (int i = 0; i < 65; i++) sched.TickForTest(); // -> 下一聚焦
                string afterBreak = sched.GetStatusText();
                TestFramework.AssertTrue(afterFocus.Contains("休息"), "第一个周期进入休息");
                TestFramework.AssertTrue(afterBreak.Contains("专注"), "休息后回到专注");
            }
        }
    }
}
