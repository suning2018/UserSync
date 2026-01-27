using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PersonnelSync.Services
{
    /// <summary>
    /// 数据库操作服务
    /// </summary>
    public class DatabaseService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseService> _logger;
        private readonly DatabaseLogService? _databaseLogService;

        public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger, DatabaseLogService? databaseLogService = null)
        {
            _configuration = configuration;
            _logger = logger;
            _databaseLogService = databaseLogService;
        }

        /// <summary>
        /// 获取数据库连接字符串
        /// </summary>
        public string GetConnectionString()
        {
            var connectionString = _configuration.GetConnectionString("SQLServer");
            
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogError("数据库连接字符串未配置。请检查 appsettings.json 中的 ConnectionStrings:SQLServer 配置。");
                throw new InvalidOperationException("数据库连接字符串未配置。请检查 appsettings.json 中的 ConnectionStrings:SQLServer 配置。");
            }
            
            return connectionString;
        }

        /// <summary>
        /// 创建数据库连接
        /// </summary>
        public async Task<SqlConnection> CreateConnectionAsync()
        {
            var connectionString = GetConnectionString();
            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }

        /// <summary>
        /// 测试数据库连接
        /// </summary>
        public async Task<(bool IsConnected, string Message)> TestConnectionAsync()
        {
            try
            {
                using var connection = await CreateConnectionAsync();
                using var command = new SqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync();
                
                return (true, "数据库连接成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "数据库连接测试失败");
                return (false, $"数据库连接失败: {ex.Message}");
            }
        }
    }
}
