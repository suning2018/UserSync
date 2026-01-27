# 人员数据同步程序

## 项目结构

```
人员同步/
├── Program.cs                          # 主程序入口
├── PersonnelSync.csproj                # 项目文件
├── appsettings.json                    # 配置文件
├── Services/                           # 服务类目录
│   ├── PersonnelSyncService.cs        # 核心同步服务
│   ├── DatabaseService.cs              # 数据库服务
│   └── DatabaseLogService.cs           # 数据库日志服务
├── SQL/                                # SQL脚本目录
│   ├── 01_TableCreation/              # 表创建脚本
│   │   ├── create_personnel_table_sqlserver.sql
│   │   └── insert_personnel_data_sqlserver.sql
│   ├── 02_UserManagement/             # 用户管理脚本
│   │   ├── generate_userlogon.sql
│   │   ├── update_password_simple.sql
│   │   ├── update_userlogon_password.sql
│   │   ├── insert_missing_users.sql
│   │   ├── user.sql
│   │   └── generate_password_update_sql.sql
│   ├── 03_BasePeople/                 # base_people管理脚本
│   │   ├── update_base_people_*.sql
│   │   ├── compare_base_people_*.sql
│   │   └── diagnose_base_people_*.sql
│   ├── 04_DataSync/                   # 数据同步脚本
│   │   ├── update_sysuser_enabledmark.sql
│   │   ├── update_jobstatus_simple.sql
│   │   ├── update_base_people_jobstatus.sql
│   │   ├── update_manufacturegroup_simple.sql
│   │   └── update_mismatched_groups.sql
│   ├── 05_Diagnostic/                 # 诊断比较脚本
│   │   ├── compare_groups_simple.sql
│   │   ├── diagnose_manufacturegroup.sql
│   │   └── missing_data_report.sql
│   └── 06_Others/                     # 其他脚本
│       └── MaterialRequestPermission.sql
├── Docs/                               # 文档目录
│   ├── 人员同步程序使用说明.md
│   └── 用户登录信息生成说明.md
├── Data/                               # 数据源目录
│   └── 人员1.md
└── Archive/                            # 归档目录
    ├── SendMaterialRequestNotice.cs
    └── UpdateUserPasswords.cs
```

## 快速开始

1. **配置数据库连接**
   - 编辑 `appsettings.json`，设置正确的数据库连接字符串

2. **运行程序**
   ```bash
   dotnet run
   ```

3. **查看文档**
   - 详细使用说明：`Docs/人员同步程序使用说明.md`
   - 用户登录信息说明：`Docs/用户登录信息生成说明.md`

## 同步步骤

程序按以下顺序执行同步任务：

1. **同步新用户到 Sys_User** - 从 `vps_empinfo_mes` 同步在制员工到 `Sys_User`
2. **生成 Sys_UserLogOn 记录** - 为缺少登录信息的用户生成登录记录
3. **同步 base_people 记录** - 从 `vps_empinfo_mes` 同步数据到 `base_people`
4. **关联 base_people 与 Sys_User** - 建立关联关系
5. **同步用户启用状态** - 更新 `Sys_User.F_EnabledMark`
6. **同步员工在职状态** - 更新 `base_people.JobStatus`

## SQL脚本说明

### 01_TableCreation - 表创建
- 用于创建和初始化数据库表

### 02_UserManagement - 用户管理
- 用户登录信息生成
- 密码更新
- 用户数据插入

### 03_BasePeople - base_people管理
- base_people 表的数据更新
- 组别信息同步
- 关联关系维护

### 04_DataSync - 数据同步
- 日常数据同步脚本
- 状态更新脚本

### 05_Diagnostic - 诊断比较
- 数据一致性检查
- 差异分析脚本

### 06_Others - 其他
- 其他相关脚本

## 配置说明

在 `appsettings.json` 中可以配置：
- 数据库连接字符串
- 日志设置
- 同步任务启用/禁用
- 检查间隔时间

## 版本历史

- v2.0 (2026-01-21) - 重构为 .NET 8.0 控制台应用程序
- v1.0 (2026-01-21) - 初始版本
