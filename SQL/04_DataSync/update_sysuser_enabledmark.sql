-- =============================================
-- 更新 Sys_User 表的 F_EnabledMark 字段为 0
-- 条件：F_Account 在 vps_empinfo_mes 中，且 isactive=0
-- =============================================

-- 更新前检查：查看将要更新的记录
SELECT 
    su.ID,
    su.F_Account,
    su.F_RealName,
    su.F_EnabledMark AS CurrentEnabledMark,
    ve.empcode,
    ve.gdname4,
    ve.isactive,
    '需要更新为0' AS Status
FROM Sys_User su
INNER JOIN vps_empinfo_mes ve ON su.F_Account = ve.empcode
WHERE 
    ve.isactive = 0
    AND su.F_EnabledMark <> 0;  -- 只显示当前不是0的记录

GO

-- =============================================
-- 执行更新：将 F_EnabledMark 更新为 0
-- =============================================

UPDATE su
SET su.F_EnabledMark = 0
FROM Sys_User su
INNER JOIN vps_empinfo_mes ve ON su.F_Account = ve.empcode
WHERE 
    ve.isactive = 0;

PRINT '已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条记录的 F_EnabledMark 字段为 0';

GO

-- =============================================
-- 验证：检查更新结果
-- =============================================

-- 检查更新后 F_EnabledMark = 0 的记录数
SELECT 
    COUNT(*) AS UpdatedCount,
    'F_EnabledMark = 0 的记录数' AS Description
FROM Sys_User su
INNER JOIN vps_empinfo_mes ve ON su.F_Account = ve.empcode
WHERE 
    ve.isactive = 0
    AND su.F_EnabledMark = 0;

-- 检查是否还有未更新的记录
SELECT 
    COUNT(*) AS NotUpdatedCount,
    'F_EnabledMark <> 0 的记录数（应该为0）' AS Description
FROM Sys_User su
INNER JOIN vps_empinfo_mes ve ON su.F_Account = ve.empcode
WHERE 
    ve.isactive = 0
    AND su.F_EnabledMark <> 0;

GO

-- =============================================
-- 详细查询：查看所有符合条件的记录（包括已更新和未更新的）
-- =============================================

SELECT 
    su.ID,
    su.F_Account,
    su.F_RealName,
    su.F_EnabledMark,
    ve.empcode,
    ve.gdname4,
    ve.isactive,
    CASE 
        WHEN su.F_EnabledMark = 0 THEN '已更新为0'
        ELSE '未更新（F_EnabledMark=' + CAST(su.F_EnabledMark AS VARCHAR(10)) + '）'
    END AS Status
FROM Sys_User su
INNER JOIN vps_empinfo_mes ve ON su.F_Account = ve.empcode
WHERE 
    ve.isactive = 0
ORDER BY su.F_Account;

GO
