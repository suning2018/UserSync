-- =============================================
-- 比较 base_people 和 personnel 表
-- 找出 Code=employee_id 相同，但 ManufactureGroup 和 group_name 不同的数据
-- =============================================

-- 方法1：使用 LEFT JOIN 比较（推荐）
SELECT 
    p.Code AS BasePeopleCode,
    p.Name AS BasePeopleName,
    bmgf.ManufactureGroup AS BasePeopleGroup,
    per.employee_id AS PersonnelEmployeeId,
    per.name AS PersonnelName,
    per.group_name AS PersonnelGroup,
    CASE 
        WHEN per.employee_id IS NULL THEN 'personnel表中不存在'
        WHEN bmgf.ManufactureGroup IS NULL THEN 'base_people中ManufactureGroup为空'
        WHEN bmgf.ManufactureGroup <> per.group_name THEN '组别不一致'
        ELSE '已匹配'
    END AS Status,
    'base_people组别：' + ISNULL(bmgf.ManufactureGroup, 'NULL') + ' | personnel组别：' + ISNULL(per.group_name, 'NULL') AS GroupDifference
FROM base_people p
LEFT JOIN Base_ManufactureGroupFiles bmgf ON p.ManufactureGroup = bmgf.ID
LEFT JOIN personnel per ON p.Code = per.employee_id
WHERE 
    -- 条件：Code = employee_id 相同，但组别不同
    per.employee_id IS NOT NULL  -- personnel表中存在
    AND (
        -- ManufactureGroup 为空但 group_name 不为空
        (bmgf.ManufactureGroup IS NULL AND per.group_name IS NOT NULL)
        -- 或者 ManufactureGroup 不为空但 group_name 为空
        OR (bmgf.ManufactureGroup IS NOT NULL AND per.group_name IS NULL)
        -- 或者两者都不为空但不相等
        OR (bmgf.ManufactureGroup IS NOT NULL AND per.group_name IS NOT NULL AND bmgf.ManufactureGroup <> per.group_name)
    )
ORDER BY p.Code;

GO

-- =============================================
-- 方法2：使用 INNER JOIN 只显示两边都存在的记录
-- =============================================

SELECT 
    p.Code,
    p.Name AS BasePeopleName,
    bmgf.ManufactureGroup AS BasePeopleGroup,
    per.name AS PersonnelName,
    per.group_name AS PersonnelGroup,
    CASE 
        WHEN bmgf.ManufactureGroup = per.group_name THEN '已匹配'
        WHEN bmgf.ManufactureGroup IS NULL AND per.group_name IS NOT NULL THEN 'base_people组别为空'
        WHEN bmgf.ManufactureGroup IS NOT NULL AND per.group_name IS NULL THEN 'personnel组别为空'
        ELSE '组别不一致'
    END AS Status
FROM base_people p
INNER JOIN Base_ManufactureGroupFiles bmgf ON p.ManufactureGroup = bmgf.ID
INNER JOIN personnel per ON p.Code = per.employee_id
WHERE 
    -- 组别不一致的情况
    bmgf.ManufactureGroup <> per.group_name
    OR (bmgf.ManufactureGroup IS NULL AND per.group_name IS NOT NULL)
    OR (bmgf.ManufactureGroup IS NOT NULL AND per.group_name IS NULL)
ORDER BY p.Code;

GO

-- =============================================
-- 方法3：详细比较（包含所有情况）
-- =============================================

SELECT 
    p.Code,
    p.Name AS BasePeopleName,
    p.ManufactureGroup AS BasePeopleGroupId,
    bmgf.ManufactureGroup AS BasePeopleGroupName,
    per.employee_id,
    per.name AS PersonnelName,
    per.group_name AS PersonnelGroupName,
    CASE 
        WHEN per.employee_id IS NULL THEN 'personnel表中不存在此Code'
        WHEN bmgf.ManufactureGroup IS NULL AND per.group_name IS NULL THEN '两边组别都为空'
        WHEN bmgf.ManufactureGroup IS NULL AND per.group_name IS NOT NULL THEN 'base_people组别为空，personnel有组别'
        WHEN bmgf.ManufactureGroup IS NOT NULL AND per.group_name IS NULL THEN 'base_people有组别，personnel组别为空'
        WHEN bmgf.ManufactureGroup = per.group_name THEN '组别一致'
        WHEN bmgf.ManufactureGroup <> per.group_name THEN '组别不一致'
        ELSE '其他情况'
    END AS ComparisonStatus
FROM base_people p
LEFT JOIN Base_ManufactureGroupFiles bmgf ON p.ManufactureGroup = bmgf.ID
LEFT JOIN personnel per ON p.Code = per.employee_id
WHERE 
    -- 只显示需要关注的情况（组别不一致或为空）
    per.employee_id IS NOT NULL  -- personnel表中存在
    AND (
        bmgf.ManufactureGroup IS NULL 
        OR per.group_name IS NULL 
        OR bmgf.ManufactureGroup <> per.group_name
    )
ORDER BY 
    CASE 
        WHEN bmgf.ManufactureGroup <> per.group_name THEN 1
        WHEN bmgf.ManufactureGroup IS NULL THEN 2
        WHEN per.group_name IS NULL THEN 3
        ELSE 4
    END,
    p.Code;

GO

-- =============================================
-- 方法4：统计信息
-- =============================================

SELECT 
    COUNT(*) AS TotalRecords,
    SUM(CASE WHEN per.employee_id IS NULL THEN 1 ELSE 0 END) AS NotInPersonnel,
    SUM(CASE WHEN per.employee_id IS NOT NULL AND bmgf.ManufactureGroup = per.group_name THEN 1 ELSE 0 END) AS MatchedGroups,
    SUM(CASE WHEN per.employee_id IS NOT NULL AND bmgf.ManufactureGroup <> per.group_name THEN 1 ELSE 0 END) AS MismatchedGroups,
    SUM(CASE WHEN per.employee_id IS NOT NULL AND bmgf.ManufactureGroup IS NULL AND per.group_name IS NOT NULL THEN 1 ELSE 0 END) AS BasePeopleNullGroup,
    SUM(CASE WHEN per.employee_id IS NOT NULL AND bmgf.ManufactureGroup IS NOT NULL AND per.group_name IS NULL THEN 1 ELSE 0 END) AS PersonnelNullGroup
FROM base_people p
LEFT JOIN Base_ManufactureGroupFiles bmgf ON p.ManufactureGroup = bmgf.ID
LEFT JOIN personnel per ON p.Code = per.employee_id;

GO

-- =============================================
-- 方法5：只列出组别不一致的记录（最简洁）
-- =============================================

SELECT 
    p.Code,
    p.Name AS BasePeopleName,
    bmgf.ManufactureGroup AS BasePeopleGroup,
    per.group_name AS PersonnelGroup,
    '组别不一致' AS Status
FROM base_people p
LEFT JOIN Base_ManufactureGroupFiles bmgf ON p.ManufactureGroup = bmgf.ID
INNER JOIN personnel per ON p.Code = per.employee_id
WHERE 
    -- Code = employee_id 相同，但组别不同
    (
        bmgf.ManufactureGroup IS NULL AND per.group_name IS NOT NULL
    )
    OR (
        bmgf.ManufactureGroup IS NOT NULL AND per.group_name IS NULL
    )
    OR (
        bmgf.ManufactureGroup IS NOT NULL 
        AND per.group_name IS NOT NULL 
        AND bmgf.ManufactureGroup <> per.group_name
    )
ORDER BY p.Code;

GO
