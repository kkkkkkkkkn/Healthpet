using System;
using System.Collections.Generic;
using System.IO;

namespace HealthyPet
{
    /// <summary>
    /// 名言管理器——负责从文件加载名言并提供随机获取
    /// </summary>
    public class QuoteManager
    {
        private List<string> _quotes;
        private Random _random;

        public QuoteManager()
        {
            _quotes = new List<string>();
            _random = new Random();
        }

        /// <summary>
        /// 从指定文件加载名言（每行一条）
        /// </summary>
        public void Load(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string[] lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
                    _quotes.Clear();
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("//"))
                        {
                            _quotes.Add(trimmed);
                        }
                    }
                    System.Diagnostics.Debug.WriteLine("已加载 " + _quotes.Count + " 条名言");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("加载名言失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 获取一条随机名言
        /// </summary>
        public string GetRandomQuote()
        {
            if (_quotes.Count == 0)
                return "你已经很棒啦，休息一下吧 (╭￣3￣)╭♡";
            return _quotes[_random.Next(_quotes.Count)];
        }

        /// <summary>
        /// 获取 N 条不重复的随机名言（用于多只宠物各显示不同内容）
        /// 使用 Fisher-Yates 部分洗牌，保证每条都不一样
        /// </summary>
        public string[] GetRandomQuotes(int count)
        {
            if (_quotes.Count == 0)
                return new[] { "你已经很棒啦，休息一下吧 (╭￣3￣)╭♡" };

            int n = Math.Min(count, _quotes.Count);
            var indices = new int[_quotes.Count];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = i;

            // Fisher-Yates 只洗前 n 个
            for (int i = 0; i < n; i++)
            {
                int j = _random.Next(i, indices.Length);
                int temp = indices[i];
                indices[i] = indices[j];
                indices[j] = temp;
            }

            var result = new string[n];
            for (int i = 0; i < n; i++)
                result[i] = _quotes[indices[i]];

            return result;
        }

        /// <summary>名言总数</summary>
        public int Count
        {
            get { return _quotes.Count; }
        }
    }
}
