-- 领料单未领完料通知权限表
-- 表名：MaterialRequest_Permission
-- 功能：存储用户与权限级别的映射关系

CREATE TABLE MaterialRequest_Permission (
    ID VARCHAR(50) PRIMARY KEY,                    -- 主键ID
    PermissionLevel INT NOT NULL,                  -- 权限级别：1,2,3,4
    UserIDs NVARCHAR(MAX) NOT NULL,                 -- 用户ID列表，用逗号分隔（如：用户1,用户2,用户3）
    SQLQuery NVARCHAR(MAX) NOT NULL,               -- SQL查询语句
    EnterpriseID VARCHAR(50),                      -- 企业ID
    OrgID VARCHAR(50),                             -- 组织ID
    Description NVARCHAR(500),                     -- 权限级别说明
    IsActive BIT DEFAULT 1,                         -- 是否启用：1-启用，0-禁用
    CreateTime DATETIME DEFAULT GETDATE(),          -- 创建时间
    UpdateTime DATETIME DEFAULT GETDATE(),          -- 更新时间
    Remark NVARCHAR(500)                            -- 备注
);

-- 创建唯一索引（包含权限级别、企业ID和组织ID）
CREATE UNIQUE INDEX IX_MaterialRequest_Permission_Unique ON MaterialRequest_Permission(PermissionLevel, EnterpriseID, OrgID);

-- 创建索引
CREATE INDEX IX_MaterialRequest_Permission_Level ON MaterialRequest_Permission(PermissionLevel);

-- 权限级别说明：
-- 权限1：最近3天的数据，只包含"待领料"状态
-- 权限2：最近7天的数据，包含"待领料"和"部分领料"状态
-- 权限3：最近30天的数据，包含所有未签收状态
-- 权限4：最近90天的数据，包含所有未签收状态

-- 插入各权限级别的配置（包含用户ID和SQL查询）
-- 权限1：当前日期前后15天的未签收领料单，包含存储用户和备注信息
INSERT INTO MaterialRequest_Permission (ID,PermissionLevel,UserIDs,SQLQuery,EnterpriseID,Org,Description,IsActive,adduser,addtime,moduser,modtime,UpdateTime,Remark) VALUES
	 (N'1',1,N'6F4892FA-ED1C-4EA2-965F-1A70CC7064E6,49FC2779-8A39-4B3C-A5FA-53B7CEFBDFB6,
A33093FE-9B6D-425C-9BA6-8207F7E9C800,9EE9855B-D44B-4D87-B602-9A66C24A24EA',N'SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND m.MaterialPreparationStatus <> ''已签收''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限1：当前日期前后15天的未签收领料单，包含存储用户和备注信息',1,NULL,NULL,NULL,NULL,'2026-01-05 16:03:04.070',NULL),
	 (N'2',2,N'
6F4892FA-ED1C-4EA2-965F-1A70CC7064E6,49FC2779-8A39-4B3C-A5FA-53B7CEFBDFB6,
0DD76643-53F0-41BC-A6CC-5F87FCAA95E8,
108E5817-7523-448B-8A54-35A99B3E28F5,
2B3F758B-0F2D-4650-AAAC-CFC500D974BE,
6545C025-5442-4852-B599-A019655AD569,
6CA12609-ACE4-4DB9-8579-F2ED715E8A9B,
B8C2958F-36F2-43BD-A3F7-26B9C9496B1F,
CEAFF5E7-A9AE-488D-9C82-0D72314883E7,
D459C24F-FD0E-4681-8EF4-33E1497C8D27',N'SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND m.MaterialPreparationStatus <> ''已签收''
  AND mm.Remark LIKE ''1%''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限2：当前日期前后15天的未签收领料单，备注以"1"开头，包含存储用户和备注信息',1,NULL,NULL,NULL,NULL,'2026-01-05 16:03:04.070',NULL),
	 (N'3',3,N'
6F4892FA-ED1C-4EA2-965F-1A70CC7064E6,49FC2779-8A39-4B3C-A5FA-53B7CEFBDFB6,
34B5956C-8AA1-4746-9DB6-DA57924AB7C7,
71776AD1-4527-488E-92FD-BA5433037B6C,
84FC33EB-3815-42A3-BC5D-18598CF9C154,
C368B0CF-6C43-4772-994E-91A3765754BF,
C9E042AF-2AA5-41A7-AAB4-C381839ACC55,
D4960AFE-711B-45EC-AC12-045FC1B6D17A',N'SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND m.MaterialPreparationStatus <> ''已签收''
  AND mm.Remark LIKE ''G%''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限3：当前日期前后15天的未签收领料单，备注以"G"开头，包含存储用户和备注信息',1,NULL,NULL,NULL,NULL,'2026-01-05 16:03:04.070',NULL),
	 (N'4',4,N'6F4892FA-ED1C-4EA2-965F-1A70CC7064E6,49FC2779-8A39-4B3C-A5FA-53B7CEFBDFB6',N'SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND( m.MaterialPreparationStatus = ''发料中'' or m.MaterialPreparationStatus is null  )
  AND i.StorgeUser LIKE ''%用户名%''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限4：当前日期前后15天的未签收领料单，存储用户包含特定用户名，包含存储用户和备注信息',1,NULL,NULL,NULL,NULL,'2026-01-05 16:03:04.070',NULL),
	 (N'10',5,N'0FA46108-44C3-4F85-BFBE-D3848B6C2147',N' SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND( m.MaterialPreparationStatus = ''发料中'' or m.MaterialPreparationStatus is null  )
  AND i.StorgeUser LIKE ''%雷云%''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限5：仓储各类',1,NULL,NULL,NULL,NULL,'2026-01-06 15:18:09.070',NULL),
	 (N'11',5,N'066ACA5D-1DD5-4BBF-AEC0-E8980CAECD3A',N' SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND( m.MaterialPreparationStatus = ''发料中'' or m.MaterialPreparationStatus is null  )
  AND i.StorgeUser LIKE ''%鲁文韬%''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限5：仓储各类',1,NULL,NULL,NULL,NULL,'2026-01-06 15:18:09.070',NULL),
	 (N'12',5,N'3BB9A2F5-5130-4900-84C1-E40EE9A7BC6F,
6FAFEC03-F892-4CB3-846B-870F970A775B,
8BB0F4EA-40CE-460A-B567-4D9D2C98A597',N' SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND( m.MaterialPreparationStatus = ''发料中'' or m.MaterialPreparationStatus is null  )
  AND i.StorgeUser LIKE ''%孙艳斌%''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限5：仓储各类',1,NULL,NULL,NULL,NULL,'2026-01-06 15:18:09.073',NULL),
	 (N'13',5,N'9EE9855B-D44B-4D87-B602-9A66C24A24EA',N' SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND( m.MaterialPreparationStatus = ''发料中'' or m.MaterialPreparationStatus is null  )
  AND i.StorgeUser LIKE ''%杨全洲%''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限5：仓储各类',1,NULL,NULL,NULL,NULL,'2026-01-06 15:18:09.073',NULL),
	 (N'14',5,N'02CF6309-1AD3-47DE-B3BF-EAF9F8F821B6',N'
 SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND( m.MaterialPreparationStatus = ''发料中'' or m.MaterialPreparationStatus is null  )
  AND i.StorgeUser LIKE ''%杨鑫敏%''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限5：仓储各类',1,NULL,NULL,NULL,NULL,'2026-01-06 15:18:09.073',NULL),
	 (N'15',5,N'31610370-6CC1-4A6E-88B4-F9F8B2A89DF3',N'
 SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND( m.MaterialPreparationStatus = ''发料中'' or m.MaterialPreparationStatus is null  )
  AND i.StorgeUser LIKE ''%郑涛%''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo
',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限5：仓储各类',1,NULL,NULL,NULL,NULL,'2026-01-06 15:18:09.073',NULL);
INSERT INTO MaterialRequest_Permission (ID,PermissionLevel,UserIDs,SQLQuery,EnterpriseID,Org,Description,IsActive,adduser,addtime,moduser,modtime,UpdateTime,Remark) VALUES
	 (N'5',5,N'3B6CFC34-1FB9-419A-A960-66CC3539272C,
8CEA8B6C-FB19-448B-808D-FBF5BC1632A3,
E7AA45CD-C601-4589-9263-E623A97017E3',N'SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND( m.MaterialPreparationStatus = ''发料中'' or m.MaterialPreparationStatus is null  )
  AND i.StorgeUser LIKE ''%程鑫%''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限5：仓储各类',1,NULL,NULL,NULL,NULL,'2026-01-06 15:18:09.070',NULL),
	 (N'6',5,N'07B8DFF8-78E9-49B0-860D-5E5EC05330E5,
D5ED6938-F1C4-4F62-BE20-0C92224D48F4,
FC72EF14-C6B5-455B-8673-8CE25813F893',N' SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND( m.MaterialPreparationStatus = ''发料中'' or m.MaterialPreparationStatus is null  )
  AND i.StorgeUser LIKE ''%刘德军%''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限5：仓储各类',1,NULL,NULL,NULL,NULL,'2026-01-06 15:18:09.070',NULL),
	 (N'7',5,N'C7536C46-9CD1-484C-95A8-6718CE98EE60,
D326A05F-FA83-4F3F-A07A-0A7DADDCC1F1,
DF2FEC6C-1FD9-448A-A393-5E793875AE1A',N' SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND( m.MaterialPreparationStatus = ''发料中'' or m.MaterialPreparationStatus is null  )
  AND i.StorgeUser LIKE ''%李康%''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限5：仓储各类',1,NULL,NULL,NULL,NULL,'2026-01-06 15:18:09.070',NULL),
	 (N'8',5,N'39E7203C-4123-4BF4-9FB5-86B49BB41277',N' SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND( m.MaterialPreparationStatus = ''发料中'' or m.MaterialPreparationStatus is null  )
  AND i.StorgeUser LIKE ''%黄行航%''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo
',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限5：仓储各类',1,NULL,NULL,NULL,NULL,'2026-01-06 15:18:09.070',NULL),
	 (N'9',5,N'4C19CEAF-B9F9-463E-A88C-3C62A568CF1F',N' SELECT 
    m.SrcDocNo AS [工单号],
    m.DocNo AS [单据号],
    m.MATNR AS [物料编码],
    bim.itemname AS [物料名称],
    bim.ItemSpecs AS [规格型号],
    m.MENGE AS [数量],
    m.MaterialRequisitionDate AS [领料日期],
    m.MaterialPreparationStatus AS [领料状态],
    i.StorgeUser AS [仓管员],
    mm.Remark AS [备注]
FROM MO_MOPickingline m 
LEFT JOIN Base_ItemMaster bim ON m.ItemMaster = bim.id
LEFT JOIN MO_MOPicking mm ON m.MOPicking = mm.id
LEFT JOIN (SELECT DISTINCT i.ItemMasterId, i.StorgeUser FROM Base_ItemMasterGroupLine i WHERE i.StorgeUser IS NOT NULL) i ON m.ItemMaster = i.ItemMasterId
WHERE m.MaterialRequisitionDate IS NOT NULL 
  AND m.MaterialRequisitionDate >= DATEADD(DAY, -15, GETDATE())
  AND m.MaterialRequisitionDate <= DATEADD(DAY, 15, GETDATE())
  AND( m.MaterialPreparationStatus = ''发料中'' or m.MaterialPreparationStatus is null  )
  AND i.StorgeUser LIKE ''%黄杨康%''
ORDER BY m.MaterialRequisitionDate, m.SrcDocNo',N'966B125C-AB2A-4D71-AB77-64368E182809',N'30643314-50A9-477A-808E-55BEC9B00109',N'权限5：仓储各类',1,NULL,NULL,NULL,NULL,'2026-01-06 15:18:09.070',NULL);


