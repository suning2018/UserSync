-- =============================================
-- 更新 base_people 表的 SysUser 字段
-- 说明：通过 F_Account 和 Code 匹配，更新 base_people.SysUser = Sys_User.ID
-- =============================================

-- 方法1：更新 base_people 中 SysUser 为 NULL 的记录
UPDATE bp
SET 
    bp.SysUser = u.ID,
    bp.[修改时间或其他字段] = GETDATE()  -- 如果有修改时间字段，请取消注释并修改字段名
FROM base_people bp
INNER JOIN Sys_User u ON bp.Code = u.F_Account
WHERE bp.SysUser IS NULL
;

PRINT '已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条记录的 SysUser 字段（从 NULL 更新）';

GO

-- =============================================
-- 方法2：更新 base_people 中 SysUser 与 Sys_User.ID 不一致的记录
-- =============================================

UPDATE bp
SET 
    bp.SysUser = u.ID,
    bp.[修改时间或其他字段] = GETDATE()  -- 如果有修改时间字段，请取消注释并修改字段名
FROM base_people bp
INNER JOIN Sys_User u ON bp.Code = u.F_Account
WHERE bp.SysUser IS NULL  -- SysUser 为 NULL
    OR bp.SysUser <> u.ID  -- 或者 SysUser 与 Sys_User.ID 不一致
;

PRINT '已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条记录的 SysUser 字段（包括 NULL 和不一致的情况）';

GO

-- =============================================
-- 方法3：只更新不一致的记录（不包括 NULL）
-- =============================================

UPDATE bp
SET 
    bp.SysUser = u.ID,
    bp.[修改时间或其他字段] = GETDATE()  -- 如果有修改时间字段，请取消注释并修改字段名
FROM base_people bp
INNER JOIN Sys_User u ON bp.Code = u.F_Account
WHERE bp.SysUser IS NOT NULL  -- SysUser 不为 NULL
    AND bp.SysUser <> u.ID    -- 但 SysUser 与 Sys_User.ID 不一致
;

PRINT '已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条记录的 SysUser 字段（仅不一致的记录）';

GO

-- =============================================
-- 方法4：完整的更新逻辑（推荐使用）
-- 更新条件：当 F_Account = Code 时，如果 base_people.SysUser 和 Sys_User.ID 不一致就更新
-- =============================================

UPDATE bp
SET 
    bp.SysUser = u.ID
    -- 如果有其他需要更新的字段，可以在这里添加
    -- bp.ModifyTime = GETDATE(),
    -- bp.ModifyUser = N'system'
FROM base_people bp
INNER JOIN Sys_User u ON bp.Code = u.F_Account
WHERE 
    -- 条件1：SysUser 为 NULL，需要更新
    (bp.SysUser IS NULL)
    -- 条件2：或者 SysUser 不为 NULL 但与 Sys_User.ID 不一致，需要更新
    OR (bp.SysUser IS NOT NULL AND bp.SysUser <> u.ID)
    -- 只更新有效的用户
;

PRINT '已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条记录的 SysUser 字段';

GO

-- =============================================
-- 查询：检查更新前的数据情况
-- =============================================

-- 查询 base_people 中 SysUser 为 NULL 的记录
SELECT 
    bp.Code,
    bp.Name AS BasePeopleName,
    bp.SysUser AS CurrentSysUser,
    u.ID AS SysUserId,
    u.F_Account,
    u.F_RealName AS SysUserName,
    CASE 
        WHEN bp.SysUser IS NULL THEN '需要更新（NULL）'
        WHEN bp.SysUser <> u.ID THEN '需要更新（不一致）'
        ELSE '已匹配'
    END AS Status
FROM base_people bp
LEFT JOIN Sys_User u ON bp.Code = u.F_Account
WHERE bp.SysUser IS NULL
    OR (u.ID IS NOT NULL AND bp.SysUser <> u.ID)
ORDER BY bp.Code;

GO

-- =============================================
-- 查询：检查更新后的数据情况
-- =============================================

-- 验证更新结果
SELECT 
    COUNT(*) AS TotalRecords,
    SUM(CASE WHEN bp.SysUser IS NULL THEN 1 ELSE 0 END) AS NullCount,
    SUM(CASE WHEN bp.SysUser IS NOT NULL AND bp.SysUser = u.ID THEN 1 ELSE 0 END) AS MatchedCount,
    SUM(CASE WHEN bp.SysUser IS NOT NULL AND bp.SysUser <> u.ID THEN 1 ELSE 0 END) AS MismatchedCount
FROM base_people bp
LEFT JOIN Sys_User u ON bp.Code = u.F_Account;

GO

-- =============================================
-- 查询：查找 base_people 中有 Code 但在 Sys_User 中找不到 F_Account 的记录
-- =============================================

SELECT 
    bp.Code,
    bp.Name,
    bp.SysUser
FROM base_people bp
LEFT JOIN Sys_User u ON bp.Code = u.F_Account
WHERE u.ID IS NULL  -- 在 Sys_User 中找不到匹配的记录
ORDER BY bp.Code;

PRINT '以上记录在 Sys_User 表中找不到对应的 F_Account，无法更新 SysUser';

GO
