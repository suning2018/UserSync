-- =============================================
-- 诊断 base_people.ManufactureGroup 更新问题
-- =============================================

-- 步骤1：检查 base_people 中 ManufactureGroup 为空（NULL 或空字符串）的记录数
SELECT 
    COUNT(*) AS NullOrEmptyCount,
    'base_people 中 ManufactureGroup 为 NULL 或空字符串的记录数' AS Description
FROM base_people
WHERE ManufactureGroup IS NULL OR ManufactureGroup = '';

-- 步骤2：检查 base_people.Code 和 personnel.employee_id 的匹配情况
SELECT 
    COUNT(*) AS MatchedCodeCount,
    'base_people.Code 能匹配到 personnel.employee_id 的记录数' AS Description
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id;

-- 步骤3：检查 personnel.group_name 和 Base_ManufactureGroupFiles.ManufactureGroup 的匹配情况
SELECT 
    COUNT(*) AS MatchedGroupCount,
    'personnel.group_name 能匹配到 Base_ManufactureGroupFiles.ManufactureGroup 的记录数' AS Description
FROM personnel per
INNER JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup;

-- 步骤4：检查完整的匹配链（base_people -> personnel -> Base_ManufactureGroupFiles）
SELECT 
    COUNT(*) AS FullMatchCount,
    '完整匹配链的记录数（Code=employee_id 且 group_name=ManufactureGroup）' AS Description
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
INNER JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup;

-- 步骤5：查看 base_people 中 ManufactureGroup 为空且能匹配到完整链的记录
SELECT 
    bp.Code,
    bp.Name AS BasePeopleName,
    bp.ManufactureGroup AS CurrentManufactureGroup,
    per.employee_id,
    per.group_name AS PersonnelGroup,
    bmgf.ID AS TargetManufactureGroupId,
    bmgf.ManufactureGroup AS TargetManufactureGroupName
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
INNER JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup
WHERE 
    (bp.ManufactureGroup IS NULL OR bp.ManufactureGroup = '')
    AND per.group_name IS NOT NULL;

-- 步骤6：检查 base_people.Code 在 personnel 中找不到匹配的记录
SELECT 
    bp.Code,
    bp.Name,
    bp.ManufactureGroup,
    '在 personnel 中找不到匹配的 employee_id' AS Status
FROM base_people bp
LEFT JOIN personnel per ON bp.Code = per.employee_id
WHERE per.employee_id IS NULL
    AND (bp.ManufactureGroup IS NULL OR bp.ManufactureGroup = '')
ORDER BY bp.Code;

-- 步骤7：检查 personnel.group_name 在 Base_ManufactureGroupFiles 中找不到匹配的记录
SELECT DISTINCT
    per.group_name AS PersonnelGroup,
    '在 Base_ManufactureGroupFiles 中找不到匹配的 ManufactureGroup' AS Status
FROM base_people bp
INNER JOIN personnel per ON bp.Code = per.employee_id
LEFT JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup
WHERE 
    (bp.ManufactureGroup IS NULL OR bp.ManufactureGroup = '')
    AND per.group_name IS NOT NULL
    AND bmgf.ID IS NULL
ORDER BY per.group_name;

-- 步骤8：查看 Base_ManufactureGroupFiles 中所有可用的组别名称
SELECT 
    ID,
    ManufactureGroup,
    '可用组别' AS Description
FROM Base_ManufactureGroupFiles
ORDER BY ManufactureGroup;

-- 步骤9：查看 personnel 中所有不同的组别名称
SELECT DISTINCT
    group_name AS PersonnelGroupName,
    COUNT(*) AS Count,
    'personnel 中的组别名称' AS Description
FROM personnel
WHERE group_name IS NOT NULL
GROUP BY group_name
ORDER BY group_name;

-- 步骤10：对比 personnel.group_name 和 Base_ManufactureGroupFiles.ManufactureGroup
SELECT DISTINCT
    per.group_name AS PersonnelGroup,
    CASE 
        WHEN bmgf.ManufactureGroup IS NOT NULL THEN '能找到匹配'
        ELSE '找不到匹配'
    END AS MatchStatus,
    bmgf.ManufactureGroup AS BaseManufactureGroup,
    bmgf.ID AS BaseManufactureGroupId
FROM personnel per
LEFT JOIN Base_ManufactureGroupFiles bmgf ON per.group_name = bmgf.ManufactureGroup
WHERE per.group_name IS NOT NULL
ORDER BY per.group_name;

GO
