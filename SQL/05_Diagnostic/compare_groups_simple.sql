-- =============================================
-- 比较 base_people 和 personnel 表的组别差异
-- 条件：Code = employee_id 相同，但 ManufactureGroup ≠ group_name
-- =============================================

-- 核心查询：列出组别不一致的记录
SELECT 
    p.Code,
    p.Name AS BasePeopleName,
    bmgf.ManufactureGroup AS BasePeopleGroup,
    per.employee_id,
    per.name AS PersonnelName,
    per.group_name AS PersonnelGroup,
    CASE 
        WHEN bmgf.ManufactureGroup IS NULL AND per.group_name IS NOT NULL THEN 'base_people组别为空'
        WHEN bmgf.ManufactureGroup IS NOT NULL AND per.group_name IS NULL THEN 'personnel组别为空'
        WHEN bmgf.ManufactureGroup <> per.group_name THEN '组别不一致'
    END AS Difference
FROM base_people p
LEFT JOIN Base_ManufactureGroupFiles bmgf ON p.ManufactureGroup = bmgf.ID
INNER JOIN personnel per ON p.Code = per.employee_id
WHERE 
    -- Code = employee_id 相同，但组别不同
    (
        -- 情况1：base_people组别为空，personnel有组别
        (bmgf.ManufactureGroup IS NULL AND per.group_name IS NOT NULL)
        -- 情况2：base_people有组别，personnel组别为空
        OR (bmgf.ManufactureGroup IS NOT NULL AND per.group_name IS NULL)
        -- 情况3：两者都有组别但不相等
        OR (bmgf.ManufactureGroup IS NOT NULL AND per.group_name IS NOT NULL AND bmgf.ManufactureGroup <> per.group_name)
    )
ORDER BY p.Code;

GO

-- =============================================
-- 统计：组别不一致的数量
-- =============================================

SELECT 
    COUNT(*) AS MismatchedCount,
    '组别不一致的记录数' AS Description
FROM base_people p
LEFT JOIN Base_ManufactureGroupFiles bmgf ON p.ManufactureGroup = bmgf.ID
INNER JOIN personnel per ON p.Code = per.employee_id
WHERE 
    (bmgf.ManufactureGroup IS NULL AND per.group_name IS NOT NULL)
    OR (bmgf.ManufactureGroup IS NOT NULL AND per.group_name IS NULL)
    OR (bmgf.ManufactureGroup IS NOT NULL AND per.group_name IS NOT NULL AND bmgf.ManufactureGroup <> per.group_name);

GO
