using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using UserSync.Extensions;
using UserSync.Services;
using UserSync.Workers;

namespace UserSync
{
    internal static class Program
    {
        static async Task Main(string[] args)
        {
            var contentRoot = AppContext.BaseDirectory;

            if (args.Any(s => string.Equals(s, "--update-passwords", StringComparison.OrdinalIgnoreCase)))
            {
                await RunPasswordUpdateAsync(args, contentRoot);
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    Log.Fatal(ex, "未处理的异常");
                else
                    Log.Fatal("未处理的异常: {ExceptionObject}", e.ExceptionObject);
                Log.CloseAndFlush();
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Log.Error(e.Exception, "未观察到的任务异常");
                e.SetObserved();
            };

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = contentRoot,
                Args = args
            });

            builder.Configuration.ValidateConfiguration();

            builder.Services.AddSerilogLogging(builder.Configuration, writeToConsole: false);
            builder.Services.AddApplicationServices(builder.Configuration);
            builder.Services.AddWindowsService(options => { options.ServiceName = "UserSync"; });
            builder.Services.AddHostedService<UserSyncWorker>();

            var host = builder.Build();

            try
            {
                await host.RunAsync();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// 命令行维护：批量更新密码（非服务模式）。无交互环境下请使用 --update-mode 1 或 2。
        /// </summary>
        static async Task RunPasswordUpdateAsync(string[] args, string contentRoot)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(contentRoot)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            configuration.ValidateConfiguration();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSerilogLogging(configuration, writeToConsole: Environment.UserInteractive);
            services.AddApplicationServices(configuration);

            using var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("PasswordUpdate");
            var databaseLogService = provider.GetService<DatabaseLogService>();

            try
            {
                var updateAll = false;
                for (var i = 0; i < args.Length; i++)
                {
                    if (!string.Equals(args[i], "--update-mode", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(args[i], "-m", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (i + 1 >= args.Length)
                        break;
                    updateAll = args[i + 1].Trim() == "2";
                    break;
                }

                if (Environment.UserInteractive)
                {
                    Console.WriteLine("=== 更新用户密码为默认密码 ===");
                    Console.WriteLine("1. 仅更新空密码（默认）  2. 更新全部用户");
                    Console.Write("选择 (1/2，默认1): ");
                    var input = Console.ReadLine();
                    if (string.Equals(input?.Trim(), "2", StringComparison.Ordinal))
                        updateAll = true;
                }

                var passwordService = provider.GetRequiredService<UserPasswordService>();
                var (success, affectedRows, message) = await passwordService.UpdateAllUserPasswordsToDefaultAsync(updateAll);

                if (Environment.UserInteractive)
                {
                    if (success)
                        Console.WriteLine($"完成: {message}，共更新 {affectedRows} 个用户");
                    else
                        Console.WriteLine($"失败: {message}");
                }

                logger.LogInformation("密码更新完成: {Message}", message);
                if (databaseLogService != null)
                {
                    await databaseLogService.LogInformationAsync(
                        $"密码更新完成: {message}",
                        "Program",
                        "PasswordUpdate",
                        null,
                        "UpdateAllUserPasswords");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新密码失败");
                if (databaseLogService != null)
                {
                    await databaseLogService.LogErrorAsync(
                        "更新密码失败",
                        ex,
                        "Program",
                        "PasswordUpdate",
                        null,
                        "UpdateAllUserPasswords");
                }

                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
