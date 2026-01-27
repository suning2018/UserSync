# 用户数据同步程序 (UserSync)

基于 .NET 8.0 的用户数据同步控制台应用程序，用于将 `vps_empinfo_mes` 表中的数据同步到 `Sys_User` 和 `base_people` 表中，确保数据一致性。

## 项目结构

```
UserSync/
├── Program.cs                          # 主程序入口
├── UserSync.csproj                     # 项目文件
├── UserSync.sln                        # 解决方案文件
├── appsettings.json                    # 应用程序配置
│
├── Services/                           # 服务层
│   ├── PersonnelSyncService.cs        # 核心同步服务
│   ├── DatabaseService.cs              # 数据库服务
│   ├── DatabaseLogService.cs          # 数据库日志服务
│   ├── DbHelper.cs                     # 数据库操作辅助类
│   ├── UserPasswordService.cs          # 用户密码服务
│   └── PasswordEncryptionHelper.cs    # 密码加密辅助类
│
├── Models/                             # 数据模型
│   └── AppSettings.cs                  # 应用程序配置模型
│
├── Extensions/                         # 扩展方法
│   ├── ServiceCollectionExtensions.cs  # 服务注册扩展
│   └── ConfigurationExtensions.cs      # 配置扩展
│
├── Helpers/                            # 辅助类
│   └── ConsoleHelper.cs                 # 控制台输出辅助
│
├── Constants/                          # 常量定义
│   └── ConfigKeys.cs                   # 配置键常量
│
├── SQL/                                # SQL脚本目录
│   ├── 01_TableCreation/              # 表创建脚本
│   ├── 02_UserManagement/             # 用户管理脚本
│   ├── 03_BasePeople/                 # base_people管理脚本
│   ├── 04_DataSync/                   # 数据同步脚本
│   ├── 05_Diagnostic/                 # 诊断比较脚本
│   └── 06_Others/                     # 其他脚本
│
├── Data/                               # 数据文件目录
│   ├── 湖州人员班组信息.xlsx
│   └── 人员1.md
│
├── Docs/                               # 文档目录
│   ├── 人员同步程序使用说明.md
│   ├── 用户登录信息生成说明.md
│   ├── 项目优化说明.md
│   └── 项目结构说明.md
│
└── Archive/                            # 归档目录（已废弃的代码）
    ├── SendMaterialRequestNotice.cs
    └── UpdateUserPasswords.cs
```

> 📖 详细的项目结构说明请查看 [Docs/项目结构说明.md](Docs/项目结构说明.md)

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

## 技术特性

- ✅ **依赖注入**：使用 Microsoft.Extensions.DependencyInjection
- ✅ **强类型配置**：配置模型类，类型安全
- ✅ **优雅关闭**：支持 CancellationToken，优雅处理程序关闭
- ✅ **结构化日志**：使用 Serilog 进行结构化日志记录
- ✅ **数据库日志**：支持将日志记录到数据库
- ✅ **配置验证**：启动时验证配置完整性

## 版本历史

- **v2.1** (2026-01-26) - 项目结构优化，代码重构
  - 引入依赖注入
  - 添加强类型配置模型
  - 优化代码组织结构
  - 添加 CancellationToken 支持
- **v2.0** (2026-01-21) - 重构为 .NET 8.0 控制台应用程序
- **v1.0** (2026-01-21) - 初始版本
