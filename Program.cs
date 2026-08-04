using System;
using System.IO;
using System.Windows.Forms;

namespace HealthyPet
{
    static class Program
    {
        /// <summary>
        /// 应用程序主入口点
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 捕获 UI 线程未处理异常，避免（如弹窗/气泡/定时器等）异常导致消息循环直接退出、
            // 整个程序无报错消失、托盘图标丢失。
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                LogFatal("UI线程未处理异常", e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogFatal("非UI线程未处理异常", e.ExceptionObject as Exception);
            };

            // 确保只运行一个实例，防止重复启动
            bool createdNew;
            using (var mutex = new System.Threading.Mutex(true, "HealthyPet_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("健康守护桌宠已在运行中！\n请查看系统托盘图标。",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 创建主应用控制器（负责托盘图标、宠物管理和定时调度）
                using (var mainApp = new MainApp())
                {
                    mainApp.Initialize();

                    // 进入 Windows 消息循环（由托盘图标保持运行）
                    Application.Run();
                }
            }
        }

        /// <summary>
        /// 记录致命异常到 error.log（与 MainApp.LogError 一致的落盘策略），供排查崩溃原因。
        /// </summary>
        private static void LogFatal(string message, Exception ex)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                string line = string.Format("[{0}] {1}: {2}\n{3}\n",
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
