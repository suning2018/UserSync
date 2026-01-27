-- =============================================
-- 更新 base_people 表的 ManufactureGroup 字段
-- 说明：当 base_people.ManufactureGroup 为空时，根据 personnel.group_name 
--       在 Base_ManufactureGroupFiles 中查找对应的 ID 并更新
-- =============================================

-- 方法1：直接更新 base_people 中 ManufactureGroup 为空的记录
UPDATE bp
SET bp.ManufactureGroup = bmgf.ID
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
INNER JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup
WHERE 
    bp.ManufactureGroup IS NULL  -- base_people 中 ManufactureGroup 为空
    AND per.group_name IS NOT NULL  -- personnel 中有组别名称
    AND bmgf.ID IS NOT NULL;  -- Base_ManufactureGroupFiles 中存在对应的组别

PRINT '已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条记录的 ManufactureGroup 字段';

GO

-- =============================================
-- 方法2：更新前检查（查看将要更新的记录）
-- =============================================

SELECT 
    bp.Code,
    bp.Name AS BasePeopleName,
    bp.ManufactureGroup AS CurrentManufactureGroup,
    per.employee_id,
    per.name AS PersonnelName,
    per.group_name AS PersonnelGroup,
    bmgf.ID AS TargetManufactureGroupId,
    bmgf.ManufactureGroup AS TargetManufactureGroupName,
    CASE 
        WHEN bp.ManufactureGroup IS NULL AND bmgf.ID IS NOT NULL THEN '需要更新'
        WHEN bp.ManufactureGroup IS NOT NULL THEN '已有组别，不更新'
        WHEN bmgf.ID IS NULL THEN 'Base_ManufactureGroupFiles中找不到对应组别'
        ELSE '其他情况'
    END AS Status
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
LEFT JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup
WHERE 
    bp.ManufactureGroup IS NULL  -- base_people 中 ManufactureGroup 为空
    AND per.group_name IS NOT NULL  -- personnel 中有组别名称
ORDER BY bp.Code;

GO

-- =============================================
-- 方法3：检查 Base_ManufactureGroupFiles 中找不到对应组别的情况
-- =============================================

SELECT 
    bp.Code,
    bp.Name AS BasePeopleName,
    per.group_name AS PersonnelGroup,
    'Base_ManufactureGroupFiles中找不到此组别' AS Status
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
LEFT JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup
WHERE 
    bp.ManufactureGroup IS NULL  -- base_people 中 ManufactureGroup 为空
    AND per.group_name IS NOT NULL  -- personnel 中有组别名称
    AND bmgf.ID IS NULL;  -- Base_ManufactureGroupFiles 中找不到对应的组别

PRINT '以上记录在 Base_ManufactureGroupFiles 中找不到对应的组别，无法更新';

GO

-- =============================================
-- 方法4：查看 Base_ManufactureGroupFiles 中所有可用的组别
-- =============================================

SELECT 
    ID,
    ManufactureGroup,
    '可用组别' AS Description
FROM Base_ManufactureGroupFiles
ORDER BY ManufactureGroup;

GO

-- =============================================
-- 方法5：统计信息
-- =============================================

SELECT 
    COUNT(*) AS TotalNullManufactureGroup,
    SUM(CASE WHEN bmgf.ID IS NOT NULL THEN 1 ELSE 0 END) AS CanUpdate,
    SUM(CASE WHEN bmgf.ID IS NULL THEN 1 ELSE 0 END) AS CannotUpdate,
    'base_people中ManufactureGroup为空的记录统计' AS Description
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
LEFT JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup
WHERE 
    bp.ManufactureGroup IS NULL
    AND per.group_name IS NOT NULL;

GO

-- =============================================
-- 方法6：完整的更新逻辑（包括验证）
-- =============================================

-- 步骤1：先查看将要更新的记录
SELECT 
    bp.Code,
    bp.Name,
    per.group_name,
    bmgf.ID AS ManufactureGroupId,
    bmgf.ManufactureGroup AS ManufactureGroupName
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
INNER JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup
WHERE bp.ManufactureGroup IS NULL
    AND per.group_name IS NOT NULL
ORDER BY bp.Code;

-- 步骤2：执行更新（取消注释以执行）
/*
UPDATE bp
SET bp.ManufactureGroup = bmgf.ID
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
INNER JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup
WHERE 
    bp.ManufactureGroup IS NULL
    AND per.group_name IS NOT NULL
    AND bmgf.ID IS NOT NULL;

PRINT '已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条记录';
*/

-- 步骤3：验证更新结果
SELECT 
    COUNT(*) AS RemainingNullCount,
    '更新后仍为空的ManufactureGroup记录数' AS Description
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
WHERE bp.ManufactureGroup IS NULL
    AND per.group_name IS NOT NULL;

GO
