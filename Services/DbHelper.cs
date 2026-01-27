using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace UserSync.Services
{
    /// <summary>
    /// 数据库操作辅助类
    /// 封装常用的数据库操作方法
    /// </summary>
    public class DbHelper
    {
        private readonly string _connectionString;
        private readonly ILogger? _logger;

        public DbHelper(IConfiguration configuration, ILogger? logger = null)
        {
            _connectionString = configuration.GetConnectionString("SQLServer")
                ?? throw new InvalidOperationException("数据库连接字符串未配置");
            _logger = logger;
        }

        /// <summary>
        /// 使用连接字符串创建 DbHelper
        /// </summary>
        public DbHelper(string connectionString, ILogger? logger = null)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        /// <summary>
        /// 创建数据库连接
        /// </summary>
        public async Task<SqlConnection> CreateConnectionAsync()
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

        /// <summary>
        /// 执行非查询SQL（INSERT, UPDATE, DELETE）
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="parameters">SQL参数（可选）</param>
        /// <returns>受影响的行数</returns>
        public async Task<int> ExecuteNonQueryAsync(string sql, params SqlParameter[]? parameters)
        {
            try
            {
                using var connection = await CreateConnectionAsync();
                using var command = new SqlCommand(sql, connection);
                
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                return await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "执行SQL失败: {Sql}", sql);
                throw;
            }
        }

        /// <summary>
        /// 执行非查询SQL（使用事务）
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="transaction">事务对象</param>
        /// <param name="parameters">SQL参数（可选）</param>
        /// <returns>受影响的行数</returns>
        public async Task<int> ExecuteNonQueryAsync(string sql, SqlTransaction transaction, params SqlParameter[]? parameters)
        {
            try
            {
                using var command = new SqlCommand(sql, transaction.Connection, transaction);
                
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                return await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "执行SQL失败: {Sql}", sql);
                throw;
            }
        }

        /// <summary>
        /// 执行标量查询（返回单个值）
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="parameters">SQL参数（可选）</param>
        /// <returns>查询结果</returns>
        public async Task<object?> ExecuteScalarAsync(string sql, params SqlParameter[]? parameters)
        {
            try
            {
                using var connection = await CreateConnectionAsync();
                using var command = new SqlCommand(sql, connection);
                
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                return await command.ExecuteScalarAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "执行标量查询失败: {Sql}", sql);
                throw;
            }
        }

        /// <summary>
        /// 执行数据读取
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="parameters">SQL参数（可选）</param>
        /// <returns>SqlDataReader对象</returns>
        public async Task<SqlDataReader> ExecuteReaderAsync(string sql, params SqlParameter[]? parameters)
        {
            try
            {
                var connection = await CreateConnectionAsync();
                var command = new SqlCommand(sql, connection);
                
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                // 注意：调用者需要负责关闭 connection 和 reader
                return await command.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "执行数据读取失败: {Sql}", sql);
                throw;
            }
        }

        /// <summary>
        /// 执行事务操作
        /// </summary>
        /// <param name="action">事务操作委托</param>
        /// <returns>事务执行结果</returns>
        public async Task<T> ExecuteTransactionAsync<T>(Func<SqlTransaction, Task<T>> action)
        {
            using var connection = await CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                var result = await action(transaction);
                transaction.Commit();
                return result;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger?.LogError(ex, "事务执行失败，已回滚");
                throw;
            }
        }

        /// <summary>
        /// 执行事务操作（无返回值）
        /// </summary>
        /// <param name="action">事务操作委托</param>
        public async Task ExecuteTransactionAsync(Func<SqlTransaction, Task> action)
        {
            using var connection = await CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                await action(transaction);
                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger?.LogError(ex, "事务执行失败，已回滚");
                throw;
            }
        }

        /// <summary>
        /// 批量执行SQL（在同一事务中）
        /// </summary>
        /// <param name="sqlCommands">SQL命令列表（SQL语句和参数）</param>
        /// <returns>总受影响的行数</returns>
        public async Task<int> ExecuteBatchAsync(IEnumerable<(string Sql, SqlParameter[]? Parameters)> sqlCommands)
        {
            return await ExecuteTransactionAsync(async transaction =>
            {
                int totalAffected = 0;
                
                foreach (var (sql, parameters) in sqlCommands)
                {
                    using var command = new SqlCommand(sql, transaction.Connection, transaction);
                    
                    if (parameters != null && parameters.Length > 0)
                    {
                        command.Parameters.AddRange(parameters);
                    }

                    totalAffected += await command.ExecuteNonQueryAsync();
                }

                return totalAffected;
            });
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
                _logger?.LogError(ex, "数据库连接测试失败");
                return (false, $"数据库连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建SQL参数
        /// </summary>
        public static SqlParameter CreateParameter(string name, object? value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        /// <summary>
        /// 创建SQL参数（指定类型）
        /// </summary>
        public static SqlParameter CreateParameter(string name, SqlDbType dbType, object? value)
        {
            var parameter = new SqlParameter(name, dbType)
            {
                Value = value ?? DBNull.Value
            };
            return parameter;
        }
    }
}
