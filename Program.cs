using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using UserSync.Extensions;
using UserSync.Helpers;
using UserSync.Models;
using UserSync.Services;

namespace UserSync
{
    class Program
    {
        private static ServiceProvider? _serviceProvider;
        private static CancellationTokenSource? _cancellationTokenSource;
        private static bool _isRunning = true;
        private static readonly SemaphoreSlim _processingLock = new SemaphoreSlim(1, 1);

        static async Task Main(string[] args)
        {
            // 注册全局异常处理
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                ConsoleHelper.WriteLineWithTimestamp($"未处理的异常: {e.ExceptionObject}");
                Serilog.Log.Fatal((Exception)e.ExceptionObject, "未处理的异常导致程序退出");
                Serilog.Log.CloseAndFlush();
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                ConsoleHelper.WriteLineWithTimestamp($"未观察到的任务异常: {e.Exception}");
                Serilog.Log.Error(e.Exception, "未观察到的任务异常");
                e.SetObserved();
            };

            try
            {
                // 加载配置
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                // 验证配置
                configuration.ValidateConfiguration();

                // 创建服务容器
                var services = new ServiceCollection();
                services.AddSingleton<IConfiguration>(configuration);
                services.AddSerilogLogging(configuration);
                services.AddApplicationServices(configuration);
                _serviceProvider = services.BuildServiceProvider() as ServiceProvider ?? throw new InvalidOperationException("无法创建服务提供程序");

                // 初始化日志
                var logger = _serviceProvider.GetRequiredService<ILogger<Program>>();
                var appSettings = configuration.GetAppSettings();

                // 初始化数据库日志服务
                DatabaseLogService? databaseLogService = null;
                if (appSettings.LogSettings.EnableDatabaseLog)
                {
                    databaseLogService = _serviceProvider.GetRequiredService<DatabaseLogService>();
                    await LogInfoAsync(databaseLogService, logger, "程序启动", "Program", "Main");
                }

                Console.WriteLine("=== 用户数据同步程序（持续监控模式）===\n");

                // 测试数据库连接
                var databaseService = _serviceProvider.GetRequiredService<DatabaseService>();
                var (isConnected, message) = await databaseService.TestConnectionAsync();
                if (!isConnected)
                {
                    ConsoleHelper.WriteError($"警告: {message}\n请检查数据库连接配置后重试。");
                    await LogWarningAsync(databaseLogService, logger, $"数据库连接失败: {message}", "Database", "TestConnection");
                    return;
                }
                ConsoleHelper.WriteSuccess($"数据库连接: {message}\n");
                await LogInfoAsync(databaseLogService, logger, $"数据库连接成功: {message}", "Database", "TestConnection");

                // 检查是否需要更新密码
                if (args.Length > 0 && args[0].ToLower() == "--update-passwords")
                {
                    await UpdateAllUserPasswordsAsync(_serviceProvider, databaseLogService, logger);
                    return;
                }

                // 获取检查间隔配置
                var checkIntervalSeconds = appSettings.SyncSettings.CheckIntervalSeconds;
                var enableAutoSync = appSettings.SyncSettings.EnableAutoSync;

                Console.WriteLine($"检查间隔: {checkIntervalSeconds} 秒");
                Console.WriteLine($"自动同步: {(enableAutoSync ? "启用" : "禁用")}");
                Console.WriteLine("程序将持续监控并执行用户数据同步任务。");
                Console.WriteLine("按 Ctrl+C 或关闭窗口以停止程序。");
                Console.WriteLine("提示: 使用 --update-passwords 参数可以更新所有用户密码为默认密码123456\n");
                await LogInfoAsync(databaseLogService, logger, $"持续监控模式已启动，检查间隔: {checkIntervalSeconds} 秒", "Program", "Main");

                // 创建取消令牌源
                _cancellationTokenSource = new CancellationTokenSource();

                // 注册Ctrl+C处理
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    _isRunning = false;
                    _cancellationTokenSource?.Cancel();
                    ConsoleHelper.WriteLineWithTimestamp("正在停止程序...");
                };

                // 持续循环检查和处理
                if (enableAutoSync)
                {
                    await ContinuousSyncLoopAsync(_serviceProvider, databaseLogService, logger, checkIntervalSeconds, _cancellationTokenSource.Token);
                }
                else
                {
                    // 单次执行模式
                    await ExecuteSyncOnceAsync(_serviceProvider, databaseLogService, logger, _cancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"程序执行出错: {ex.Message}");
                Console.WriteLine($"异常详情: {ex}");
                Serilog.Log.Fatal(ex, "程序执行出错");
                try
                {
                    if (_serviceProvider != null)
                    {
                        var logger = _serviceProvider.GetService<ILogger<Program>>();
                        var databaseLogService = _serviceProvider.GetService<DatabaseLogService>();
                        if (logger != null)
                        {
                            await LogErrorAsync(databaseLogService, logger, "程序执行出错", ex, "Program", "Main");
                        }
                    }
                }
                catch (Exception logEx)
                {
                    ConsoleHelper.WriteError($"记录日志失败: {logEx.Message}");
                }
            }
            finally
            {
                Console.WriteLine("\n程序即将退出...");
                Serilog.Log.CloseAndFlush();
                _cancellationTokenSource?.Dispose();
                _serviceProvider?.Dispose();
                if (Environment.UserInteractive)
                {
                    Console.WriteLine("\n程序已停止。");
                    ConsoleHelper.WaitForExit();
                }
            }
        }

        /// <summary>
        /// 持续循环同步
        /// </summary>
        static async Task ContinuousSyncLoopAsync(
            ServiceProvider serviceProvider,
            DatabaseLogService? databaseLogService,
            ILogger logger,
            int checkIntervalSeconds,
            CancellationToken cancellationToken)
        {
            int loopCount = 0;
            ConsoleHelper.WriteLineWithTimestamp($"持续监控已启动，将每 {checkIntervalSeconds} 秒检查一次...\n");

            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!_isRunning || cancellationToken.IsCancellationRequested)
                    {
                        ConsoleHelper.WriteLineWithTimestamp("检测到停止信号，退出循环");
                        break;
                    }

                    loopCount++;
                    var startTime = DateTime.Now;
                    ConsoleHelper.WriteLineWithTimestamp($"开始第 {loopCount} 次同步...");

                    // 执行同步任务
                    await ExecuteSyncOnceAsync(serviceProvider, databaseLogService, logger, cancellationToken);

                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    ConsoleHelper.WriteLineWithTimestamp($"第 {loopCount} 次同步完成（耗时 {elapsed:F2} 秒），等待 {checkIntervalSeconds} 秒后继续...\n");

                    // 等待指定间隔后再次检查
                    if (_isRunning && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(checkIntervalSeconds * 1000, cancellationToken);

                            if (!_isRunning || cancellationToken.IsCancellationRequested)
                            {
                                ConsoleHelper.WriteLineWithTimestamp("检测到停止信号，退出循环");
                                break;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            ConsoleHelper.WriteLineWithTimestamp("操作已取消");
                            break;
                        }
                        catch (Exception delayEx)
                        {
                            ConsoleHelper.WriteLineWithTimestamp($"等待期间出错: {delayEx.Message}");
                            logger.LogError(delayEx, "等待期间出错");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLineWithTimestamp($"同步循环处理出错: {ex.Message}");
                    Console.WriteLine($"异常堆栈: {ex}");
                    logger.LogError(ex, "同步循环处理出错");
                    try
                    {
                        await LogErrorAsync(databaseLogService, logger, "同步循环处理出错", ex, "Program", "ContinuousSyncLoop");
                    }
                    catch (Exception logEx)
                    {
                        ConsoleHelper.WriteError($"记录日志失败: {logEx.Message}");
                    }
                    // 出错后等待一段时间再继续
                    if (_isRunning && !cancellationToken.IsCancellationRequested)
                    {
                        ConsoleHelper.WriteLineWithTimestamp($"等待 {checkIntervalSeconds} 秒后继续监控...\n");
                        try
                        {
                            await Task.Delay(checkIntervalSeconds * 1000, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            }

            ConsoleHelper.WriteLineWithTimestamp($"持续监控已停止（共执行 {loopCount} 次同步）");
        }

        /// <summary>
        /// 执行一次同步
        /// </summary>
        static async Task ExecuteSyncOnceAsync(
            ServiceProvider serviceProvider,
            DatabaseLogService? databaseLogService,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (!await _processingLock.WaitAsync(0, cancellationToken) || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var syncService = serviceProvider.GetRequiredService<PersonnelSyncService>();
                var results = await syncService.ExecuteAllSyncTasksAsync();
                var report = syncService.GenerateSyncReport(results);

                Console.WriteLine(report);
                logger.LogInformation("同步任务执行完成\n{Report}", report);

                if (databaseLogService != null)
                {
                    await databaseLogService.LogInformationAsync(
                        "同步任务执行完成", "UserSync", "ExecuteSync", null, "ExecuteAll");
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("同步任务已取消");
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"执行同步任务失败: {ex.Message}");
                Console.WriteLine($"异常堆栈: {ex}");
                logger.LogError(ex, "执行同步任务失败");
                try
                {
                    await LogErrorAsync(databaseLogService, logger, "执行同步任务失败", ex, "Program", "ExecuteSyncOnce");
                }
                catch (Exception logEx)
                {
                    ConsoleHelper.WriteError($"记录日志失败: {logEx.Message}");
                }
            }
            finally
            {
                _processingLock.Release();
            }
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        static async Task LogInfoAsync(DatabaseLogService? databaseLogService, ILogger logger, string message, string category, string operation)
        {
            logger?.LogInformation(message);
            if (databaseLogService != null)
            {
                await databaseLogService.LogInformationAsync(message, category, "Program", null, operation);
            }
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        static async Task LogWarningAsync(DatabaseLogService? databaseLogService, ILogger logger, string message, string category, string operation)
        {
            logger?.LogWarning(message);
            if (databaseLogService != null)
            {
                await databaseLogService.LogWarningAsync(message, category, "Program", null, operation);
            }
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        static async Task LogErrorAsync(DatabaseLogService? databaseLogService, ILogger logger, string message, Exception ex, string category, string operation)
        {
            logger?.LogError(ex, message);
            if (databaseLogService != null)
            {
                await databaseLogService.LogErrorAsync(message, ex, category, "Program", null, operation);
            }
        }

        /// <summary>
        /// 更新所有用户密码为默认密码
        /// </summary>
        static async Task UpdateAllUserPasswordsAsync(
            ServiceProvider serviceProvider,
            DatabaseLogService? databaseLogService,
            ILogger logger)
        {
            try
            {
                Console.WriteLine("=== 开始更新所有用户密码为默认密码123456 ===\n");

                var passwordService = serviceProvider.GetRequiredService<UserPasswordService>();

                // 询问是否更新所有用户（包括已有密码的）
                Console.WriteLine("请选择更新模式：");
                Console.WriteLine("1. 只更新密码为空的用户（推荐）");
                Console.WriteLine("2. 更新所有用户的密码（包括已有密码的）");
                Console.Write("请输入选项 (1/2，默认1): ");

                string? input = Console.ReadLine();
                bool updateAll = input?.Trim() == "2";

                var (success, affectedRows, message) = await passwordService.UpdateAllUserPasswordsToDefaultAsync(updateAll);

                ConsoleHelper.WriteSeparator();
                if (success)
                {
                    ConsoleHelper.WriteSuccess(message);
                    Console.WriteLine($"  共更新 {affectedRows} 个用户的密码");
                }
                else
                {
                    ConsoleHelper.WriteError(message);
                }
                ConsoleHelper.WriteSeparator();
                Console.WriteLine();

                await LogInfoAsync(databaseLogService, logger, $"密码更新完成: {message}", "Program", "UpdateAllUserPasswords");
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"\n更新密码失败: {ex.Message}");
                Console.WriteLine($"异常详情: {ex}");
                logger.LogError(ex, "更新密码失败");
                await LogErrorAsync(databaseLogService, logger, "更新密码失败", ex, "Program", "UpdateAllUserPasswords");
            }
        }
    }
}
