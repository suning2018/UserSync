-- =============================================
-- 更新 base_people 中组别不一致的记录
-- 条件：base_people.ManufactureGroup 不为空，但与 personnel.group_name 不一致
-- =============================================

-- 更新前检查：查看组别不一致的记录（排除"自动化光机组"和"光机主轴箱组"）
SELECT 
    bp.Code,
    bp.Name AS BasePeopleName,
    bp.ManufactureGroup AS CurrentManufactureGroupId,
    bmgf_current.ManufactureGroup AS CurrentManufactureGroupName,
    per.employee_id,
    per.name AS PersonnelName,
    per.group_name AS PersonnelGroup,
    bmgf.ID AS TargetManufactureGroupId,
    bmgf.ManufactureGroup AS TargetManufactureGroupName,
    '组别不一致，需要更新' AS Status
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
INNER JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup
LEFT JOIN Base_ManufactureGroupFiles bmgf_current ON bp.ManufactureGroup = bmgf_current.ID
WHERE 
    bp.ManufactureGroup IS NOT NULL  -- base_people 中 ManufactureGroup 不为空
    AND bp.ManufactureGroup <> ''    -- 且不是空字符串
    AND bmgf_current.ManufactureGroup IS NOT NULL  -- 当前组别名称存在
    AND bmgf_current.ManufactureGroup <> per.group_name  -- 但与 personnel.group_name 不一致
    AND per.group_name IS NOT NULL  -- personnel 中有组别名称
    -- 排除"自动化光机组"和"光机主轴箱组"，这两个组别保持不变
    AND bmgf_current.ManufactureGroup NOT IN (N'自动化光机组', N'光机主轴箱组')
    AND per.group_name NOT IN (N'自动化光机组', N'光机主轴箱组');

GO

-- =============================================
-- 执行更新：更新组别不一致的记录（排除"自动化光机组"和"光机主轴箱组"）
-- =============================================

UPDATE bp
SET bp.ManufactureGroup = bmgf.ID
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
INNER JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup
LEFT JOIN Base_ManufactureGroupFiles bmgf_current ON bp.ManufactureGroup = bmgf_current.ID
WHERE 
    bp.ManufactureGroup IS NOT NULL  -- base_people 中 ManufactureGroup 不为空
    AND bp.ManufactureGroup <> ''    -- 且不是空字符串
    AND bmgf_current.ManufactureGroup IS NOT NULL  -- 当前组别名称存在
    AND bmgf_current.ManufactureGroup <> per.group_name  -- 但与 personnel.group_name 不一致
    AND per.group_name IS NOT NULL  -- personnel 中有组别名称
    -- 排除"自动化光机组"和"光机主轴箱组"，这两个组别保持不变
    AND bmgf_current.ManufactureGroup NOT IN (N'自动化光机组', N'光机主轴箱组')
    AND per.group_name NOT IN (N'自动化光机组', N'光机主轴箱组');

PRINT '已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条组别不一致的记录';

GO

-- =============================================
-- 验证：检查更新后是否还有组别不一致的记录（排除"自动化光机组"和"光机主轴箱组"）
-- =============================================

SELECT 
    bp.Code,
    bp.Name AS BasePeopleName,
    bmgf_current.ManufactureGroup AS CurrentManufactureGroupName,
    per.group_name AS PersonnelGroup,
    '更新后仍不一致' AS Status
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
LEFT JOIN Base_ManufactureGroupFiles bmgf_current ON bp.ManufactureGroup = bmgf_current.ID
WHERE 
    bp.ManufactureGroup IS NOT NULL
    AND bp.ManufactureGroup <> ''
    AND bmgf_current.ManufactureGroup IS NOT NULL
    AND bmgf_current.ManufactureGroup <> per.group_name
    AND per.group_name IS NOT NULL
    -- 排除"自动化光机组"和"光机主轴箱组"，这两个组别保持不变
    AND bmgf_current.ManufactureGroup NOT IN (N'自动化光机组', N'光机主轴箱组')
    AND per.group_name NOT IN (N'自动化光机组', N'光机主轴箱组');

GO
