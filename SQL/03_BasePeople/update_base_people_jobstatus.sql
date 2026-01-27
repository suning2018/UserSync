-- =============================================
-- 更新 base_people 表的 JobStatus 字段
-- 说明：根据 vps_empinfo_mes.isactive 更新 JobStatus
-- JobStatus：0=在制，1=离职
-- vps_empinfo_mes.isactive：0=离职，1=在制
-- =============================================

-- 更新前检查：查看将要更新的记录
SELECT 
    bp.Code,
    bp.Name AS BasePeopleName,
    bp.JobStatus AS CurrentJobStatus,
    CASE 
        WHEN bp.JobStatus = 0 THEN '在制'
        WHEN bp.JobStatus = 1 THEN '离职'
        ELSE '未知状态'
    END AS CurrentStatusName,
    ve.empcode,
    ve.isactive AS VpsIsActive,
    CASE 
        WHEN ve.isactive = 0 THEN '离职'
        WHEN ve.isactive = 1 THEN '在制'
        ELSE '未知'
    END AS VpsStatusName,
    CASE 
        WHEN ve.isactive = 0 THEN 1  -- isactive=0 对应 JobStatus=1（离职）
        WHEN ve.isactive = 1 THEN 0  -- isactive=1 对应 JobStatus=0（在制）
        ELSE NULL
    END AS TargetJobStatus,
    CASE 
        WHEN bp.JobStatus IS NULL AND ve.isactive IS NOT NULL THEN '需要更新（当前为NULL）'
        WHEN ve.isactive = 0 AND bp.JobStatus <> 1 THEN '需要更新为离职（1）'
        WHEN ve.isactive = 1 AND bp.JobStatus <> 0 THEN '需要更新为在制（0）'
        WHEN ve.isactive = 0 AND bp.JobStatus = 1 THEN '已匹配（离职）'
        WHEN ve.isactive = 1 AND bp.JobStatus = 0 THEN '已匹配（在制）'
        ELSE '其他情况'
    END AS Status
FROM base_people bp
INNER JOIN vps_empinfo_mes ve ON bp.Code = ve.empcode
WHERE 
    (
        -- 需要更新的情况
        bp.JobStatus IS NULL
        OR (ve.isactive = 0 AND bp.JobStatus <> 1)
        OR (ve.isactive = 1 AND bp.JobStatus <> 0)
    )
ORDER BY bp.Code;

GO

-- =============================================
-- 执行更新：根据 vps_empinfo_mes.isactive 更新 JobStatus
-- =============================================

-- 更新为离职（isactive=0 -> JobStatus=1）
UPDATE bp
SET bp.JobStatus = 1
FROM base_people bp
INNER JOIN vps_empinfo_mes ve ON bp.Code = ve.empcode
WHERE 
    ve.isactive = 0  -- 离职
    AND (bp.JobStatus IS NULL OR bp.JobStatus <> 1);  -- 当前不是离职状态

PRINT '已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条记录为离职状态（JobStatus=1）';

-- 更新为在制（isactive=1 -> JobStatus=0）
UPDATE bp
SET bp.JobStatus = 0
FROM base_people bp
INNER JOIN vps_empinfo_mes ve ON bp.Code = ve.empcode
WHERE 
    ve.isactive = 1  -- 在制
    AND (bp.JobStatus IS NULL OR bp.JobStatus <> 0);  -- 当前不是在制状态

PRINT '已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条记录为在制状态（JobStatus=0）';

GO

-- =============================================
-- 方法2：使用 CASE 语句一次性更新（推荐）
-- =============================================

UPDATE bp
SET bp.JobStatus = CASE 
    WHEN ve.isactive = 0 THEN 1  -- isactive=0 对应 JobStatus=1（离职）
    WHEN ve.isactive = 1 THEN 0  -- isactive=1 对应 JobStatus=0（在制）
    ELSE bp.JobStatus  -- 保持原值
END
FROM base_people bp
INNER JOIN vps_empinfo_mes ve ON bp.Code = ve.empcode
WHERE 
    ve.gdname4 = N'湖州制造部'
    AND ve.isactive IS NOT NULL
    AND (
        -- 需要更新的情况
        bp.JobStatus IS NULL
        OR (ve.isactive = 0 AND bp.JobStatus <> 1)
        OR (ve.isactive = 1 AND bp.JobStatus <> 0)
    );

PRINT '已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条记录的 JobStatus 字段';

GO

-- =============================================
-- 验证：检查更新结果
-- =============================================

-- 统计更新后的状态分布
SELECT 
    bp.JobStatus,
    CASE 
        WHEN bp.JobStatus = 0 THEN '在制'
        WHEN bp.JobStatus = 1 THEN '离职'
        ELSE '未知'
    END AS StatusName,
    ve.isactive AS VpsIsActive,
    COUNT(*) AS Count
FROM base_people bp
INNER JOIN vps_empinfo_mes ve ON bp.Code = ve.empcode
GROUP BY bp.JobStatus, ve.isactive
ORDER BY bp.JobStatus, ve.isactive;

-- 检查是否还有不一致的记录
SELECT 
    bp.Code,
    bp.Name,
    bp.JobStatus AS CurrentJobStatus,
    CASE 
        WHEN bp.JobStatus = 0 THEN '在制'
        WHEN bp.JobStatus = 1 THEN '离职'
        ELSE '未知'
    END AS CurrentStatusName,
    ve.isactive AS VpsIsActive,
    CASE 
        WHEN ve.isactive = 0 THEN '离职'
        WHEN ve.isactive = 1 THEN '在制'
        ELSE '未知'
    END AS VpsStatusName,
    CASE 
        WHEN ve.isactive = 0 AND bp.JobStatus <> 1 THEN '不一致（应为离职）'
        WHEN ve.isactive = 1 AND bp.JobStatus <> 0 THEN '不一致（应为在制）'
        ELSE '已匹配'
    END AS Status
FROM base_people bp
INNER JOIN vps_empinfo_mes ve ON bp.Code = ve.empcode
WHERE 
    ve.gdname4 = N'湖州制造部'
    AND ve.isactive IS NOT NULL
    AND (
        (ve.isactive = 0 AND bp.JobStatus <> 1)
        OR (ve.isactive = 1 AND bp.JobStatus <> 0)
    )
ORDER BY bp.Code;

GO
