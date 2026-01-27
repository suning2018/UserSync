namespace UserSync.Constants
{
    /// <summary>
    /// 配置键常量
    /// </summary>
    public static class ConfigKeys
    {
        public const string ConnectionStringSQLServer = "ConnectionStrings:SQLServer";
        public const string LogPath = "LogSettings:LogPath";
        public const string LogFileName = "LogSettings:LogFileName";
        public const string RetainDays = "LogSettings:RetainDays";
        public const string EnableDatabaseLog = "LogSettings:EnableDatabaseLog";
        public const string CheckIntervalSeconds = "SyncSettings:CheckIntervalSeconds";
        public const string EnableAutoSync = "SyncSettings:EnableAutoSync";
    }
}
