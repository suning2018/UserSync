-- =============================================
-- 直接在SQL中更新用户密码为默认密码123456
-- 说明：此脚本使用SQL Server内置函数实现密码加密
-- 默认密码：123456
-- =============================================

-- 方法1：使用SQL Server内置函数（如果系统有CLR加密函数）
-- 注意：如果系统没有CLR函数，请使用方法2或方法3

-- 检查是否存在CLR加密函数
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'fn_MD5' AND type = 'FN')
BEGIN
    -- 如果存在CLR函数，使用CLR函数进行加密
    UPDATE ul
    SET 
        F_UserPassword = dbo.fn_MD5(
            dbo.fn_DESEncrypt(
                dbo.fn_MD5('123456', 32),
                ul.F_UserSecretkey
            ),
            32
        ),
        F_LastModifyTime = GETDATE(),
        F_LastModifyUserId = N'system'
    FROM Sys_UserLogOn ul
    INNER JOIN Sys_User u ON ul.F_UserId = u.ID
    WHERE (ul.F_UserPassword IS NULL OR ul.F_UserPassword = '')
        AND u.F_DeleteMark = 0
        AND u.F_EnabledMark = 1;
    
    PRINT '已使用CLR函数更新密码，共更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 个用户';
END
ELSE
BEGIN
    PRINT '未找到CLR加密函数，请使用方法2或方法3';
END

GO

-- =============================================
-- 方法2：导出数据到临时表，方便在应用程序中处理
-- =============================================

-- 创建临时表，导出需要更新密码的用户信息
IF OBJECT_ID('tempdb..#UsersToUpdate') IS NOT NULL
    DROP TABLE #UsersToUpdate;

SELECT 
    u.ID AS UserId,
    u.F_Account AS Account,
    u.F_RealName AS RealName,
    ul.F_UserSecretkey AS Secretkey,
    '123456' AS DefaultPassword
INTO #UsersToUpdate
FROM Sys_User u
INNER JOIN Sys_UserLogOn ul ON u.ID = ul.F_UserId
WHERE (ul.F_UserPassword IS NULL OR ul.F_UserPassword = '')
    AND u.F_DeleteMark = 0
    AND u.F_EnabledMark = 1;

-- 显示需要更新的用户列表
SELECT * FROM #UsersToUpdate;

-- 生成UPDATE语句模板（需要在应用程序中填充加密后的密码）
SELECT 
    'UPDATE Sys_UserLogOn SET F_UserPassword = N''' + 
    '加密后的密码' +  -- 这里需要在应用程序中替换为实际加密后的密码
    ''', F_LastModifyTime = GETDATE(), F_LastModifyUserId = N''system'' WHERE F_UserId = N''' + 
    UserId + ''';' AS UpdateSQL
FROM #UsersToUpdate;

PRINT '已生成 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条UPDATE语句模板';
PRINT '请在应用程序中生成加密密码后，替换"加密后的密码"部分，然后执行';

-- 清理临时表
-- DROP TABLE #UsersToUpdate;

GO

-- =============================================
-- 方法3：批量更新（推荐）- 使用存储过程或CLR函数
-- =============================================

-- 如果系统有CLR加密函数，创建以下存储过程
/*
CREATE PROCEDURE sp_UpdateUserPasswordToDefault
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @DefaultPassword NVARCHAR(100) = N'123456';
    DECLARE @UpdatedCount INT = 0;
    
    -- 创建临时表存储需要更新的用户
    CREATE TABLE #TempUsers (
        UserId NVARCHAR(50),
        Secretkey NVARCHAR(50),
        EncryptedPassword NVARCHAR(200)
    );
    
    -- 获取所有需要更新密码的用户
    INSERT INTO #TempUsers (UserId, Secretkey)
    SELECT 
        ul.F_UserId,
        ul.F_UserSecretkey
    FROM Sys_UserLogOn ul
    INNER JOIN Sys_User u ON ul.F_UserId = u.ID
    WHERE (ul.F_UserPassword IS NULL OR ul.F_UserPassword = '')
        AND u.F_DeleteMark = 0
        AND u.F_EnabledMark = 1;
    
    -- 为每个用户生成加密密码（如果系统有CLR函数）
    -- 这里假设有CLR函数：dbo.fn_EncryptPassword(@password, @secretkey)
    UPDATE #TempUsers
    SET EncryptedPassword = dbo.fn_EncryptPassword(@DefaultPassword, Secretkey);
    
    -- 更新Sys_UserLogOn表
    UPDATE ul
    SET 
        F_UserPassword = t.EncryptedPassword,
        F_LastModifyTime = GETDATE(),
        F_LastModifyUserId = N'system'
    FROM Sys_UserLogOn ul
    INNER JOIN #TempUsers t ON ul.F_UserId = t.UserId;
    
    SET @UpdatedCount = @@ROWCOUNT;
    
    DROP TABLE #TempUsers;
    
    PRINT '已更新 ' + CAST(@UpdatedCount AS VARCHAR(10)) + ' 个用户的密码为默认密码123456';
    
    RETURN @UpdatedCount;
END
GO

-- 执行存储过程
-- EXEC sp_UpdateUserPasswordToDefault;
*/

-- =============================================
-- 方法4：如果系统没有CLR函数，需要手动计算（不推荐）
-- =============================================

-- 由于DES加密在纯SQL中实现复杂，建议：
-- 1. 在应用程序中批量生成加密后的密码
-- 2. 导出为SQL更新语句
-- 3. 执行SQL更新语句

-- 示例：手动更新单个用户的密码（需要预先计算好加密后的密码）
/*
UPDATE Sys_UserLogOn 
SET 
    F_UserPassword = N'预先计算好的加密密码',  -- 需要在应用程序中计算
    F_LastModifyTime = GETDATE(),
    F_LastModifyUserId = N'system'
WHERE F_UserId = N'用户ID';
*/

-- =============================================
-- 方法5：使用OPENROWSET调用外部程序（高级方法）
-- =============================================

-- 如果系统配置了OPENROWSET，可以调用外部程序生成密码
-- 注意：需要启用Ad Hoc Distributed Queries
/*
EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;
EXEC sp_configure 'Ad Hoc Distributed Queries', 1;
RECONFIGURE;
*/

-- =============================================
-- 推荐方案：创建CLR函数（如果可能）
-- =============================================

/*
-- 如果系统支持CLR，可以创建以下CLR函数来加密密码
-- 需要在.NET中创建CLR程序集，然后在SQL Server中注册

CREATE ASSEMBLY EncryptionFunctions
FROM 'C:\Path\To\EncryptionFunctions.dll'
WITH PERMISSION_SET = SAFE;
GO

CREATE FUNCTION dbo.fn_EncryptPassword(@password NVARCHAR(100), @secretkey NVARCHAR(50))
RETURNS NVARCHAR(200)
AS EXTERNAL NAME EncryptionFunctions.[EncryptionFunctions.Encrypt].EncryptPassword;
GO

-- 然后使用以下SQL更新密码
UPDATE ul
SET 
    F_UserPassword = dbo.fn_EncryptPassword('123456', ul.F_UserSecretkey),
    F_LastModifyTime = GETDATE(),
    F_LastModifyUserId = N'system'
FROM Sys_UserLogOn ul
INNER JOIN Sys_User u ON ul.F_UserId = u.ID
WHERE (ul.F_UserPassword IS NULL OR ul.F_UserPassword = '')
    AND u.F_DeleteMark = 0
    AND u.F_EnabledMark = 1;
*/

-- =============================================
-- 实用查询：检查哪些用户需要更新密码
-- =============================================

SELECT 
    u.ID,
    u.F_Account,
    u.F_RealName,
    ul.F_UserSecretkey,
    CASE 
        WHEN ul.F_UserPassword IS NULL OR ul.F_UserPassword = '' THEN '需要更新'
        ELSE '已有密码'
    END AS PasswordStatus
FROM Sys_User u
LEFT JOIN Sys_UserLogOn ul ON u.ID = ul.F_UserId
WHERE u.F_DeleteMark = 0
    AND u.F_EnabledMark = 1
    AND (ul.F_UserPassword IS NULL OR ul.F_UserPassword = '')
ORDER BY u.F_Account;

GO

-- =============================================
-- 重要说明：
-- =============================================
-- 1. 如果系统有CLR加密函数，直接使用方法1
-- 2. 如果系统没有CLR函数，建议：
--    a) 在应用程序中批量生成加密密码
--    b) 导出为SQL UPDATE语句
--    c) 执行SQL更新
-- 3. 或者联系DBA创建CLR函数来支持SQL中的密码加密
-- 4. 默认密码：123456
