-- =============================================
-- 简单直接的SQL密码更新脚本
-- 默认密码：123456
-- =============================================

-- 方案A：如果系统有CLR加密函数（推荐）
-- 假设函数名为：dbo.fn_EncryptPassword(@password, @secretkey)
-- 或者：dbo.fn_MD5 和 dbo.fn_DESEncrypt

-- 尝试使用CLR函数更新密码
BEGIN TRY
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
    
    PRINT '成功！已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 个用户的密码为123456';
END TRY
BEGIN CATCH
    PRINT '错误：未找到CLR加密函数，请使用方案B';
    PRINT '错误信息：' + ERROR_MESSAGE();
END CATCH

GO

-- =============================================
-- 方案B：如果没有CLR函数，使用以下方法
-- =============================================

-- 1. 先查询需要更新的用户（包含密钥信息）
SELECT 
    u.ID AS UserId,
    u.F_Account AS Account,
    u.F_RealName AS RealName,
    ul.F_UserSecretkey AS Secretkey
FROM Sys_User u
INNER JOIN Sys_UserLogOn ul ON u.ID = ul.F_UserId
WHERE (ul.F_UserPassword IS NULL OR ul.F_UserPassword = '')
    AND u.F_DeleteMark = 0
    AND u.F_EnabledMark = 1;

-- 2. 将上述查询结果导出
-- 3. 在应用程序中使用以下逻辑生成加密密码：
--    - 原始密码：123456
--    - 第一步：Md5('123456', 32).ToLower()
--    - 第二步：DESEncrypt.Encrypt(第一步结果, Secretkey).ToLower()
--    - 第三步：Md5(第二步结果, 32).ToLower()
-- 4. 生成UPDATE语句并执行

-- =============================================
-- 方案C：手动更新（适用于少量用户）
-- =============================================

-- 示例：更新单个用户的密码
-- 注意：需要预先在应用程序中计算好加密后的密码
/*
DECLARE @UserId NVARCHAR(50) = N'用户ID';
DECLARE @EncryptedPassword NVARCHAR(200) = N'加密后的密码';  -- 需要在应用程序中生成

UPDATE Sys_UserLogOn 
SET 
    F_UserPassword = @EncryptedPassword,
    F_LastModifyTime = GETDATE(),
    F_LastModifyUserId = N'system'
WHERE F_UserId = @UserId;
*/

-- =============================================
-- 方案D：检查系统是否有CLR函数
-- =============================================

-- 检查是否存在常见的CLR加密函数
SELECT 
    name AS FunctionName,
    type_desc AS FunctionType
FROM sys.objects
WHERE type = 'FN'  -- 标量函数
    AND (
        name LIKE '%MD5%' 
        OR name LIKE '%DES%' 
        OR name LIKE '%Encrypt%'
        OR name LIKE '%Password%'
    )
ORDER BY name;

-- 如果找到函数，可以使用类似以下的方式：
/*
-- 示例：使用找到的CLR函数
UPDATE ul
SET 
    F_UserPassword = dbo.[函数名]('123456', ul.F_UserSecretkey),
    F_LastModifyTime = GETDATE(),
    F_LastModifyUserId = N'system'
FROM Sys_UserLogOn ul
INNER JOIN Sys_User u ON ul.F_UserId = u.ID
WHERE (ul.F_UserPassword IS NULL OR ul.F_UserPassword = '')
    AND u.F_DeleteMark = 0
    AND u.F_EnabledMark = 1;
*/

GO

-- =============================================
-- 验证：检查更新结果
-- =============================================

SELECT 
    COUNT(*) AS TotalUsers,
    SUM(CASE WHEN ul.F_UserPassword IS NULL OR ul.F_UserPassword = '' THEN 1 ELSE 0 END) AS UsersWithoutPassword,
    SUM(CASE WHEN ul.F_UserPassword IS NOT NULL AND ul.F_UserPassword <> '' THEN 1 ELSE 0 END) AS UsersWithPassword
FROM Sys_User u
LEFT JOIN Sys_UserLogOn ul ON u.ID = ul.F_UserId
WHERE u.F_DeleteMark = 0
    AND u.F_EnabledMark = 1;

GO
