using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using PersonnelSync.Services;

namespace PersonnelSync
{
    class Program
    {
        private static DatabaseLogService? _databaseLogService;
        private static IConfiguration? _configuration;
        private static ILoggerFactory? _loggerFactory;
        private static bool _isRunning = true;
        private static readonly SemaphoreSlim _processingLock = new SemaphoreSlim(1, 1);

        static async Task Main(string[] args)
        {
            // 注册全局异常处理
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Console.WriteLine($"未处理的异常: {e.ExceptionObject}");
                Serilog.Log.Fatal((Exception)e.ExceptionObject, "未处理的异常导致程序退出");
                Serilog.Log.CloseAndFlush();
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Console.WriteLine($"未观察到的任务异常: {e.Exception}");
                Serilog.Log.Error(e.Exception, "未观察到的任务异常");
                e.SetObserved();
            };

            try
            {
                // 加载配置
                _configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                // 初始化日志
                InitializeLogging(_configuration);
                
                // 创建日志工厂
                _loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConfiguration(_configuration.GetSection("Logging")).AddSerilog();
                });

                // 初始化数据库日志服务
                var enableDatabaseLog = bool.Parse(_configuration["LogSettings:EnableDatabaseLog"] ?? "true");
                if (enableDatabaseLog)
                {
                    _databaseLogService = new DatabaseLogService(_configuration, _loggerFactory.CreateLogger<DatabaseLogService>());
                    await LogInfoAsync("程序启动", "Program", "Main");
                }

                Console.WriteLine("=== 人员数据同步程序（持续监控模式）===\n");

                // 测试数据库连接
                var databaseService = new DatabaseService(_configuration, _loggerFactory.CreateLogger<DatabaseService>(), _databaseLogService);
                var (isConnected, message) = await databaseService.TestConnectionAsync();
                if (!isConnected)
                {
                    Console.WriteLine($"警告: {message}\n请检查数据库连接配置后重试。");
                    await LogWarningAsync($"数据库连接失败: {message}", "Database", "TestConnection");
                    return;
                }
                Console.WriteLine($"数据库连接: {message}\n");
                await LogInfoAsync($"数据库连接成功: {message}", "Database", "TestConnection");

                // 检查是否需要更新密码
                if (args.Length > 0 && args[0].ToLower() == "--update-passwords")
                {
                    await UpdateAllUserPasswordsAsync();
                    return;
                }

                // 获取检查间隔配置
                var checkIntervalSeconds = int.Parse(_configuration["SyncSettings:CheckIntervalSeconds"] ?? "3600");
                var enableAutoSync = bool.Parse(_configuration["SyncSettings:EnableAutoSync"] ?? "true");
                
                Console.WriteLine($"检查间隔: {checkIntervalSeconds} 秒");
                Console.WriteLine($"自动同步: {(enableAutoSync ? "启用" : "禁用")}");
                Console.WriteLine("程序将持续监控并执行人员数据同步任务。");
                Console.WriteLine("按 Ctrl+C 或关闭窗口以停止程序。");
                Console.WriteLine("提示: 使用 --update-passwords 参数可以更新所有用户密码为默认密码123456\n");
                await LogInfoAsync($"持续监控模式已启动，检查间隔: {checkIntervalSeconds} 秒", "Program", "Main");

                // 注册Ctrl+C处理
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    _isRunning = false;
                    Console.WriteLine("\n正在停止程序...");
                };

                // 持续循环检查和处理
                if (enableAutoSync)
                {
                    await ContinuousSyncLoopAsync(checkIntervalSeconds);
                }
                else
                {
                    // 单次执行模式
                    await ExecuteSyncOnceAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"程序执行出错: {ex.Message}");
                Console.WriteLine($"异常详情: {ex}");
                Serilog.Log.Fatal(ex, "程序执行出错");
                try
                {
                    if (_databaseLogService == null && _configuration != null && _loggerFactory != null)
                    {
                        _databaseLogService = new DatabaseLogService(_configuration, _loggerFactory.CreateLogger<DatabaseLogService>());
                    }
                    if (_databaseLogService != null)
                    {
                        await _databaseLogService.LogErrorAsync("程序执行出错", ex, "Program", "Main", null, "Execute");
                    }
                }
                catch (Exception logEx)
                {
                    Console.WriteLine($"记录日志失败: {logEx.Message}");
                }
            }
            finally
            {
                Console.WriteLine("\n程序即将退出，按任意键关闭...");
                Serilog.Log.CloseAndFlush();
                if (Environment.UserInteractive)
                {
                    Console.WriteLine("\n程序已停止。");
                    WaitForExit();
                }
            }
        }

        /// <summary>
        /// 持续循环同步
        /// </summary>
        static async Task ContinuousSyncLoopAsync(int checkIntervalSeconds)
        {
            int loopCount = 0;
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 持续监控已启动，将每 {checkIntervalSeconds} 秒检查一次...\n");
            
            while (_isRunning)
            {
                try
                {
                    if (!_isRunning)
                    {
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 检测到停止信号，退出循环");
                        break;
                    }
                    
                    loopCount++;
                    var startTime = DateTime.Now;
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始第 {loopCount} 次同步...");

                    // 执行同步任务
                    await ExecuteSyncOnceAsync();

                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 第 {loopCount} 次同步完成（耗时 {elapsed:F2} 秒），等待 {checkIntervalSeconds} 秒后继续...\n");

                    // 等待指定间隔后再次检查
                    if (_isRunning)
                    {
                        try
                        {
                            await Task.Delay(checkIntervalSeconds * 1000);
                            
                            if (!_isRunning)
                            {
                                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 检测到停止信号，退出循环");
                                break;
                            }
                        }
                        catch (Exception delayEx)
                        {
                            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 等待期间出错: {delayEx.Message}");
                            Serilog.Log.Error(delayEx, "等待期间出错");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 同步循环处理出错: {ex.Message}");
                    Console.WriteLine($"异常堆栈: {ex}");
                    Serilog.Log.Error(ex, "同步循环处理出错");
                    try
                    {
                        await LogErrorAsync("同步循环处理出错", ex, "Program", "ContinuousSyncLoop");
                    }
                    catch (Exception logEx)
                    {
                        Console.WriteLine($"记录日志失败: {logEx.Message}");
                    }
                    // 出错后等待一段时间再继续
                    if (_isRunning)
                    {
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 等待 {checkIntervalSeconds} 秒后继续监控...\n");
                        await Task.Delay(checkIntervalSeconds * 1000);
                    }
                }
            }
            
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 持续监控已停止（共执行 {loopCount} 次同步）");
        }

        /// <summary>
        /// 执行一次同步
        /// </summary>
        static async Task ExecuteSyncOnceAsync()
        {
            if (!await _processingLock.WaitAsync(0) || _configuration == null || _loggerFactory == null)
            {
                return;
            }

            try
            {
                var syncService = new PersonnelSyncService(
                    _configuration,
                    _loggerFactory.CreateLogger<PersonnelSyncService>(),
                    _databaseLogService,
                    null,
                    _loggerFactory);

                var results = await syncService.ExecuteAllSyncTasksAsync();
                var report = syncService.GenerateSyncReport(results);

                Console.WriteLine(report);
                Serilog.Log.Information("同步任务执行完成\n{Report}", report);

                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogInformationAsync(
                        "同步任务执行完成", "PersonnelSync", "ExecuteSync", null, "ExecuteAll");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行同步任务失败: {ex.Message}");
                Console.WriteLine($"异常堆栈: {ex}");
                Serilog.Log.Error(ex, "执行同步任务失败");
                try
                {
                    await LogErrorAsync("执行同步任务失败", ex, "Program", "ExecuteSyncOnce");
                }
                catch (Exception logEx)
                {
                    Console.WriteLine($"记录日志失败: {logEx.Message}");
                }
            }
            finally
            {
                _processingLock.Release();
            }
        }

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        static void InitializeLogging(IConfiguration configuration)
        {
            var logPath = configuration["LogSettings:LogPath"] ?? "Logs";
            var logFileName = configuration["LogSettings:LogFileName"] ?? "personnel-sync-{Date}.log";
            Directory.CreateDirectory(logPath);

            Serilog.Log.Logger = new Serilog.LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.Console()
                .WriteTo.File(
                    Path.Combine(logPath, logFileName),
                    rollingInterval: Serilog.RollingInterval.Day,
                    retainedFileCountLimit: int.Parse(configuration["LogSettings:RetainDays"] ?? "30"),
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        /// <summary>
        /// 等待退出
        /// </summary>
        static void WaitForExit()
        {
            if (Console.IsInputRedirected || !Environment.UserInteractive)
            {
                Console.WriteLine("程序将在3秒后自动退出...");
                Task.Delay(3000).Wait();
            }
            else
            {
                Console.WriteLine("按任意键退出...");
                try { Console.ReadKey(); }
                catch { Task.Delay(3000).Wait(); }
            }
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        static async Task LogInfoAsync(string message, string category, string operation)
        {
            Serilog.Log.Information(message);
            if (_databaseLogService != null)
            {
                await _databaseLogService.LogInformationAsync(message, category, "Program", null, operation);
            }
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        static async Task LogWarningAsync(string message, string category, string operation)
        {
            Serilog.Log.Warning(message);
            if (_databaseLogService != null)
            {
                await _databaseLogService.LogWarningAsync(message, category, "Program", null, operation);
            }
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        static async Task LogErrorAsync(string message, Exception ex, string category, string operation)
        {
            Serilog.Log.Error(ex, message);
            if (_databaseLogService != null)
            {
                await _databaseLogService.LogErrorAsync(message, ex, category, "Program", null, operation);
            }
        }

        /// <summary>
        /// 更新所有用户密码为默认密码
        /// </summary>
        static async Task UpdateAllUserPasswordsAsync()
        {
            if (_configuration == null || _loggerFactory == null)
            {
                Console.WriteLine("配置或日志工厂未初始化");
                return;
            }

            try
            {
                Console.WriteLine("=== 开始更新所有用户密码为默认密码123456 ===\n");

                var passwordService = new UserPasswordService(
                    _configuration,
                    _loggerFactory.CreateLogger<UserPasswordService>(),
                    _databaseLogService);

                // 询问是否更新所有用户（包括已有密码的）
                Console.WriteLine("请选择更新模式：");
                Console.WriteLine("1. 只更新密码为空的用户（推荐）");
                Console.WriteLine("2. 更新所有用户的密码（包括已有密码的）");
                Console.Write("请输入选项 (1/2，默认1): ");
                
                string? input = Console.ReadLine();
                bool updateAll = input?.Trim() == "2";

                var (success, affectedRows, message) = await passwordService.UpdateAllUserPasswordsToDefaultAsync(updateAll);

                Console.WriteLine("\n" + new string('=', 50));
                if (success)
                {
                    Console.WriteLine($"✓ {message}");
                    Console.WriteLine($"  共更新 {affectedRows} 个用户的密码");
                }
                else
                {
                    Console.WriteLine($"✗ {message}");
                }
                Console.WriteLine(new string('=', 50) + "\n");

                await LogInfoAsync($"密码更新完成: {message}", "Program", "UpdateAllUserPasswords");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n更新密码失败: {ex.Message}");
                Console.WriteLine($"异常详情: {ex}");
                Serilog.Log.Error(ex, "更新密码失败");
                await LogErrorAsync("更新密码失败", ex, "Program", "UpdateAllUserPasswords");
            }
        }
    }
}
