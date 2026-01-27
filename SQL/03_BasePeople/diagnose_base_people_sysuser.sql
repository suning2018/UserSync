-- =============================================
-- 诊断 base_people 和 Sys_User 的匹配问题
-- =============================================

-- 步骤1：检查 base_people 中 SysUser 为 NULL 的记录数量
SELECT 
    COUNT(*) AS NullSysUserCount,
    'base_people 中 SysUser 为 NULL 的记录数' AS Description
FROM base_people
WHERE SysUser IS NULL;

GO

-- 步骤2：检查 base_people 中有多少条记录
SELECT 
    COUNT(*) AS TotalBasePeopleCount,
    'base_people 总记录数' AS Description
FROM base_people;

GO

-- 步骤3：检查 Sys_User 中有多少条有效记录
SELECT 
    COUNT(*) AS ValidSysUserCount,
    'Sys_User 中有效用户数（未删除且启用）' AS Description
FROM Sys_User
WHERE F_DeleteMark = 0
    AND F_EnabledMark = 1;

GO

-- 步骤4：检查 base_people.Code 和 Sys_User.F_Account 的匹配情况
SELECT 
    COUNT(*) AS MatchedCount,
    'base_people.Code 能匹配到 Sys_User.F_Account 的记录数' AS Description
FROM base_people bp
INNER JOIN Sys_User u ON bp.Code = u.F_Account
;

GO

-- 步骤5：检查 base_people.Code 在 Sys_User 中找不到匹配的记录
SELECT 
    bp.Code,
    bp.Name,
    bp.SysUser,
    '在 Sys_User 中找不到匹配的 F_Account' AS Status
FROM base_people bp
LEFT JOIN Sys_User u ON bp.Code = u.F_Account
WHERE u.ID IS NULL
ORDER BY bp.Code;

GO

-- 步骤6：检查 Sys_User.F_Account 在 base_people 中找不到匹配的记录
SELECT 
    u.F_Account,
    u.F_RealName,
    u.ID,
    '在 base_people 中找不到匹配的 Code' AS Status
FROM Sys_User u
LEFT JOIN base_people bp ON u.F_Account = bp.Code
WHERE bp.Code IS NULL
ORDER BY u.F_Account;

GO

-- 步骤7：检查 base_people.Code 和 Sys_User.F_Account 的数据类型和格式
SELECT TOP 10
    bp.Code AS BasePeopleCode,
    LEN(bp.Code) AS CodeLength,
    DATALENGTH(bp.Code) AS CodeDataLength,
    u.F_Account AS SysUserAccount,
    LEN(u.F_Account) AS AccountLength,
    DATALENGTH(u.F_Account) AS AccountDataLength,
    CASE 
        WHEN bp.Code = u.F_Account THEN '匹配'
        WHEN LTRIM(RTRIM(bp.Code)) = LTRIM(RTRIM(u.F_Account)) THEN '去除空格后匹配'
        ELSE '不匹配'
    END AS MatchStatus
FROM base_people bp
INNER JOIN Sys_User u ON bp.Code = u.F_Account
;

GO

-- 步骤8：检查 base_people 中 SysUser 不为 NULL 但与 Sys_User.ID 不一致的记录
SELECT 
    bp.Code,
    bp.Name,
    bp.SysUser AS BasePeopleSysUser,
    u.ID AS SysUserId,
    u.F_Account,
    u.F_RealName,
    CASE 
        WHEN bp.SysUser = u.ID THEN '已匹配'
        ELSE '不一致'
    END AS Status
FROM base_people bp
INNER JOIN Sys_User u ON bp.Code = u.F_Account
WHERE bp.SysUser IS NOT NULL
    AND bp.SysUser <> u.ID
;

GO

-- 步骤9：完整的匹配检查（包括所有情况）
SELECT 
    bp.Code,
    bp.Name,
    bp.SysUser AS CurrentSysUser,
    u.ID AS TargetSysUserId,
    u.F_Account,
    u.F_RealName,
    CASE 
        WHEN u.ID IS NULL THEN 'base_people.Code 在 Sys_User 中找不到匹配'
        WHEN bp.SysUser IS NULL THEN 'NULL，需要更新'
        WHEN bp.SysUser <> u.ID THEN '不一致，需要更新'
        WHEN bp.SysUser = u.ID THEN '已匹配'
        ELSE '其他情况'
    END AS Status
FROM base_people bp
LEFT JOIN Sys_User u ON bp.Code = u.F_Account
ORDER BY 
    CASE 
        WHEN u.ID IS NULL THEN 1
        WHEN bp.SysUser IS NULL THEN 2
        WHEN bp.SysUser <> u.ID THEN 3
        ELSE 4
    END,
    bp.Code;

GO

-- 步骤10：尝试使用 TRIM 去除空格进行匹配（如果数据中有空格）
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
INNER JOIN Sys_User u ON LTRIM(RTRIM(bp.Code)) = LTRIM(RTRIM(u.F_Account))
WHERE 
    (bp.SysUser IS NULL OR bp.SysUser <> u.ID)
;

GO
