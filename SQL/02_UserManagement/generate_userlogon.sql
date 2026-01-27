-- =============================================
-- 为Sys_User用户生成Sys_UserLogOn登录信息
-- 说明：此脚本为缺少Sys_UserLogOn记录的用户生成登录信息
-- 默认密码：123456（需要在应用程序中加密后更新）
-- 注意：密码加密逻辑需要在应用程序中完成，这里生成基础记录
-- =============================================

-- 方法1：为所有缺少Sys_UserLogOn记录的用户生成基础记录
-- 注意：F_UserPassword需要根据实际密码通过应用程序加密后更新

INSERT INTO Sys_UserLogOn (ID, F_UserId, F_UserSecretkey, F_UserPassword, F_AllowStartTime, F_AllowEndTime, F_LockStartDate, F_LockEndDate, F_FirstVisitTime, F_PreviousVisitTime, F_LastVisitTime, F_LogOnCount, F_UserOnLine, F_Question, F_AnswerQuestion, F_CheckIPAddress, F_Language, F_Theme, F_LastModifyTime, F_LastModifyUserId)
SELECT 
    u.ID AS ID,
    u.ID AS F_UserId,
    -- 生成16位密钥：使用NEWID()生成GUID，取前16位并转换为小写
    LOWER(SUBSTRING(REPLACE(CAST(NEWID() AS VARCHAR(36)), '-', ''), 1, 16)) AS F_UserSecretkey,
    -- 默认密码：123456（需要在应用程序中通过加密逻辑生成后更新）
    -- 注意：这里使用空字符串，实际密码需要通过应用程序的加密逻辑生成
    -- 加密步骤：
    -- 1. Md5('123456', 32).ToLower()
    -- 2. DESEncrypt.Encrypt(步骤1结果, F_UserSecretkey).ToLower()
    -- 3. Md5(步骤2结果, 32).ToLower() -> 这就是最终的F_UserPassword
    N'' AS F_UserPassword,  -- 需要后续通过应用程序更新为加密后的密码
    NULL AS F_AllowStartTime,
    NULL AS F_AllowEndTime,
    NULL AS F_LockStartDate,
    NULL AS F_LockEndDate,
    NULL AS F_FirstVisitTime,
    NULL AS F_PreviousVisitTime,
    NULL AS F_LastVisitTime,
    0 AS F_LogOnCount,
    0 AS F_UserOnLine,
    NULL AS F_Question,
    NULL AS F_AnswerQuestion,
    0 AS F_CheckIPAddress,
    NULL AS F_Language,
    NULL AS F_Theme,
    GETDATE() AS F_LastModifyTime,
    N'system' AS F_LastModifyUserId
FROM Sys_User u
LEFT JOIN Sys_UserLogOn ul ON u.ID = ul.F_UserId
WHERE ul.ID IS NULL  -- 只插入缺少Sys_UserLogOn记录的用户
    AND u.F_DeleteMark = 0  -- 只处理未删除的用户
    AND u.F_EnabledMark = 1;  -- 只处理启用的用户

PRINT '已为 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 个用户生成Sys_UserLogOn基础记录';
PRINT '注意：F_UserPassword字段需要后续通过应用程序更新为正确加密的密码';

GO

-- =============================================
-- 方法2：为指定用户生成Sys_UserLogOn记录（示例）
-- =============================================
/*
DECLARE @UserId NVARCHAR(50) = N'BC23888B-95F6-48FF-8519-4461BEA9B0F5';  -- 用户ID
DECLARE @UserSecretkey NVARCHAR(50) = LOWER(SUBSTRING(REPLACE(CAST(NEWID() AS VARCHAR(36)), '-', ''), 1, 16));
DECLARE @DefaultPassword NVARCHAR(100) = N'123456';  -- 默认密码，需要加密

-- 检查用户是否存在
IF EXISTS (SELECT 1 FROM Sys_User WHERE ID = @UserId AND F_DeleteMark = 0 AND F_EnabledMark = 1)
BEGIN
    -- 检查是否已有登录记录
    IF NOT EXISTS (SELECT 1 FROM Sys_UserLogOn WHERE F_UserId = @UserId)
    BEGIN
        INSERT INTO Sys_UserLogOn (
            ID, 
            F_UserId, 
            F_UserSecretkey, 
            F_UserPassword,
            F_LogOnCount,
            F_UserOnLine,
            F_CheckIPAddress,
            F_LastModifyTime,
            F_LastModifyUserId
        )
        VALUES (
            @UserId,
            @UserId,
            @UserSecretkey,
            N'',  -- 密码需要通过应用程序加密后更新
            0,
            0,
            0,
            GETDATE(),
            N'system'
        );
        
        PRINT '已为用户 ' + @UserId + ' 生成Sys_UserLogOn记录';
        PRINT '密钥：' + @UserSecretkey;
        PRINT '注意：F_UserPassword需要通过应用程序更新';
    END
    ELSE
    BEGIN
        PRINT '用户 ' + @UserId + ' 已存在Sys_UserLogOn记录';
    END
END
ELSE
BEGIN
    PRINT '用户 ' + @UserId + ' 不存在或已禁用';
END
*/

-- =============================================
-- 方法3：查询缺少Sys_UserLogOn记录的用户
-- =============================================
/*
SELECT 
    u.ID,
    u.F_Account,
    u.F_RealName,
    u.F_EnabledMark,
    u.F_DeleteMark
FROM Sys_User u
LEFT JOIN Sys_UserLogOn ul ON u.ID = ul.F_UserId
WHERE ul.ID IS NULL
    AND u.F_DeleteMark = 0
    AND u.F_EnabledMark = 1
ORDER BY u.F_Account;
*/

-- =============================================
-- 重要说明：
-- =============================================
-- 1. F_UserSecretkey：已通过SQL生成16位随机密钥
-- 2. F_UserPassword：需要在应用程序中通过以下逻辑生成：
--    - 原始密码（如：123456）
--    - 第一步：Md5(原始密码, 32).ToLower()
--    - 第二步：DESEncrypt.Encrypt(第一步结果, F_UserSecretkey).ToLower()
--    - 第三步：Md5(第二步结果, 32).ToLower()  -> 这就是最终的F_UserPassword
--
-- 3. 建议的更新方式：
--    - 在应用程序中调用用户提供的代码逻辑
--    - 或者创建一个存储过程，通过CLR调用.NET加密函数
--    - 或者批量导出用户ID和密钥，在应用程序中生成密码后批量更新
--
-- 4. 更新密码的SQL示例（需要替换为实际加密后的密码）：
--    UPDATE Sys_UserLogOn 
--    SET F_UserPassword = N'加密后的密码',
--        F_LastModifyTime = GETDATE()
--    WHERE F_UserId = N'用户ID';

GO
