-- =============================================
-- 更新 base_people 表的 ManufactureGroup 字段
-- 条件：base_people.ManufactureGroup 为空时，根据 personnel.group_name 
--       在 Base_ManufactureGroupFiles 中查找对应的 ID 并更新
-- =============================================

-- 更新前检查：查看将要更新的记录（包括组别不一致的）
SELECT 
    bp.Code,
    bp.Name AS BasePeopleName,
    bp.ManufactureGroup AS CurrentManufactureGroup,
    bmgf_current.ManufactureGroup AS CurrentManufactureGroupName,
    per.group_name AS PersonnelGroup,
    bmgf.ID AS TargetManufactureGroupId,
    bmgf.ManufactureGroup AS TargetManufactureGroupName,
    CASE 
        WHEN bp.ManufactureGroup IS NULL OR bp.ManufactureGroup = '' THEN 'ManufactureGroup为空，需要更新'
        WHEN bmgf_current.ManufactureGroup <> per.group_name THEN '组别不一致，需要更新'
        ELSE '已匹配'
    END AS Status
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
INNER JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup
LEFT JOIN Base_ManufactureGroupFiles bmgf_current ON bp.ManufactureGroup = bmgf_current.ID
WHERE 
    -- 条件1：ManufactureGroup 为空
    (bp.ManufactureGroup IS NULL OR bp.ManufactureGroup = '')
    -- 条件2：或者 ManufactureGroup 不为空但与 personnel.group_name 不一致
    OR (bp.ManufactureGroup IS NOT NULL AND bp.ManufactureGroup <> '' 
        AND bmgf_current.ManufactureGroup IS NOT NULL 
        AND bmgf_current.ManufactureGroup <> per.group_name)
    AND per.group_name IS NOT NULL;  -- personnel 中有组别名称

GO

-- =============================================
-- 执行更新
-- =============================================

UPDATE bp
SET bp.ManufactureGroup = bmgf.ID
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
INNER JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup
LEFT JOIN Base_ManufactureGroupFiles bmgf_current ON bp.ManufactureGroup = bmgf_current.ID
WHERE 
    -- 条件1：ManufactureGroup 为空
    (bp.ManufactureGroup IS NULL OR bp.ManufactureGroup = '')
    -- 条件2：或者 ManufactureGroup 不为空但与 personnel.group_name 不一致
    OR (bp.ManufactureGroup IS NOT NULL AND bp.ManufactureGroup <> '' 
        AND bmgf_current.ManufactureGroup IS NOT NULL 
        AND bmgf_current.ManufactureGroup <> per.group_name)
    AND per.group_name IS NOT NULL;  -- personnel 中有组别名称

PRINT '已更新 ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' 条记录的 ManufactureGroup 字段';

GO

-- =============================================
-- 检查更新后仍为空的记录（在 Base_ManufactureGroupFiles 中找不到对应组别）
-- =============================================

SELECT 
    bp.Code,
    bp.Name AS BasePeopleName,
    per.group_name AS PersonnelGroup,
    'Base_ManufactureGroupFiles中找不到此组别，无法更新' AS Status
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
LEFT JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup
WHERE 
    (bp.ManufactureGroup IS NULL OR bp.ManufactureGroup = '')  -- 更新后仍为空
    AND per.group_name IS NOT NULL
    AND bmgf.ID IS NULL;  -- Base_ManufactureGroupFiles 中找不到

GO
