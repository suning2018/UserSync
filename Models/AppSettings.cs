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
        public SyncTasks SyncTasks { get; set; } = new();
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
        public bool SyncBasePeopleManufactureGroup { get; set; } = false;
    }
}
