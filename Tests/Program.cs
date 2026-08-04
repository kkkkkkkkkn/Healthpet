using System;

namespace HealthyPet.Tests
{
    class Program
    {
        [STAThread]
        static void Main()
        {
            Console.WriteLine("=== HealthyPet 单元测试 ===");
            Console.WriteLine();

            AppConfigTests.RunAll();
            ReminderSchedulerTests.RunAll();
            ImageHelperTests.RunAll();

            TestFramework.Report();

            Console.WriteLine("按任意键退出...");
            try { Console.ReadKey(); } catch { }
        }
    }
}
