-- =============================================
-- 更新 base_people 表的 SysUser 字段
-- 条件：当 F_Account = Code 时，如果 base_people.SysUser ≠ Sys_User.ID，则更新
-- =============================================

-- 直接更新：当 F_Account = Code 且 SysUser 不一致时更新
UPDATE bp
SET bp.SysUser = u.ID
FROM base_people bp
INNER JOIN Sys_User u ON bp.Code = u.F_Account
WHERE 
    -- 条件：SysUser 为 NULL 或者与 Sys_User.ID 不一致
    (bp.SysUser IS NULL OR bp.SysUser <> u.ID);

PRINT '已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条记录';

GO

-- =============================================
-- 更新前检查：查看需要更新的记录
-- =============================================

-- 检查1：先看看 base_people 中 SysUser 为 NULL 的记录
SELECT 
    COUNT(*) AS NullCount,
    'base_people 中 SysUser 为 NULL 的记录数' AS Description
FROM base_people
WHERE SysUser IS NULL;

-- 检查2：看看 base_people.Code 能匹配到 Sys_User.F_Account 的记录
SELECT 
    COUNT(*) AS MatchedCount,
    'base_people.Code 能匹配到 Sys_User.F_Account 的记录数' AS Description
FROM base_people bp
INNER JOIN Sys_User u ON bp.Code = u.F_Account;

-- 检查3：查看所有匹配的记录（包括已匹配的）
SELECT 
    bp.Code,
    bp.Name,
    bp.SysUser AS CurrentSysUser,
    u.ID AS TargetSysUserId,
    u.F_Account,
    u.F_RealName,
    CASE 
        WHEN bp.SysUser IS NULL THEN 'NULL，需要更新'
        WHEN bp.SysUser <> u.ID THEN '不一致，需要更新'
        WHEN bp.SysUser = u.ID THEN '已匹配'
        ELSE '其他'
    END AS Status
FROM base_people bp
INNER JOIN Sys_User u ON bp.Code = u.F_Account
ORDER BY Status, bp.Code;

-- 检查4：只查看需要更新的记录（NULL 或不一致）
SELECT 
    bp.Code,
    bp.Name,
    bp.SysUser AS CurrentSysUser,
    u.ID AS TargetSysUserId,
    u.F_Account,
    u.F_RealName,
    CASE 
        WHEN bp.SysUser IS NULL THEN 'NULL，需要更新'
        WHEN bp.SysUser <> u.ID THEN '不一致，需要更新'
        ELSE '已匹配'
    END AS Status
FROM base_people bp
INNER JOIN Sys_User u ON bp.Code = u.F_Account
WHERE 
    (bp.SysUser IS NULL OR bp.SysUser <> u.ID);

-- 检查5：如果上面没有结果，检查是否有空格或格式问题
SELECT TOP 20
    bp.Code AS BasePeopleCode,
    u.F_Account AS SysUserAccount,
    CASE 
        WHEN bp.Code = u.F_Account THEN '完全匹配'
        WHEN LTRIM(RTRIM(bp.Code)) = LTRIM(RTRIM(u.F_Account)) THEN '去除空格后匹配'
        ELSE '不匹配'
    END AS MatchType
FROM base_people bp
INNER JOIN Sys_User u ON bp.Code = u.F_Account;

GO
