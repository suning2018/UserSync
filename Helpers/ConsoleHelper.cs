namespace UserSync.Helpers
{
    /// <summary>
    /// 控制台辅助类
    /// </summary>
    public static class ConsoleHelper
    {
        /// <summary>
        /// 写入带时间戳的消息
        /// </summary>
        public static void WriteLineWithTimestamp(string message)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }

        /// <summary>
        /// 写入分隔线
        /// </summary>
        public static void WriteSeparator(int length = 50)
        {
            Console.WriteLine(new string('=', length));
        }

        /// <summary>
        /// 写入成功消息
        /// </summary>
        public static void WriteSuccess(string message)
        {
            Console.WriteLine($"✓ {message}");
        }

        /// <summary>
        /// 写入错误消息
        /// </summary>
        public static void WriteError(string message)
        {
            Console.WriteLine($"✗ {message}");
        }

        /// <summary>
        /// 等待退出
        /// </summary>
        public static void WaitForExit()
        {
            if (Console.IsInputRedirected || !Environment.UserInteractive)
            {
                Console.WriteLine("程序将在3秒后自动退出...");
                Task.Delay(3000).Wait();
            }
            else
            {
                Console.WriteLine("按任意键退出...");
                try
                {
                    Console.ReadKey();
                }
                catch
                {
                    Task.Delay(3000).Wait();
                }
            }
        }
    }
}
