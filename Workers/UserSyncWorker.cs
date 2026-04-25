using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UserSync.Extensions;
using UserSync.Services;

namespace UserSync.Workers
{
    /// <summary>
    /// Windows 服务中的用户同步后台任务：默认每天 02:00 执行；若启动或当前时间已过当日 02:00 且当日尚未跑过，则立即执行一次，下一次仍为次日 02:00。
    /// </summary>
    public sealed class UserSyncWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UserSyncWorker> _logger;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly SemaphoreSlim _processingLock = new(1, 1);
        /// <summary>本地日历日：已完成当日计划同步的日期（成功后写入）。</summary>
        private DateTime? _dailySyncCompletedForLocalDate;

        public UserSyncWorker(
            IServiceProvider serviceProvider,
            ILogger<UserSyncWorker> logger,
            IHostApplicationLifetime lifetime)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _lifetime = lifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
            var appSettings = configuration.GetAppSettings();
            DatabaseLogService? databaseLogService = appSettings.LogSettings.EnableDatabaseLog
                ? _serviceProvider.GetRequiredService<DatabaseLogService>()
                : null;

            await LogInfoAsync(databaseLogService, "服务已启动", "UserSyncWorker", "ExecuteAsync");

            var databaseService = _serviceProvider.GetRequiredService<DatabaseService>();
            var (isConnected, message) = await databaseService.TestConnectionAsync();
            if (!isConnected)
            {
                _logger.LogError("数据库连接失败: {Message}", message);
                await LogWarningAsync(databaseLogService, $"数据库连接失败: {message}", "Database", "TestConnection");
                _lifetime.StopApplication();
                return;
            }

            _logger.LogInformation("数据库连接成功: {Message}", message);
            await LogInfoAsync(databaseLogService, $"数据库连接成功: {message}", "Database", "TestConnection");

            var enableAutoSync = appSettings.SyncSettings.EnableAutoSync;
            _logger.LogInformation(
                "同步计划: {Schedule}，自动同步: {AutoSync}",
                "每天 02:00（已过当日 2 点则先立即补跑）",
                enableAutoSync ? "启用" : "禁用");
            await LogInfoAsync(
                databaseLogService,
                $"持续监控已就绪，执行计划: 每天 02:00，已过当日 2 点则立即补跑一次；自动同步: {(enableAutoSync ? "启用" : "禁用")}",
                "UserSyncWorker",
                "ExecuteAsync");

            if (enableAutoSync)
            {
                await ContinuousSyncLoopAsync(databaseLogService, stoppingToken);
                await LogInfoAsync(databaseLogService, "持续同步循环已结束", "UserSyncWorker", "ExecuteAsync");
            }
            else
            {
                await ExecuteSyncOnceAsync(databaseLogService, stoppingToken);
                await LogInfoAsync(databaseLogService, "单次同步已完成，正在停止服务", "UserSyncWorker", "ExecuteAsync");
                _lifetime.StopApplication();
            }
        }

        private async Task ContinuousSyncLoopAsync(DatabaseLogService? databaseLogService, CancellationToken cancellationToken)
        {
            var loopCount = 0;
            _logger.LogInformation("持续监控已启动：每日 02:00 执行；若已过当日 02:00 且当日未执行则立即执行");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    var today = now.Date;
                    var today2am = today.AddHours(2);

                    DateTime nextWakeLocal;
                    if (_dailySyncCompletedForLocalDate == today)
                    {
                        nextWakeLocal = today2am.AddDays(1);
                    }
                    else if (now > today2am)
                    {
                        nextWakeLocal = now;
                    }
                    else
                    {
                        nextWakeLocal = today2am;
                    }

                    var waitTime = nextWakeLocal - DateTime.Now;
                    if (waitTime < TimeSpan.Zero)
                        waitTime = TimeSpan.Zero;

                    if (waitTime == TimeSpan.Zero && now > today2am && _dailySyncCompletedForLocalDate != today)
                    {
                        _logger.LogInformation("当前已过当日 02:00 且当日尚未同步，立即执行");
                        if (databaseLogService != null)
                        {
                            await databaseLogService.LogInformationAsync(
                                "当前已过当日 02:00，立即执行同步",
                                "UserSyncWorker",
                                "ContinuousSyncLoop",
                                null,
                                "ScheduleNextRun");
                        }
                    }
                    else
                    {
                        var nextRunTime = DateTime.Now + waitTime;
                        _logger.LogInformation(
                            "下次同步时间: {NextRun}（约 {Minutes:F1} 分钟后）",
                            nextRunTime,
                            waitTime.TotalMinutes);

                        if (databaseLogService != null)
                        {
                            await databaseLogService.LogInformationAsync(
                                $"下次同步时间: {nextRunTime:yyyy-MM-dd HH:mm:ss}",
                                "UserSyncWorker",
                                "ContinuousSyncLoop",
                                null,
                                "ScheduleNextRun");
                        }
                    }

                    await Task.Delay(waitTime, cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    loopCount++;
                    var startTime = DateTime.Now;
                    _logger.LogInformation("开始第 {LoopCount} 次同步", loopCount);

                    await ExecuteSyncOnceAsync(databaseLogService, cancellationToken);

                    _dailySyncCompletedForLocalDate = DateTime.Today;

                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    _logger.LogInformation("第 {LoopCount} 次同步完成（耗时 {Elapsed:F2} 秒）", loopCount, elapsed);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "同步循环处理出错");
                    try
                    {
                        await LogErrorAsync(databaseLogService, "同步循环处理出错", ex, "UserSyncWorker", "ContinuousSyncLoop");
                    }
                    catch (Exception logEx)
                    {
                        _logger.LogError(logEx, "记录数据库日志失败");
                    }
                }
            }

            _logger.LogInformation("持续监控已停止");
        }

        private async Task ExecuteSyncOnceAsync(DatabaseLogService? databaseLogService, CancellationToken cancellationToken)
        {
            if (!await _processingLock.WaitAsync(0, cancellationToken) || cancellationToken.IsCancellationRequested)
                return;

            try
            {
                var syncService = _serviceProvider.GetRequiredService<PersonnelSyncService>();
                var results = await syncService.ExecuteAllSyncTasksAsync();
                var report = syncService.GenerateSyncReport(results);

                _logger.LogInformation("同步任务执行完成\n{Report}", report);

                if (databaseLogService != null)
                {
                    await databaseLogService.LogInformationAsync(
                        "同步任务执行完成", "UserSync", "ExecuteSync", null, "ExecuteAll");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("同步任务已取消");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行同步任务失败");
                try
                {
                    await LogErrorAsync(databaseLogService, "执行同步任务失败", ex, "UserSyncWorker", "ExecuteSyncOnce");
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "记录数据库日志失败");
                }
            }
            finally
            {
                _processingLock.Release();
            }
        }

        private async Task LogInfoAsync(DatabaseLogService? databaseLogService, string message, string category, string operation)
        {
            _logger.LogInformation("{Message}", message);
            if (databaseLogService != null)
                await databaseLogService.LogInformationAsync(message, category, "UserSyncWorker", null, operation);
        }

        private async Task LogWarningAsync(DatabaseLogService? databaseLogService, string message, string category, string operation)
        {
            _logger.LogWarning("{Message}", message);
            if (databaseLogService != null)
                await databaseLogService.LogWarningAsync(message, category, "UserSyncWorker", null, operation);
        }

        private async Task LogErrorAsync(DatabaseLogService? databaseLogService, string message, Exception ex, string category, string operation)
        {
            _logger.LogError(ex, "{Message}", message);
            if (databaseLogService != null)
                await databaseLogService.LogErrorAsync(message, ex, category, "UserSyncWorker", null, operation);
        }

        public override void Dispose()
        {
            _processingLock.Dispose();
            base.Dispose();
        }
    }
}
