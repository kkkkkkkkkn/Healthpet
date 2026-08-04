using System;
using System.Collections.Generic;

namespace HealthyPet.Tests
{
    /// <summary>
    /// 轻量级单元测试框架（零依赖，仅需 .NET Framework）
    /// 提供断言与简单的测试运行统计
    /// </summary>
    public static class TestFramework
    {
        private static int _passed;
        private static int _failed;
        private static List<string> _failures = new List<string>();

        public static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                Fail(message);
            else
                _passed++;
        }

        public static void AssertFalse(bool condition, string message)
        {
            AssertTrue(!condition, message);
        }

        public static void AssertEqual<T>(T expected, T actual, string message)
        {
            bool eq = Equals(expected, actual);
            if (!eq)
                Fail(string.Format("{0} | 期望: {1}, 实际: {2}", message, expected, actual));
            else
                _passed++;
        }

        public static void AssertNotNull(object obj, string message)
        {
            if (obj == null)
                Fail(message);
            else
                _passed++;
        }

        public static void AssertNull(object obj, string message)
        {
            if (obj != null)
                Fail(message);
            else
                _passed++;
        }

        private static void Fail(string message)
        {
            _failed++;
            _failures.Add(message);
            Console.WriteLine("  [FAIL] " + message);
        }

        public static void Run(string testClassName, Action runTests)
        {
            Console.WriteLine("--- " + testClassName + " ---");
            runTests();
        }

        public static void Report()
        {
            Console.WriteLine();
            Console.WriteLine("=====================================");
            Console.WriteLine(string.Format("通过: {0}  失败: {1}", _passed, _failed));
            if (_failed > 0)
            {
                Console.WriteLine("失败明细:");
                foreach (var f in _failures)
                    Console.WriteLine("  - " + f);
            }
            Console.WriteLine("=====================================");
            Environment.ExitCode = _failed > 0 ? 1 : 0;
        }
    }
}
