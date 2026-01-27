using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PersonnelSync.Services
{
    /// <summary>
    /// 用户密码更新服务
    /// </summary>
    public class UserPasswordService
    {
        private readonly DbHelper _dbHelper;
        private readonly ILogger _logger;
        private readonly DatabaseLogService? _databaseLogService;
        private const string DefaultPassword = "123456";

        public UserPasswordService(
            IConfiguration configuration,
            ILogger logger,
            DatabaseLogService? databaseLogService = null)
        {
            _dbHelper = new DbHelper(configuration, logger);
            _logger = logger;
            _databaseLogService = databaseLogService;
        }

        /// <summary>
        /// 更新所有用户的密码为默认密码
        /// </summary>
        /// <param name="updateAll">是否更新所有用户（true=更新所有，false=只更新密码为空的用户）</param>
        /// <returns>更新结果</returns>
        public async Task<(bool Success, int AffectedRows, string Message)> UpdateAllUserPasswordsToDefaultAsync(bool updateAll = false)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int affectedRows = 0;

            try
            {
                // 查询需要更新密码的用户
                string querySql = updateAll
                    ? @"
                        SELECT 
                            ul.ID,
                            ul.F_UserId,
                            ul.F_UserSecretkey,
                            ul.F_UserPassword
                        FROM Sys_UserLogOn ul
                        INNER JOIN Sys_User u ON ul.F_UserId = u.ID
                        WHERE u.F_DeleteMark IS NULL OR u.F_DeleteMark = 0
                    "
                    : @"
                        SELECT 
                            ul.ID,
                            ul.F_UserId,
                            ul.F_UserSecretkey,
                            ul.F_UserPassword
                        FROM Sys_UserLogOn ul
                        INNER JOIN Sys_User u ON ul.F_UserId = u.ID
                        WHERE (ul.F_UserPassword IS NULL OR ul.F_UserPassword = '')
                            AND (u.F_DeleteMark IS NULL OR u.F_DeleteMark = 0)
                    ";

                var usersToUpdate = new List<(string Id, string UserId, string SecretKey)>();

                using (var reader = await _dbHelper.ExecuteReaderAsync(querySql))
                {
                    while (await reader.ReadAsync())
                    {
                        string id = reader["ID"].ToString() ?? string.Empty;
                        string userId = reader["F_UserId"].ToString() ?? string.Empty;
                        string secretKey = reader["F_UserSecretkey"]?.ToString() ?? string.Empty;

                        // 如果密钥为空，生成新密钥
                        if (string.IsNullOrEmpty(secretKey))
                        {
                            secretKey = PasswordEncryptionHelper.GenerateSecretKey();
                        }

                        usersToUpdate.Add((id, userId, secretKey));
                    }
                }

                if (usersToUpdate.Count == 0)
                {
                    stopwatch.Stop();
                    string message = updateAll 
                        ? "没有需要更新密码的用户" 
                        : "没有密码为空的用户需要更新";
                    
                    _logger.LogInformation("[UpdateAllUserPasswordsToDefault] {Message}, 耗时: {Duration}ms", 
                        message, stopwatch.ElapsedMilliseconds);
                    
                    return (true, 0, message);
                }

                // 批量更新密码
                affectedRows = await _dbHelper.ExecuteTransactionAsync(async transaction =>
                {
                    int count = 0;
                    foreach (var (id, userId, secretKey) in usersToUpdate)
                    {
                        try
                        {
                            // 加密密码
                            string encryptedPassword = PasswordEncryptionHelper.EncryptPassword(DefaultPassword, secretKey);

                            // 更新密码
                            string updateSql = @"
                                UPDATE Sys_UserLogOn
                                SET F_UserPassword = @Password,
                                    F_UserSecretkey = @SecretKey,
                                    modtime = GETDATE(),
                                    moduser = N'system'
                                WHERE ID = @Id
                            ";

                            var parameters = new[]
                            {
                                DbHelper.CreateParameter("@Password", encryptedPassword),
                                DbHelper.CreateParameter("@SecretKey", secretKey),
                                DbHelper.CreateParameter("@Id", SqlDbType.UniqueIdentifier, Guid.Parse(id))
                            };

                            await _dbHelper.ExecuteNonQueryAsync(updateSql, transaction, parameters);
                            count++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[UpdateAllUserPasswordsToDefault] 更新用户 {UserId} 密码失败", userId);
                        }
                    }
                    return count;
                });

                stopwatch.Stop();

                string successMessage = updateAll
                    ? $"成功更新 {affectedRows} 个用户的密码为默认密码 {DefaultPassword}"
                    : $"成功更新 {affectedRows} 个密码为空用户的密码为默认密码 {DefaultPassword}";

                _logger.LogInformation("[UpdateAllUserPasswordsToDefault] {Message}, 耗时: {Duration}ms", 
                    successMessage, stopwatch.ElapsedMilliseconds);

                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogInformationAsync(
                        successMessage, "UserPasswordService", "UpdateAllUserPasswordsToDefault", null, "ExecuteUpdate");
                }

                return (true, affectedRows, successMessage);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                string errorMessage = $"更新用户密码失败：{ex.Message}";
                
                _logger.LogError(ex, "[UpdateAllUserPasswordsToDefault] 更新用户密码失败");
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogErrorAsync(
                        errorMessage, ex, "UserPasswordService", "UpdateAllUserPasswordsToDefault", null, "ExecuteUpdate");
                }

                return (false, affectedRows, errorMessage);
            }
        }

        /// <summary>
        /// 为指定用户更新密码为默认密码
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>更新结果</returns>
        public async Task<(bool Success, string Message)> UpdateUserPasswordToDefaultAsync(string userId)
        {
            try
            {
                // 查询用户登录信息
                string querySql = @"
                    SELECT 
                        ul.ID,
                        ul.F_UserSecretkey
                    FROM Sys_UserLogOn ul
                    WHERE ul.F_UserId = @UserId
                ";

                string? id = null;
                string? secretKey = null;

                var userIdParam = DbHelper.CreateParameter("@UserId", SqlDbType.UniqueIdentifier, Guid.Parse(userId));

                using (var reader = await _dbHelper.ExecuteReaderAsync(querySql, userIdParam))
                {
                    if (await reader.ReadAsync())
                    {
                        id = reader["ID"].ToString();
                        secretKey = reader["F_UserSecretkey"]?.ToString();
                    }
                }

                if (string.IsNullOrEmpty(id))
                {
                    return (false, $"用户 {userId} 的登录信息不存在");
                }

                // 如果密钥为空，生成新密钥
                if (string.IsNullOrEmpty(secretKey))
                {
                    secretKey = PasswordEncryptionHelper.GenerateSecretKey();
                }

                // 加密密码
                string encryptedPassword = PasswordEncryptionHelper.EncryptPassword(DefaultPassword, secretKey);

                // 更新密码
                string updateSql = @"
                    UPDATE Sys_UserLogOn
                    SET F_UserPassword = @Password,
                        F_UserSecretkey = @SecretKey,
                        modtime = GETDATE(),
                        moduser = N'system'
                    WHERE ID = @Id
                ";

                var parameters = new[]
                {
                    DbHelper.CreateParameter("@Password", encryptedPassword),
                    DbHelper.CreateParameter("@SecretKey", secretKey),
                    DbHelper.CreateParameter("@Id", SqlDbType.UniqueIdentifier, Guid.Parse(id))
                };

                int affectedRows = await _dbHelper.ExecuteNonQueryAsync(updateSql, parameters);

                if (affectedRows > 0)
                {
                    string message = $"用户 {userId} 的密码已更新为默认密码 {DefaultPassword}";
                    _logger.LogInformation("[UpdateUserPasswordToDefault] {Message}", message);
                    
                    if (_databaseLogService != null)
                    {
                        await _databaseLogService.LogInformationAsync(
                            message, "UserPasswordService", "UpdateUserPasswordToDefault", userId, "ExecuteUpdate");
                    }

                    return (true, message);
                }
                else
                {
                    return (false, $"更新用户 {userId} 密码失败：未找到记录");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"更新用户 {userId} 密码失败：{ex.Message}";
                _logger.LogError(ex, "[UpdateUserPasswordToDefault] 更新用户密码失败");
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogErrorAsync(
                        errorMessage, ex, "UserPasswordService", "UpdateUserPasswordToDefault", userId, "ExecuteUpdate");
                }

                return (false, errorMessage);
            }
        }
    }
}
