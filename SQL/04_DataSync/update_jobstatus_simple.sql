-- =============================================
-- 更新 base_people 表的 JobStatus 字段（简洁版）
-- JobStatus：0=在制，1=离职
-- vps_empinfo_mes.isactive：0=离职，1=在制
-- =============================================

-- 更新前检查：查看将要更新的记录
SELECT 
    bp.Code,
    bp.Name,
    bp.JobStatus AS CurrentJobStatus,
    CASE 
        WHEN bp.JobStatus = 0 THEN '在制'
        WHEN bp.JobStatus = 1 THEN '离职'
        ELSE 'NULL'
    END AS CurrentStatus,
    ve.isactive AS VpsIsActive,
    CASE 
        WHEN ve.isactive = 0 THEN '离职'
        WHEN ve.isactive = 1 THEN '在制'
    END AS VpsStatus,
    CASE 
        WHEN ve.isactive = 0 THEN 1  -- 离职
        WHEN ve.isactive = 1 THEN 0   -- 在制
    END AS TargetJobStatus,
    CASE 
        WHEN ve.isactive = 0 AND bp.JobStatus <> 1 THEN '需要更新为离职'
        WHEN ve.isactive = 1 AND bp.JobStatus <> 0 THEN '需要更新为在制'
        ELSE '已匹配'
    END AS Status
FROM base_people bp
INNER JOIN vps_empinfo_mes ve ON bp.Code = ve.empcode
WHERE 
    ve.isactive IS NOT NULL
    AND (
        bp.JobStatus IS NULL
        OR (ve.isactive = 0 AND bp.JobStatus <> 1)
        OR (ve.isactive = 1 AND bp.JobStatus <> 0)
    )
ORDER BY bp.Code;

GO

-- =============================================
-- 执行更新：使用 CASE 语句一次性更新
-- =============================================

UPDATE bp
SET bp.JobStatus = CASE 
    WHEN ve.isactive = 0 THEN 1  -- isactive=0（离职）-> JobStatus=1
    WHEN ve.isactive = 1 THEN 0   -- isactive=1（在制）-> JobStatus=0
    ELSE bp.JobStatus  -- 保持原值
END
FROM base_people bp
INNER JOIN vps_empinfo_mes ve ON bp.Code = ve.empcode
WHERE 
    ve.isactive IS NOT NULL
    AND (
        bp.JobStatus IS NULL
        OR (ve.isactive = 0 AND bp.JobStatus <> 1)
        OR (ve.isactive = 1 AND bp.JobStatus <> 0)
    );

PRINT '已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条记录的 JobStatus 字段';

GO

-- =============================================
-- 验证：检查更新结果
-- =============================================

SELECT 
    COUNT(*) AS TotalRecords,
    SUM(CASE WHEN bp.JobStatus = 0 AND ve.isactive = 1 THEN 1 ELSE 0 END) AS MatchedActive,
    SUM(CASE WHEN bp.JobStatus = 1 AND ve.isactive = 0 THEN 1 ELSE 0 END) AS MatchedInactive,
    SUM(CASE WHEN (bp.JobStatus = 0 AND ve.isactive = 0) OR (bp.JobStatus = 1 AND ve.isactive = 1) THEN 1 ELSE 0 END) AS Mismatched,
    '更新后状态统计' AS Description
FROM base_people bp
INNER JOIN vps_empinfo_mes ve ON bp.Code = ve.empcode
WHERE ve.gdname4 = N'湖州制造总部';

GO
