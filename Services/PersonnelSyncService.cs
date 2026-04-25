using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UserSync.Extensions;
using UserSync.Models;

namespace UserSync.Services
{
    /// <summary>
    /// 人员数据同步服务
    /// 功能：同步 vps_empinfo_mes 数据到 Sys_User 和 base_people 表
    /// </summary>
    public class PersonnelSyncService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PersonnelSyncService> _logger;
        private readonly DatabaseLogService? _databaseLogService;
        private readonly DbHelper _dbHelper;
        private readonly UserPasswordService? _passwordService;
        private readonly ILoggerFactory? _loggerFactory;

        /// <summary>由 <see cref="SyncSettings.ManufactureGdname4Factories"/> 生成的 <c>ve.gdname4 IN (...)</c> 片段。</summary>
        private readonly string _sqlVpsManufactureGdname4Predicate;

        /// <summary>由配置生成的 <c>CASE ve.gdname4 ... END</c>（Factory 列）。</summary>
        private readonly string _sqlSelectFactoryByGdname4;

        public PersonnelSyncService(
            IConfiguration configuration,
            ILogger<PersonnelSyncService> logger,
            DatabaseLogService? databaseLogService = null,
            UserPasswordService? passwordService = null,
            ILoggerFactory? loggerFactory = null)
        {
            _configuration = configuration;
            _logger = logger;
            _databaseLogService = databaseLogService;
            _dbHelper = new DbHelper(configuration, logger);
            _loggerFactory = loggerFactory;
            
            if (passwordService != null)
            {
                _passwordService = passwordService;
            }
            else if (loggerFactory != null)
            {
                _passwordService = new UserPasswordService(configuration, loggerFactory.CreateLogger<UserPasswordService>(), databaseLogService);
            }
            else
            {
                // 如果没有 loggerFactory，使用通用的 logger（UserPasswordService 现在支持通用 ILogger）
                _passwordService = new UserPasswordService(configuration, logger, databaseLogService);
            }

            (_sqlVpsManufactureGdname4Predicate, _sqlSelectFactoryByGdname4) =
                BuildManufactureSqlFragments(configuration.GetAppSettings().SyncSettings);
        }

        /// <summary>将字符串嵌入 T-SQL Unicode 字面量（单引号转义）。</summary>
        private static string SqlUnicodeLiteral(string value)
        {
            return "N'" + (value ?? string.Empty).Replace("'", "''") + "'";
        }

        /// <summary>根据 SyncSettings 生成 gdname4 过滤与 Factory CASE 片段。</summary>
        private static (string Predicate, string FactoryCase) BuildManufactureSqlFragments(SyncSettings sync)
        {
            var items = sync.ManufactureGdname4Factories;
            if (items == null || items.Count == 0)
            {
                throw new InvalidOperationException(
                    "SyncSettings.ManufactureGdname4Factories 不能为空，请在 appsettings.json 中配置。");
            }

            var normalized = items
                .Select(x => (Gdname4: (x.Gdname4 ?? string.Empty).Trim(), Factory: x.Factory ?? string.Empty))
                .Where(x => !string.IsNullOrEmpty(x.Gdname4))
                .ToList();
            if (normalized.Count == 0)
            {
                throw new InvalidOperationException(
                    "SyncSettings.ManufactureGdname4Factories 中至少需要一条非空的 Gdname4。");
            }

            var distinctGdForIn = normalized.Select(x => x.Gdname4).Distinct(StringComparer.Ordinal).ToList();
            var inList = string.Join(", ", distinctGdForIn.Select(SqlUnicodeLiteral));
            var predicate = "ve.gdname4 IN (" + inList + ")";

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("                        CASE ve.gdname4");
            foreach (var (gd, fac) in normalized)
            {
                sb.AppendLine($"                            WHEN {SqlUnicodeLiteral(gd)} THEN {SqlUnicodeLiteral(fac)}");
            }

            sb.AppendLine($"                            ELSE {SqlUnicodeLiteral(sync.ManufactureGdname4DefaultFactory ?? string.Empty)}");
            sb.Append("                        END");

            return (predicate, sb.ToString());
        }

        /// <summary>
        /// 同步结果类
        /// </summary>
        public class SyncResult
        {
            public string TaskName { get; set; } = string.Empty;
            public bool Success { get; set; }
            public int AffectedRows { get; set; }
            public string Message { get; set; } = string.Empty;
            public Exception? Exception { get; set; }
            public long ExecutionDuration { get; set; }
        }

        /// <summary>
        /// 执行所有同步任务
        /// </summary>
        public async Task<List<SyncResult>> ExecuteAllSyncTasksAsync()
        {
            var results = new List<SyncResult>();
            var syncSettings = _configuration.GetSection("SyncSettings:SyncTasks");

            // 步骤1：将vps_empinfo_mes中在制的员工，且在sys_user中查询不出来的员工同步到sys_user
            if (syncSettings.GetValue<bool>("SyncNewUsersFromVps", true))
            {
                results.Add(await SyncNewUsersFromVpsAsync());
            }

            // 步骤2：根据sys_user中查询Sys_UserLogOn，并生成Sys_UserLogOn记录
            if (syncSettings.GetValue<bool>("GenerateUserLogOn", true))
            {
                results.Add(await GenerateUserLogOnAsync());
            }

            // 步骤2.5：更新密码为空的账号为默认密码123456
            if (syncSettings.GetValue<bool>("UpdateEmptyPasswords", true))
            {
                results.Add(await UpdateEmptyPasswordsAsync());
            }

            // 步骤3：生成base_people记录
            if (syncSettings.GetValue<bool>("SyncBasePeopleFromVps", true))
            {
                results.Add(await SyncBasePeopleFromVpsAsync());
            }

            // 步骤4：base_people关联sys_user
            if (syncSettings.GetValue<bool>("SyncBasePeopleSysUser", true))
            {
                results.Add(await SyncBasePeopleSysUserAsync());
            }

            // 其他同步任务（可选）
            // 同步 Sys_User.F_EnabledMark
            if (syncSettings.GetValue<bool>("SyncSysUserEnabledMark", true))
            {
                results.Add(await SyncSysUserEnabledMarkAsync());
            }

            // 同步 base_people.JobStatus
            if (syncSettings.GetValue<bool>("SyncBasePeopleJobStatus", true))
            {
                results.Add(await SyncBasePeopleJobStatusAsync());
            }

            return results;
        }

        /// <summary>
        /// 同步 Sys_User 表的 F_EnabledMark 字段
        /// 根据 vps_empinfo_mes.isactive=0 更新 F_EnabledMark=0
        /// </summary>
        public async Task<SyncResult> SyncSysUserEnabledMarkAsync()
        {
            var result = new SyncResult { TaskName = "同步Sys_User.F_EnabledMark" };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                string sql = @"
                    UPDATE su
                    SET su.F_EnabledMark = 0
                    FROM Sys_User su
                    INNER JOIN vps_empinfo_mes ve ON su.F_Account = ve.empcode
                    WHERE " + _sqlVpsManufactureGdname4Predicate + @"
                        AND ve.isactive = 0
                        AND (su.F_EnabledMark IS NULL OR su.F_EnabledMark <> 0);
                ";

                result.AffectedRows = await _dbHelper.ExecuteNonQueryAsync(sql);
                stopwatch.Stop();

                result.Success = true;
                result.Message = $"成功更新 {result.AffectedRows} 条记录的 F_EnabledMark 字段为 0";
                result.ExecutionDuration = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("[{TaskName}] {Message}, 耗时: {Duration}ms", 
                    result.TaskName, result.Message, result.ExecutionDuration);
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogInformationAsync(
                        result.Message, "UserSync", "SyncSysUserEnabledMark", null, "ExecuteSync");
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Message = $"同步失败：{ex.Message}";
                result.Exception = ex;
                result.ExecutionDuration = stopwatch.ElapsedMilliseconds;

                _logger.LogError(ex, "[{TaskName}] 同步失败", result.TaskName);
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogErrorAsync(
                        $"同步失败：{result.TaskName}", ex, "UserSync", "SyncSysUserEnabledMark", null, "ExecuteSync");
                }
            }

            return result;
        }

        /// <summary>
        /// 同步 base_people 表的 JobStatus 字段
        /// 根据 vps_empinfo_mes.isactive 更新 JobStatus（0=在制，1=离职）
        /// </summary>
        public async Task<SyncResult> SyncBasePeopleJobStatusAsync()
        {
            var result = new SyncResult { TaskName = "同步base_people.JobStatus" };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                string sql = @"
                    UPDATE bp
                    SET bp.JobStatus = CASE 
                        WHEN ve.isactive = 0 THEN 1  -- isactive=0（离职）-> JobStatus=1
                        WHEN ve.isactive = 1 THEN 0   -- isactive=1（在制）-> JobStatus=0
                        ELSE bp.JobStatus  -- 保持原值
                    END
                    FROM base_people bp
                    INNER JOIN vps_empinfo_mes ve ON bp.Code = ve.empcode
                    WHERE " + _sqlVpsManufactureGdname4Predicate + @"
                        AND ve.isactive IS NOT NULL
                        AND (
                            bp.JobStatus IS NULL
                            OR (ve.isactive = 0 AND bp.JobStatus <> 1)
                            OR (ve.isactive = 1 AND bp.JobStatus <> 0)
                        );
                ";

                result.AffectedRows = await _dbHelper.ExecuteNonQueryAsync(sql);
                stopwatch.Stop();

                result.Success = true;
                result.Message = $"成功更新 {result.AffectedRows} 条记录的 JobStatus 字段";
                result.ExecutionDuration = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("[{TaskName}] {Message}, 耗时: {Duration}ms", 
                    result.TaskName, result.Message, result.ExecutionDuration);
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogInformationAsync(
                        result.Message, "UserSync", "SyncBasePeopleJobStatus", null, "ExecuteSync");
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Message = $"同步失败：{ex.Message}";
                result.Exception = ex;
                result.ExecutionDuration = stopwatch.ElapsedMilliseconds;

                _logger.LogError(ex, "[{TaskName}] 同步失败", result.TaskName);
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogErrorAsync(
                        $"同步失败：{result.TaskName}", ex, "UserSync", "SyncBasePeopleJobStatus", null, "ExecuteSync");
                }
            }

            return result;
        }

        /// <summary>
        /// 同步 base_people 表的 SysUser 字段
        /// 根据 Sys_User.F_Account = base_people.Code 更新 base_people.SysUser = Sys_User.ID
        /// </summary>
        public async Task<SyncResult> SyncBasePeopleSysUserAsync()
        {
            var result = new SyncResult { TaskName = "同步base_people.SysUser" };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                string sql = @"
                    UPDATE bp
                    SET bp.SysUser = u.ID
                    FROM base_people bp
                    INNER JOIN Sys_User u ON bp.Code = u.F_Account
                    WHERE (bp.SysUser IS NULL OR bp.SysUser <> u.ID);
                ";

                result.AffectedRows = await _dbHelper.ExecuteNonQueryAsync(sql);
                stopwatch.Stop();

                result.Success = true;
                result.Message = $"成功更新 {result.AffectedRows} 条记录的 SysUser 字段";
                result.ExecutionDuration = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("[{TaskName}] {Message}, 耗时: {Duration}ms", 
                    result.TaskName, result.Message, result.ExecutionDuration);
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogInformationAsync(
                        result.Message, "UserSync", "SyncBasePeopleSysUser", null, "ExecuteSync");
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Message = $"同步失败：{ex.Message}";
                result.Exception = ex;
                result.ExecutionDuration = stopwatch.ElapsedMilliseconds;

                _logger.LogError(ex, "[{TaskName}] 同步失败", result.TaskName);
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogErrorAsync(
                        $"同步失败：{result.TaskName}", ex, "UserSync", "SyncBasePeopleSysUser", null, "ExecuteSync");
                }
            }

            return result;
        }

        /// <summary>
        /// 生成同步报告
        /// </summary>
        public string GenerateSyncReport(List<SyncResult> results)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("==========================================");
            report.AppendLine("人员数据同步报告");
            report.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine("==========================================");
            report.AppendLine();

            int successCount = 0;
            int failCount = 0;
            int totalAffected = 0;

            foreach (var result in results)
            {
                report.AppendLine($"【{result.TaskName}】");
                report.AppendLine($"  状态：{(result.Success ? "成功" : "失败")}");
                report.AppendLine($"  影响行数：{result.AffectedRows}");
                report.AppendLine($"  耗时：{result.ExecutionDuration}ms");
                report.AppendLine($"  消息：{result.Message}");

                if (result.Exception != null)
                {
                    report.AppendLine($"  错误：{result.Exception.Message}");
                }

                report.AppendLine();

                if (result.Success)
                {
                    successCount++;
                    totalAffected += result.AffectedRows;
                }
                else
                {
                    failCount++;
                }
            }

            report.AppendLine("==========================================");
            report.AppendLine($"总计：成功 {successCount} 个任务，失败 {failCount} 个任务");
            report.AppendLine($"总影响行数：{totalAffected}");
            report.AppendLine($"总耗时：{results.Sum(r => r.ExecutionDuration)}ms");
            report.AppendLine("==========================================");

            return report.ToString();
        }

        /// <summary>
        /// 步骤1：将vps_empinfo_mes中在制的员工，且在sys_user中查询不出来的员工同步到sys_user
        /// </summary>
        public async Task<SyncResult> SyncNewUsersFromVpsAsync()
        {
            var result = new SyncResult { TaskName = "同步新用户到Sys_User" };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                string sql = @"
                    INSERT INTO Sys_User (
                        ID, F_Account, F_RealName, F_NickName, F_HeadIcon, F_Gender, F_Birthday, 
                        F_MobilePhone, F_Email, F_WeChat, F_ManagerId, F_SecurityLevel, F_Signature, 
                        F_OrganizeId, F_DepartmentId, F_RoleId, F_DutyId, F_IsAdministrator, F_SortCode, 
                        F_DeleteMark, F_EnabledMark, F_Description, F_CreatorTime, F_CreatorUserId, 
                        F_LastModifyTime, F_LastModifyUserId, F_DeleteTime, F_DeleteUserId, EnterpriseID, 
                        Org, addtime, adduser, modtime, moduser, unionid, openid, LastOrg, ManuNum, 
                        Dapfca, Address, DeliveFreque, DeliveCount, CarNum, DriverName, DriverContact, 
                        ManuName, LimitFollow, avatarUrl, FirmName, Sort, SingleTime, WaveTime, Punctual, 
                        lat, lng, [Position], SupplyID, Flag, tempDepart, IsDaily, isWechat, isWeb, 
                        isMini, isPwd, isSRM, UserPriceType, DisFactor, SupplyRegisterRecordId, UserType, 
                        isInterface, SupplyPotentialID, isOutNetwork, JypowerID, PurchaseGroup, U8DeptCode, 
                        U8PersonCode, U8PurchaseType, isSonAccount, isReSet, Company, isJIT, Factory, MRPControl
                    )
                    SELECT 
                        NEWID() AS ID,
                        ve.empcode AS F_Account,
                        ve.empname AS F_RealName,
                        NULL AS F_NickName,
                        NULL AS F_HeadIcon,
                        0 AS F_Gender,
                        NULL AS F_Birthday,
                        NULL AS F_MobilePhone,
                        NULL AS F_Email,
                        NULL AS F_WeChat,
                        NULL AS F_ManagerId,
                        NULL AS F_SecurityLevel,
                        NULL AS F_Signature,
                        N'86F31434-9D7A-474B-BC4B-BBF206657A5A' AS F_OrganizeId,
                        N'86F31434-9D7A-474B-BC4B-BBF206657A5A' AS F_DepartmentId,
                        N'FE49CA8D-AF31-4320-A50B-EE005DD5B690' AS F_RoleId,
                        NULL AS F_DutyId,
                        0 AS F_IsAdministrator,
                        NULL AS F_SortCode,
                        NULL AS F_DeleteMark,
                        1 AS F_EnabledMark,
                        NULL AS F_Description,
                        GETDATE() AS F_CreatorTime,
                        N'system' AS F_CreatorUserId,
                        GETDATE() AS F_LastModifyTime,
                        N'system' AS F_LastModifyUserId,
                        NULL AS F_DeleteTime,
                        NULL AS F_DeleteUserId,
                        N'966B125C-AB2A-4D71-AB77-64368E182809' AS EnterpriseID,
                        N'30643314-50A9-477A-808E-55BEC9B00109' AS Org,
                        GETDATE() AS addtime,
                        N'system' AS adduser,
                        GETDATE() AS modtime,
                        N'system' AS moduser,
                        NULL AS unionid,
                        NULL AS openid,
                        NULL AS LastOrg,
                        NULL AS ManuNum,
                        NULL AS Dapfca,
                        NULL AS Address,
                        NULL AS DeliveFreque,
                        NULL AS DeliveCount,
                        NULL AS CarNum,
                        NULL AS DriverName,
                        NULL AS DriverContact,
                        NULL AS ManuName,
                        NULL AS LimitFollow,
                        NULL AS avatarUrl,
                        NULL AS FirmName,
                        NULL AS Sort,
                        NULL AS SingleTime,
                        NULL AS WaveTime,
                        NULL AS Punctual,
                        NULL AS lat,
                        NULL AS lng,
                        NULL AS [Position],
                        NULL AS SupplyID,
                        NULL AS Flag,
                        NULL AS tempDepart,
                        0 AS IsDaily,
                        1 AS isWechat,
                        1 AS isWeb,
                        1 AS isMini,
                        1 AS isPwd,
                        0 AS isSRM,
                        NULL AS UserPriceType,
                        0.000000000 AS DisFactor,
                        NULL AS SupplyRegisterRecordId,
                        NULL AS UserType,
                        0 AS isInterface,
                        NULL AS SupplyPotentialID,
                        0 AS isOutNetwork,
                        NULL AS JypowerID,
                        NULL AS PurchaseGroup,
                        NULL AS U8DeptCode,
                        NULL AS U8PersonCode,
                        NULL AS U8PurchaseType,
                        0 AS isSonAccount,
                        0 AS isReSet,
                        NULL AS Company,
                        NULL AS isJIT,
                        " + _sqlSelectFactoryByGdname4 + @" AS Factory,
                        NULL AS MRPControl
                    FROM vps_empinfo_mes ve
                    LEFT JOIN Sys_User su ON ve.empcode = su.F_Account
                    WHERE " + _sqlVpsManufactureGdname4Predicate + @"
                        AND ve.isactive = 1  -- 在制员工
                        AND su.ID IS NULL;  -- 在Sys_User中不存在
                ";

                result.AffectedRows = await _dbHelper.ExecuteNonQueryAsync(sql);
                stopwatch.Stop();

                result.Success = true;
                result.Message = $"成功插入 {result.AffectedRows} 条新用户记录到 Sys_User";
                result.ExecutionDuration = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("[{TaskName}] {Message}, 耗时: {Duration}ms", 
                    result.TaskName, result.Message, result.ExecutionDuration);
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogInformationAsync(
                        result.Message, "UserSync", "SyncNewUsersFromVps", null, "ExecuteSync");
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Message = $"同步失败：{ex.Message}";
                result.Exception = ex;
                result.ExecutionDuration = stopwatch.ElapsedMilliseconds;

                _logger.LogError(ex, "[{TaskName}] 同步失败", result.TaskName);
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogErrorAsync(
                        $"同步失败：{result.TaskName}", ex, "UserSync", "SyncNewUsersFromVps", null, "ExecuteSync");
                }
            }

            return result;
        }

        /// <summary>
        /// 步骤2：根据sys_user中查询Sys_UserLogOn，并生成Sys_UserLogOn记录
        /// </summary>
        public async Task<SyncResult> GenerateUserLogOnAsync()
        {
            var result = new SyncResult { TaskName = "生成Sys_UserLogOn记录" };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                string sql = @"
                    INSERT INTO Sys_UserLogOn (
                        ID, F_UserId, F_UserPassword, F_UserSecretkey, F_AllowStartTime, F_AllowEndTime, 
                        F_LockStartDate, F_LockEndDate, F_FirstVisitTime, F_PreviousVisitTime, F_LastVisitTime, 
                        F_ChangePasswordDate, F_MultiUserLogin, F_LogOnCount, F_UserOnLine, F_Question, 
                        F_AnswerQuestion, F_CheckIPAddress, F_Language, F_Theme, addtime, adduser, modtime, moduser, Org, EnterpriseID
                    )
                    SELECT 
                        u.ID AS ID,
                        u.ID AS F_UserId,
                        N'' AS F_UserPassword,  -- 需要后续通过应用程序更新为加密后的密码
                        NULL AS F_UserSecretkey,  -- 先为空，后续更新密码时会自动生成
                        NULL AS F_AllowStartTime,
                        NULL AS F_AllowEndTime,
                        NULL AS F_LockStartDate,
                        NULL AS F_LockEndDate,
                        NULL AS F_FirstVisitTime,
                        NULL AS F_PreviousVisitTime,
                        NULL AS F_LastVisitTime,
                        NULL AS F_ChangePasswordDate,
                        NULL AS F_MultiUserLogin,
                        0 AS F_LogOnCount,
                        0 AS F_UserOnLine,
                        NULL AS F_Question,
                        NULL AS F_AnswerQuestion,
                        0 AS F_CheckIPAddress,
                        NULL AS F_Language,
                        NULL AS F_Theme,
                        GETDATE() AS addtime,
                        N'system' AS adduser,
                        GETDATE() AS modtime,
                        N'system' AS moduser,
                        u.Org AS Org,
                        u.EnterpriseID AS EnterpriseID
                    FROM Sys_User u
                    LEFT JOIN Sys_UserLogOn ul ON u.ID = ul.F_UserId
                    WHERE ul.ID IS NULL;  -- 只插入缺少Sys_UserLogOn记录的用户
                ";

                result.AffectedRows = await _dbHelper.ExecuteNonQueryAsync(sql);
                stopwatch.Stop();

                result.Success = true;
                result.Message = $"成功生成 {result.AffectedRows} 条 Sys_UserLogOn 记录";
                result.ExecutionDuration = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("[{TaskName}] {Message}, 耗时: {Duration}ms", 
                    result.TaskName, result.Message, result.ExecutionDuration);
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogInformationAsync(
                        result.Message, "UserSync", "GenerateUserLogOn", null, "ExecuteSync");
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Message = $"生成失败：{ex.Message}";
                result.Exception = ex;
                result.ExecutionDuration = stopwatch.ElapsedMilliseconds;

                _logger.LogError(ex, "[{TaskName}] 生成失败", result.TaskName);
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogErrorAsync(
                        $"生成失败：{result.TaskName}", ex, "UserSync", "GenerateUserLogOn", null, "ExecuteSync");
                }
            }

            return result;
        }

        /// <summary>
        /// 步骤3：生成base_people记录（从vps_empinfo_mes同步）
        /// </summary>
        public async Task<SyncResult> SyncBasePeopleFromVpsAsync()
        {
            var result = new SyncResult { TaskName = "同步base_people记录" };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // 根据实际的base_people表结构插入所有必需字段
                string sql = @"
                    INSERT INTO base_people (
                        ID, Code, Name, F_OrganizeId, addtime, adduser, modtime, moduser, Org, EnterpriseID, 
                        Subjects, IsQuit, Mailbox, [Position], Phone, Extension, WorksAddress, BasicRemark, 
                        EntryDate, SiYears, StaffType, StaffStatus, ProbDate, ActualFormal, PostLevel, PlanFormal, 
                        IDCardName, IDCard, DateBirth, Age, Sex, Nation, IDCardAddress, IDCardTerm, IsLongTerm, 
                        Marriage, FristWorkDate, WorkYears, HoursType, Political, SocialSecurity, PublicReserve, 
                        Education, GraduateSchool, GraduateDate, Major, BankNumber, BankDeposit, ContractCompany, 
                        ContractType, FristStartDate, FirstEndDate, NowStartDate, NowEndDate, ContractTerm, Renewal, 
                        UrgentName, UrgentRelation, UrgentTel, CommitTime, CommitUser, ApproveTime, ApproveUser, 
                        CancelApproveTime, CancelApproveUser, DocApproveStatus, UploadStatus, SysUser, JobStatus, 
                        HeadPortID, HeadPortPath, ICCard, Teams, LeaveDate, ParentIdStr, ParentNameStr, 
                        PeopleEducationId, PeopleGradeId, PeoplePositionId, AccommodationType, NewStaffApplyId, 
                        SalaryTotal, WorkingType, OverTimePay, MeritsPay, FullTimeReward, PostWage, RegularRange, 
                        DinnerSubsidy, StaffSchool, StaffMajor, GraduationYear, CalcOverTime, CalcProdMerit, 
                        SheBao1, GongJiJin, SpecialDeduction, SheBao2, SheBao3, StaffType2, DocumentTypeId, 
                        U8DeptCode, U8PersonCode, BankName, InstructID, RankCode, ManufactureGroup, Factory, 
                        PositionFL, PositionLevel, PositionNew, PositionID
                    )
                    SELECT 
                        NEWID() AS ID,
                        ve.empcode AS Code,
                        ve.empname AS Name,
                        NULL AS F_OrganizeId,
                        GETDATE() AS addtime,
                        N'system' AS adduser,
                        GETDATE() AS modtime,
                        N'system' AS moduser,
                        N'30643314-50A9-477A-808E-55BEC9B00109' AS Org,
                        N'966B125C-AB2A-4D71-AB77-64368E182809' AS EnterpriseID,
                        NULL AS Subjects,
                        0 AS IsQuit,
                        NULL AS Mailbox,
                        NULL AS [Position],
                        NULL AS Phone,
                        NULL AS Extension,
                        NULL AS WorksAddress,
                        NULL AS BasicRemark,
                        NULL AS EntryDate,
                        NULL AS SiYears,
                        N'员工岗' AS StaffType,
                        NULL AS StaffStatus,
                        NULL AS ProbDate,
                        NULL AS ActualFormal,
                        NULL AS PostLevel,
                        NULL AS PlanFormal,
                        NULL AS IDCardName,
                        NULL AS IDCard,
                        NULL AS DateBirth,
                        0 AS Age,
                        0 AS Sex,
                        NULL AS Nation,
                        NULL AS IDCardAddress,
                        NULL AS IDCardTerm,
                        0 AS IsLongTerm,
                        NULL AS Marriage,
                        NULL AS FristWorkDate,
                        0 AS WorkYears,
                        NULL AS HoursType,
                        NULL AS Political,
                        NULL AS SocialSecurity,
                        NULL AS PublicReserve,
                        NULL AS Education,
                        NULL AS GraduateSchool,
                        NULL AS GraduateDate,
                        NULL AS Major,
                        NULL AS BankNumber,
                        NULL AS BankDeposit,
                        NULL AS ContractCompany,
                        NULL AS ContractType,
                        NULL AS FristStartDate,
                        NULL AS FirstEndDate,
                        NULL AS NowStartDate,
                        NULL AS NowEndDate,
                        NULL AS ContractTerm,
                        0 AS Renewal,
                        NULL AS UrgentName,
                        NULL AS UrgentRelation,
                        NULL AS UrgentTel,
                        GETDATE() AS CommitTime,
                        NULL AS CommitUser,
                        GETDATE() AS ApproveTime,
                        NULL AS ApproveUser,
                        GETDATE() AS CancelApproveTime,
                        NULL AS CancelApproveUser,
                        NULL AS DocApproveStatus,
                        0 AS UploadStatus,
                        NULL AS SysUser,  -- 后续通过 SyncBasePeopleSysUserAsync 更新
                        CASE WHEN ve.isactive = 0 THEN 1 ELSE 0 END AS JobStatus,  -- 0=在制，1=离职
                        NULL AS HeadPortID,
                        NULL AS HeadPortPath,
                        NULL AS ICCard,
                        N'86F31434-9D7A-474B-BC4B-BBF206657A5A' AS Teams,
                        NULL AS LeaveDate,
                        N'6712844B-A922-4566-9A0C-89C9B0A525BB,7AF1A42B-B09F-41A0-812E-5B1812FB4E64,86F31434-9D7A-474B-BC4B-BBF206657A5A' AS ParentIdStr,
                        N'创世纪集团-浙江创世纪-制造部' AS ParentNameStr,
                        NULL AS PeopleEducationId,
                        NULL AS PeopleGradeId,
                        NULL AS PeoplePositionId,
                        NULL AS AccommodationType,
                        NULL AS NewStaffApplyId,
                        0.00 AS SalaryTotal,
                        NULL AS WorkingType,
                        NULL AS OverTimePay,
                        NULL AS MeritsPay,
                        NULL AS FullTimeReward,
                        NULL AS PostWage,
                        NULL AS RegularRange,
                        NULL AS DinnerSubsidy,
                        NULL AS StaffSchool,
                        NULL AS StaffMajor,
                        NULL AS GraduationYear,
                        NULL AS CalcOverTime,
                        NULL AS CalcProdMerit,
                        0.00 AS SheBao1,
                        NULL AS GongJiJin,
                        NULL AS SpecialDeduction,
                        0.00 AS SheBao2,
                        0.00 AS SheBao3,
                        N'' AS StaffType2,
                        NULL AS DocumentTypeId,
                        NULL AS U8DeptCode,
                        NULL AS U8PersonCode,
                        NULL AS BankName,
                        NULL AS InstructID,
                        N'' AS RankCode,
                        NULL AS ManufactureGroup,  -- 不同步 ManufactureGroup
                        " + _sqlSelectFactoryByGdname4 + @" AS Factory,
                        NULL AS PositionFL,
                        NULL AS PositionLevel,
                        NULL AS PositionNew,
                        NULL AS PositionID
                    FROM vps_empinfo_mes ve
                    LEFT JOIN base_people bp ON ve.empcode = bp.Code
                    WHERE " + _sqlVpsManufactureGdname4Predicate + @"
                        AND ve.isactive = 1  -- 在制员工
                        AND bp.Code IS NULL;  -- 在base_people中不存在
                ";

                result.AffectedRows = await _dbHelper.ExecuteNonQueryAsync(sql);
                stopwatch.Stop();

                result.Success = true;
                result.Message = $"成功插入 {result.AffectedRows} 条记录到 base_people";
                result.ExecutionDuration = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("[{TaskName}] {Message}, 耗时: {Duration}ms", 
                    result.TaskName, result.Message, result.ExecutionDuration);
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogInformationAsync(
                        result.Message, "UserSync", "SyncBasePeopleFromVps", null, "ExecuteSync");
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Message = $"同步失败：{ex.Message}";
                result.Exception = ex;
                result.ExecutionDuration = stopwatch.ElapsedMilliseconds;

                _logger.LogError(ex, "[{TaskName}] 同步失败", result.TaskName);
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogErrorAsync(
                        $"同步失败：{result.TaskName}", ex, "UserSync", "SyncBasePeopleFromVps", null, "ExecuteSync");
                }
            }

            return result;
        }

        /// <summary>
        /// 更新密码为空的账号为默认密码123456
        /// </summary>
        public async Task<SyncResult> UpdateEmptyPasswordsAsync()
        {
            var result = new SyncResult { TaskName = "更新密码为空的账号" };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (_passwordService == null)
                {
                    result.Success = false;
                    result.Message = "密码服务未初始化";
                    return result;
                }

                var (success, affectedRows, message) = await _passwordService.UpdateAllUserPasswordsToDefaultAsync(updateAll: false);
                
                stopwatch.Stop();

                result.Success = success;
                result.AffectedRows = affectedRows;
                result.Message = message;
                result.ExecutionDuration = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("[{TaskName}] {Message}, 耗时: {Duration}ms", 
                    result.TaskName, result.Message, result.ExecutionDuration);
                
                if (_databaseLogService != null)
                {
                    if (success)
                    {
                        await _databaseLogService.LogInformationAsync(
                            result.Message, "UserSync", "UpdateEmptyPasswords", null, "ExecuteSync");
                    }
                    else
                    {
                        await _databaseLogService.LogErrorAsync(
                            result.Message, null, "UserSync", "UpdateEmptyPasswords", null, "ExecuteSync");
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Message = $"更新密码失败：{ex.Message}";
                result.Exception = ex;
                result.ExecutionDuration = stopwatch.ElapsedMilliseconds;

                _logger.LogError(ex, "[{TaskName}] 更新密码失败", result.TaskName);
                
                if (_databaseLogService != null)
                {
                    await _databaseLogService.LogErrorAsync(
                        $"更新密码失败：{result.TaskName}", ex, "UserSync", "UpdateEmptyPasswords", null, "ExecuteSync");
                }
            }

            return result;
        }
    }
}
