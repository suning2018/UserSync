# 用户数据同步程序 (UserSync)

基于 .NET 8.0 的 **Windows 服务**，将 `vps_empinfo_mes` 视图中指定制造部的人员数据同步到 `Sys_User`、`base_people` 等表，并配合 Serilog 文件日志与可选数据库日志。

## 运行形态

| 方式 | 说明 |
|------|------|
| **Windows 服务（推荐）** | 使用 `Host` + `Microsoft.Extensions.Hosting.WindowsServices` 注册为系统服务，无控制台窗口；日志写入程序目录下 `Logs`（见 `LogSettings`）。 |
| **命令行维护** | 直接运行 `UserSync.exe` 并带参数 `--update-passwords`，用于批量重置密码（可选 `--update-mode` / `-m`）。 |

### 安装服务（管理员命令提示符 / PowerShell 示例）

**PowerShell 注意**：`sc` 会被解析为 `Set-Content` 的别名，**不是**服务控制程序。请在 PowerShell 中使用 **`sc.exe`**（如下），或改用 **cmd.exe** 执行传统 `sc ...` 命令。

```text
sc.exe create UserSync binPath= "C:\部署目录\UserSync.exe" start= auto
sc.exe start UserSync
```

请将 **`appsettings.json` 与可执行文件放在同一目录**。卸载：`sc.exe stop UserSync` → `sc.exe delete UserSync`。

### 调度策略（`Workers/UserSyncWorker.cs`）

- 默认 **每天本地时间 02:00** 执行一轮完整同步。
- 若服务启动时 **已超过当日 02:00** 且 **当日尚未执行过**同步，则 **立即补跑一次**；下一轮仍安排在 **次日 02:00**。
- `SyncSettings:EnableAutoSync` 为 `false` 时：启动后 **只执行一次**同步，随后停止服务进程（适合调试或单次任务）。

> 说明：`appsettings.json` 中的 `CheckIntervalSeconds` 为历史配置项，当前调度不依赖该字段，可保留或删除。

## 项目结构

```
UserSync/
├── Program.cs                          # Host 入口、密码维护命令行分支
├── Workers/
│   └── UserSyncWorker.cs               # 后台任务：每日 02:00 / 补跑逻辑
├── UserSync.csproj
├── appsettings.json                    # 连接串与各开关（勿将含密码的提交到公开仓库）
│
├── Services/
│   ├── PersonnelSyncService.cs        # 核心同步 SQL 与任务编排
│   ├── DatabaseService.cs
│   ├── DatabaseLogService.cs
│   ├── DbHelper.cs
│   ├── UserPasswordService.cs
│   └── PasswordEncryptionHelper.cs
│
├── Models/AppSettings.cs
├── Extensions/                         # DI、Serilog、配置扩展
├── Helpers/ConsoleHelper.cs            # 命令行维护场景仍可使用
├── Constants/ConfigKeys.cs
│
├── SQL/                                # 手工/运维用脚本
├── Data/
├── Docs/
└── Archive/                            # 未参与编译的归档代码
```

## 快速开始

1. **配置数据库**  
   编辑 `appsettings.json` 中 `ConnectionStrings:SQLServer`（生产环境建议使用密钥管理或环境变量，勿泄露密码）。

2. **发布并部署**  
   在项目根目录执行 `dotnet publish`（常用参数见下方 **「发布参数示例」**），将输出目录中的 `UserSync.exe` 与 `appsettings.json` 一并复制到服务器，再注册 Windows 服务。

3. **详细文档**  
   见 `Docs/` 下说明（部分内容可能仍描述旧版控制台行为，以本 README 与代码为准）。

## 发布参数示例

在包含 `UserSync.csproj` 的目录下执行；若当前目录为解决方案根目录，请加上项目路径。

### 常用参数说明

| 参数 | 含义 |
|------|------|
| `-c Release` | 使用 Release 配置编译（体积与性能更适合上线）。 |
| `-o <路径>` | 输出目录，例如 `.\publish` 或 `D:\Deploy\UserSync`。 |
| `-r win-x64` | 目标运行时（64 位 Windows）。单机无 SDK 部署时常与自包含一起用。 |
| `--self-contained true` | 自带 .NET 运行时，目标机可不装 Runtime（输出更大）。 |
| `--self-contained false` | 依赖目标机已安装 **.NET 8 桌面/服务器运行时**（体积小，需预装运行时）。 |

### 示例一：依赖框架（服务器已装 .NET 8 Runtime）

输出到当前目录下的 `publish` 文件夹：

```bash
dotnet publish UserSync.csproj -c Release -o ./publish --self-contained false
```

### 示例二：自包含 64 位 Windows（服务器未装 .NET）

```bash
dotnet publish UserSync.csproj -c Release -r win-x64 -o ./publish --self-contained true
```

### 示例三：指定绝对输出路径（便于 CI/CD）

```bash
dotnet publish UserSync.csproj -c Release -o "D:\Artifacts\UserSync" --self-contained false
```

### 发布后需一并复制的文件

- 将 **`appsettings.json`**（及环境专用配置如 `appsettings.Production.json`，若使用）复制到与 **`UserSync.exe` 相同目录**。  
- 项目已配置 `appsettings.json` **复制到输出目录**；发布目录中应能看到该文件，部署时勿漏拷。

### 可选：单文件发布（排查问题时慎用）

单文件会把依赖打包为一个 `exe`，启动解压略慢；服务场景一般 **框架依赖 + 多文件** 即可。

```bash
dotnet publish UserSync.csproj -c Release -r win-x64 -o ./publish --self-contained true /p:PublishSingleFile=true
```

### 从发布到安装为 Windows 服务（完整流程）

程序本身已是「可作为服务运行的 exe」，**发布**只是把文件编好；**注册服务**是在 Windows 上用 `sc`（或 PowerShell）把该 exe 登记为服务。

1. **在开发机编译发布**（二选一）  
   - 服务器已装 .NET 8：`dotnet publish UserSync.csproj -c Release -o ./publish --self-contained false`  
   - 服务器未装运行时：`dotnet publish UserSync.csproj -c Release -r win-x64 -o ./publish --self-contained true`

2. **把整个输出目录拷到服务器**  
   例如 `D:\Services\UserSync\`，确保其中有 **`UserSync.exe`**、**`appsettings.json`** 以及发布生成的 **`.dll` 等依赖**（不要只拷 exe）。

3. **编辑服务器上的 `appsettings.json`**  
   配置数据库连接、`SyncSettings` 等。

4. **以管理员打开「命令提示符」或 PowerShell**，创建并启动服务（**`binPath=` 后必须带空格再写路径**，路径用英文引号）。在 **PowerShell 中请使用 `sc.exe`**，勿单独写 `sc`：

```text
sc.exe create UserSync binPath= "D:\Services\UserSync\UserSync.exe" start= auto DisplayName= "UserSync 人员同步"
sc.exe start UserSync
```

5. **确认状态**  
   - `services.msc` 中找到服务，或命令行：`sc.exe query UserSync`  
   - 日志：程序目录下 `Logs`（由 `LogSettings` 决定）。

6. **升级版本**  
   `sc.exe stop UserSync` → 覆盖发布目录中的文件（保留或合并 `appsettings.json`）→ `sc.exe start UserSync`。

**卸载服务**：`sc.exe stop UserSync` → `sc.exe delete UserSync`。

**说明**：若使用 **框架依赖**发布（`--self-contained false`），服务器须安装 [.NET 8 运行时](https://dotnet.microsoft.com/download/dotnet/8.0)（Hosting Bundle 或 Desktop Runtime 按环境选择）。服务默认以 **Local System** 运行；若数据库为 **Windows 身份验证**，需在服务属性里改为有权限的域/机器账号。

## 同步任务顺序（`PersonnelSyncService`）

在 `SyncSettings:SyncTasks` 中可按布尔开关启用/禁用各步骤，默认顺序为：

1. **SyncNewUsersFromVps** — 在制且不在 `Sys_User` 的新员工写入 `Sys_User`（含 `Factory` 等字段）。
2. **GenerateUserLogOn** — 为缺少记录的账号生成 `Sys_UserLogOn`。
3. **UpdateEmptyPasswords** — 空密码账号更新为默认密码策略（见 `UserPasswordService`）。
4. **SyncBasePeopleFromVps** — 从 `vps_empinfo_mes` 插入 `base_people`（新记录）。
5. **SyncBasePeopleSysUser** — `base_people.SysUser` 与 `Sys_User` 关联。
6. **SyncSysUserEnabledMark** — 按 VPS 离职状态更新 `Sys_User.F_EnabledMark`。
7. **SyncBasePeopleJobStatus** — 更新 `base_people.JobStatus`。

**不再包含** `base_people.ManufactureGroup` 的程序内同步（该任务已从代码中移除）。

## 制造部与 Factory 范围（`appsettings.json`）

在 **`SyncSettings:ManufactureGdname4Factories`** 中配置数组，每项为 **`Gdname4`**（与视图 `vps_empinfo_mes.gdname4` 完全一致）与 **`Factory`**（写入 `Sys_User` / `base_people` 的字面值）。程序据此生成 `ve.gdname4 IN (...)` 与 `CASE ve.gdname4 ... END`。

- **`SyncSettings:ManufactureGdname4DefaultFactory`**：未匹配任何已配置 `Gdname4` 时使用的 Factory（常见为空字符串 `""`）。
- 数组**顺序**决定 `CASE` 中 `WHEN` 的先后；同一 `Gdname4` 重复配置时以**先出现**的为准。

默认示例见仓库内 `appsettings.json`；上线后按实际部门名称与工厂编码修改即可。

## 配置说明（`appsettings.json`）

| 区域 | 说明 |
|------|------|
| `ConnectionStrings` | SQL Server 连接串 |
| `LogSettings` | 日志目录、文件名模板、保留天数、是否写库日志 |
| `SyncSettings` | `EnableAutoSync`、各 `SyncTasks` 开关 |
| `Logging` | Microsoft.Extensions.Logging 级别 |

服务运行时 Serilog **默认仅写文件**（不写控制台）；日志目录相对路径会解析到 **程序集所在目录**，避免服务当前目录为 `System32` 时路径错误。

## 技术特性

- **Generic Host**、**BackgroundService** 承载同步循环  
- **Windows Services** 集成（`AddWindowsService`）  
- **依赖注入**、强类型配置、`appsettings.json`  
- **Serilog** 按日滚动文件日志  
- **可选**数据库日志表写入（`DatabaseLogService`）  
- 全局未处理异常与同步过程日志  

## 版本历史

- **v3.0** (2026-04)  
  - 以 Windows 服务方式运行；每日 02:00 调度，已过当日 02:00 则当日补跑一次。  
  - 制造部与 `Factory` 映射、`gdname4` 范围调整；移除 `base_people.ManufactureGroup` 同步任务。  
- **v2.1** (2026-01-26) — 依赖注入、强类型配置、结构优化  
- **v2.0** (2026-01-21) — 迁移 .NET 8.0  
- **v1.0** (2026-01-21) — 初始版本  
