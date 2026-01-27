-- =============================================
-- SQL Server 人员信息表创建脚本
-- 表名: personnel
-- 说明: 存储生产科各组人员信息
-- =============================================

-- 如果表已存在，先删除
IF OBJECT_ID('personnel', 'U') IS NOT NULL
    DROP TABLE personnel;
GO

-- 创建人员信息表
CREATE TABLE personnel (
    id INT IDENTITY(1,1) PRIMARY KEY,                    -- 主键ID，自增
    serial_number INT NOT NULL,                          -- 序号
    group_name NVARCHAR(50) NOT NULL,                     -- 组别
    employee_id NVARCHAR(20) NOT NULL,                    -- 工号
    name NVARCHAR(50) NOT NULL,                          -- 姓名
    join_date DATE NULL,                                 -- 入职日期
    created_at DATETIME DEFAULT GETDATE(),               -- 创建时间
    updated_at DATETIME DEFAULT GETDATE()                 -- 更新时间
);
GO

-- 创建索引
CREATE INDEX idx_employee_id ON personnel(employee_id);
CREATE INDEX idx_group_name ON personnel(group_name);
CREATE INDEX idx_name ON personnel(name);
GO

-- 添加表注释
EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'生产科各组人员信息表', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'personnel';
GO

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'主键ID', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'personnel', 
    @level2type = N'COLUMN', @level2name = N'id';
GO

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'序号', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'personnel', 
    @level2type = N'COLUMN', @level2name = N'serial_number';
GO

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'组别', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'personnel', 
    @level2type = N'COLUMN', @level2name = N'group_name';
GO

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'工号', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'personnel', 
    @level2type = N'COLUMN', @level2name = N'employee_id';
GO

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'姓名', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'personnel', 
    @level2type = N'COLUMN', @level2name = N'name';
GO

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'入职日期', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'personnel', 
    @level2type = N'COLUMN', @level2name = N'join_date';
GO

PRINT '人员信息表创建成功！';
GO
