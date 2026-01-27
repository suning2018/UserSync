using Microsoft.Extensions.Configuration;
using UserSync.Models;

namespace UserSync.Extensions
{
    /// <summary>
    /// 配置扩展方法
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// 获取强类型配置
        /// </summary>
        public static AppSettings GetAppSettings(this IConfiguration configuration)
        {
            return new AppSettings
            {
                ConnectionStrings = configuration.GetSection("ConnectionStrings").Get<ConnectionStrings>() ?? new ConnectionStrings(),
                LogSettings = configuration.GetSection("LogSettings").Get<LogSettings>() ?? new LogSettings(),
                SyncSettings = configuration.GetSection("SyncSettings").Get<SyncSettings>() ?? new SyncSettings()
            };
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        public static void ValidateConfiguration(this IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("SQLServer");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("数据库连接字符串未配置。请检查 appsettings.json 中的 ConnectionStrings:SQLServer 配置。");
            }
        }
    }
}
