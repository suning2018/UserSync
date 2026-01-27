using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using UserSync.Models;
using UserSync.Services;

namespace UserSync.Extensions
{
    /// <summary>
    /// 服务集合扩展方法
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加应用程序服务
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // 注册配置
            services.Configure<AppSettings>(configuration);
            services.Configure<ConnectionStrings>(configuration.GetSection("ConnectionStrings"));
            services.Configure<LogSettings>(configuration.GetSection("LogSettings"));
            services.Configure<SyncSettings>(configuration.GetSection("SyncSettings"));
            services.Configure<SyncTasks>(configuration.GetSection("SyncSettings:SyncTasks"));

            // 注册服务（注意依赖顺序）
            services.AddSingleton<DatabaseLogService>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var logger = sp.GetRequiredService<ILogger<DatabaseLogService>>();
                return new DatabaseLogService(config, logger);
            });
            services.AddSingleton<DatabaseService>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var logger = sp.GetRequiredService<ILogger<DatabaseService>>();
                var dbLogService = sp.GetService<DatabaseLogService>();
                return new DatabaseService(config, logger, dbLogService);
            });
            services.AddSingleton<DbHelper>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var logger = sp.GetRequiredService<ILogger<DbHelper>>();
                return new DbHelper(config, logger);
            });
            services.AddSingleton<UserPasswordService>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var logger = sp.GetRequiredService<ILogger<UserPasswordService>>();
                var dbLogService = sp.GetService<DatabaseLogService>();
                return new UserPasswordService(config, logger, dbLogService);
            });
            services.AddSingleton<PersonnelSyncService>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var logger = sp.GetRequiredService<ILogger<PersonnelSyncService>>();
                var dbLogService = sp.GetService<DatabaseLogService>();
                var passwordService = sp.GetRequiredService<UserPasswordService>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                return new PersonnelSyncService(config, logger, dbLogService, passwordService, loggerFactory);
            });

            return services;
        }

        /// <summary>
        /// 配置 Serilog 日志
        /// </summary>
        public static IServiceCollection AddSerilogLogging(this IServiceCollection services, IConfiguration configuration)
        {
            var logSettings = configuration.GetSection("LogSettings").Get<LogSettings>() ?? new LogSettings();
            Directory.CreateDirectory(logSettings.LogPath);

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.Console()
                .WriteTo.File(
                    Path.Combine(logSettings.LogPath, logSettings.LogFileName),
                    rollingInterval: Serilog.RollingInterval.Day,
                    retainedFileCountLimit: logSettings.RetainDays,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

            Log.Logger = loggerConfiguration.CreateLogger();

            services.AddLogging(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.AddSerilog();
            });

            return services;
        }
    }
}
