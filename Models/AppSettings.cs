namespace UserSync.Models
{
    /// <summary>
    /// 应用程序配置模型
    /// </summary>
    public class AppSettings
    {
        public ConnectionStrings ConnectionStrings { get; set; } = new();
        public LogSettings LogSettings { get; set; } = new();
        public SyncSettings SyncSettings { get; set; } = new();
    }

    /// <summary>
    /// 数据库连接字符串配置
    /// </summary>
    public class ConnectionStrings
    {
        public string SQLServer { get; set; } = string.Empty;
    }

    /// <summary>
    /// 日志设置
    /// </summary>
    public class LogSettings
    {
        public string LogPath { get; set; } = "Logs";
        public string LogFileName { get; set; } = "user-sync-{Date}.log";
        public int RetainDays { get; set; } = 30;
        public bool EnableDatabaseLog { get; set; } = true;
    }

    /// <summary>
    /// 同步设置
    /// </summary>
    public class SyncSettings
    {
        public int CheckIntervalSeconds { get; set; } = 3600;
        public bool EnableAutoSync { get; set; } = true;

        /// <summary>
        /// vps_empinfo_mes.gdname4 与写入 Sys_User/base_people 的 Factory 字面值对应；顺序决定 CASE 分支优先级。
        /// </summary>
        public List<ManufactureGdname4FactoryItem> ManufactureGdname4Factories { get; set; } = new();

        /// <summary>
        /// 未出现在 <see cref="ManufactureGdname4Factories"/> 中的 gdname4 对应的 Factory（通常为空字符串）。
        /// </summary>
        public string ManufactureGdname4DefaultFactory { get; set; } = string.Empty;

        public SyncTasks SyncTasks { get; set; } = new();
    }

    /// <summary>
    /// 制造部 gdname4 与 Factory 映射（来自 appsettings.json）。
    /// </summary>
    public class ManufactureGdname4FactoryItem
    {
        public string Gdname4 { get; set; } = string.Empty;
        public string Factory { get; set; } = string.Empty;
    }

    /// <summary>
    /// 同步任务配置
    /// </summary>
    public class SyncTasks
    {
        public bool SyncNewUsersFromVps { get; set; } = true;
        public bool GenerateUserLogOn { get; set; } = true;
        public bool UpdateEmptyPasswords { get; set; } = true;
        public bool SyncBasePeopleFromVps { get; set; } = true;
        public bool SyncBasePeopleSysUser { get; set; } = true;
        public bool SyncSysUserEnabledMark { get; set; } = true;
        public bool SyncBasePeopleJobStatus { get; set; } = true;
    }
}
