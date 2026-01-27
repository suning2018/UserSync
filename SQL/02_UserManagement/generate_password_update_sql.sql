-- =============================================
-- 生成密码更新SQL语句（需要配合应用程序使用）
-- 说明：此脚本生成UPDATE语句模板，需要在应用程序中填充加密后的密码
-- 默认密码：123456
-- =============================================

-- 步骤1：查询所有需要更新密码的用户
SELECT 
    u.ID AS UserId,
    u.F_Account AS Account,
    u.F_RealName AS RealName,
    ul.F_UserSecretkey AS Secretkey,
    '123456' AS DefaultPassword,
    -- 生成UPDATE语句模板
    'UPDATE Sys_UserLogOn SET F_UserPassword = N''[ENCRYPTED_PASSWORD]'', F_LastModifyTime = GETDATE(), F_LastModifyUserId = N''system'' WHERE F_UserId = N''' + u.ID + ''';' AS UpdateSQLTemplate
FROM Sys_User u
INNER JOIN Sys_UserLogOn ul ON u.ID = ul.F_UserId
WHERE (ul.F_UserPassword IS NULL OR ul.F_UserPassword = '')
    AND u.F_DeleteMark = 0
    AND u.F_EnabledMark = 1
ORDER BY u.F_Account;

-- 步骤2：在应用程序中处理
-- 1. 读取上述查询结果
-- 2. 对每个用户，使用Secretkey和DefaultPassword生成加密密码
-- 3. 替换UpdateSQLTemplate中的[ENCRYPTED_PASSWORD]
-- 4. 执行生成的UPDATE语句

-- 步骤3：或者直接生成批量更新语句（如果系统有CLR函数）
/*
-- 如果系统有CLR加密函数 dbo.fn_EncryptPassword
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

GO
