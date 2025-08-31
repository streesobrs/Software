using Software.其他界面;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Data;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System.Xml.Linq;
using Software.Models;
using Microsoft.Data.Sqlite;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Text.RegularExpressions;
using WPFLocalizeExtension.Engine;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;

namespace Software
{
    public partial class App : Application
    {
        private static readonly string _databasePath;
        private static readonly string _logDirectory;
        private Task _setCultureTask;
        private IHost _webHost;

        public static bool IsUpdated { get; private set; } = false;
        public static string UpdateTime { get; private set; } = "";
        public static string UpdateVersion { get; private set; } = "";

        static App()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string folderPath = Path.Combine(documentsPath, "StreeDB");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

#if DEBUG
            _databasePath = Path.Combine(folderPath, "debug_SoftwareDatabase.db");
#else
            _databasePath = Path.Combine(folderPath, "SoftwareDatabase.db");
#endif

            _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        }

        public App()
        {
            // 添加全局异常处理
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            InitializeSerilog();
            _setCultureTask = SetCultureAsync();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Logger.Fatal(e.ExceptionObject as Exception, "未处理的应用程序域异常");
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Logger.Fatal(e.Exception, "未处理的UI线程异常");
            e.Handled = true;
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Logger.Fatal(e.Exception, "未观察的任务异常");
            e.SetObserved();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            var startupStopwatch = Stopwatch.StartNew();

            try
            {
                // 等待文化设置完成
                await _setCultureTask;

                // 并行执行初始化任务
                var tasks = new List<Task>
                {
                    HandleStartupArgumentsAsync(e.Args),
                    InitializeDatabaseAsync()
                };

                await Task.WhenAll(tasks);

                // 启动Web主机（但不等待它完成）
                _ = StartWebHostAsync(e.Args);

                base.OnStartup(e);

                //var mainWindow = new MainWindow();
                //mainWindow.Show();

                startupStopwatch.Stop();
                Log.Logger.Information($"应用程序启动完成，总耗时: {startupStopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                startupStopwatch.Stop();
                Log.Logger.Fatal(ex, $"应用程序启动失败，耗时: {startupStopwatch.ElapsedMilliseconds}ms");
                File.WriteAllText("error.log", ex.ToString());
                MessageBox.Show("应用程序在启动时遇到了一个错误。请查看 error.log 文件以获取更多信息。");
                this.Shutdown();
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            // 确保Web主机正确关闭
            if (_webHost != null)
            {
                try
                {
                    await _webHost.StopAsync(TimeSpan.FromSeconds(5));
                    _webHost.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, "停止Web主机时发生错误");
                }
            }

            base.OnExit(e);
        }

        private void InitializeSerilog()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }

                string logPath = Path.Combine(_logDirectory, "log.log");

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Override("Default", LogEventLevel.Information)
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                    .Enrich.FromLogContext()
                    .WriteTo.File(
                        path: logPath,
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} || {Level} || {SourceContext:l} || {Message} || {Exception} ||end {NewLine}",
                        buffered: false,
                        shared: true
                    )
                    .CreateLogger();

                Log.Logger.Information("日志系统初始化完成");
            }
            catch (Exception ex)
            {
                // 如果日志初始化失败，至少记录到事件查看器或控制台
                EventLog.WriteEntry("Application", $"日志初始化失败: {ex.Message}", EventLogEntryType.Error);
                Console.WriteLine($"日志初始化失败: {ex.Message}");
            }
        }

        private async Task SetCultureAsync()
        {
            try
            {
                string culture = await GetConfigValueFromDatabaseAsync("Culture");
                CultureInfo cultureInfo;

                if (!string.IsNullOrEmpty(culture))
                {
                    try
                    {
                        cultureInfo = new CultureInfo(culture);
                    }
                    catch (CultureNotFoundException ex)
                    {
                        Log.Logger.Warning(ex, $"未找到指定的区域设置: {culture}，将使用默认区域设置");
                        cultureInfo = CultureInfo.CurrentCulture;
                    }
                }
                else
                {
                    cultureInfo = CultureInfo.CurrentCulture;
                }

                // 添加空检查
                if (LocalizeDictionary.Instance != null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        LocalizeDictionary.Instance.Culture = cultureInfo;
                    });
                }
                else
                {
                    Log.Logger.Warning("LocalizeDictionary.Instance 为 null，无法设置文化");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "设置区域文化时发生错误");
            }
        }

        private async Task<string> GetConfigValueFromDatabaseAsync(string key)
        {
            try
            {
                if (!File.Exists(_databasePath))
                {
                    Log.Logger.Warning($"数据库文件不存在：{_databasePath}");
                    return null;
                }

                using (var connection = new SqliteConnection($"Data Source={_databasePath};Cache=Shared"))
                {
                    await connection.OpenAsync();
                    string query = "SELECT Value FROM Settings WHERE Key = @Key;";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Key", key);
                        var result = await command.ExecuteScalarAsync();
                        return result?.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"读取配置值时发生错误 (Key: {key})");
                return null;
            }
        }

        private async Task HandleStartupArgumentsAsync(string[] args)
        {
            if (args.Length == 0)
            {
                Log.Logger.Information("正常启动：无参数");
                return;
            }

            try
            {
                if (args[0] == "updated")
                {
                    IsUpdated = true;
                    UpdateTime = args.Length > 1 ? args[1] : "未知时间";
                    Log.Logger.Information($"更新后启动：时间={UpdateTime}");

                    if (!string.IsNullOrEmpty(UpdateTime) && UpdateTime != "未知时间")
                    {
                        await SaveLastUpdateTimeAsync(UpdateTime);
                    }
                }
                else
                {
                    Log.Logger.Information("参数格式不匹配，按正常启动处理");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "参数解析错误");
            }
        }

        private async Task SaveLastUpdateTimeAsync(string lastUpdateTime)
        {
            try
            {
                if (!File.Exists(_databasePath))
                {
                    Log.Logger.Warning($"数据库文件不存在：{_databasePath}");
                    return;
                }

                using (var connection = new SqliteConnection($"Data Source={_databasePath};Cache=Shared"))
                {
                    await connection.OpenAsync();
                    string query = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('LastUpdateTime', @LastUpdateTime);";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@LastUpdateTime", lastUpdateTime);
                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            Log.Logger.Information($"成功保存更新时间到数据库：{lastUpdateTime}");
                        }
                        else
                        {
                            Log.Logger.Warning("保存更新时间到数据库，但未影响任何行");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "保存LastUpdateTime时发生错误");
            }
        }

        private async Task InitializeDatabaseAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                bool isNewDatabase = !File.Exists(_databasePath);

                using (var connection = new SqliteConnection($"Data Source={_databasePath};Cache=Shared"))
                {
                    await connection.OpenAsync();

                    if (isNewDatabase)
                    {
                        Log.Logger.Information("检测到新数据库，开始初始化");
                        await CreateSettingsTableAsync(connection);
                        await InsertInitialSettingsDataAsync(connection);
                        await CreateSongPlayStatsTableAsync(connection);
                        await CreateTotalPlayStatsTableAsync(connection);
                    }
                    else
                    {
                        Log.Logger.Information("检测到现有数据库，开始验证和更新");
                        if (await ColumnExistsAsync(connection, "Settings", "MigrationVersion"))
                        {
                            await DeleteColumnAsync(connection, "Settings", "MigrationVersion");
                        }

                        await CreateSettingsTableAsync(connection);
                        await CheckSettingsKeysAsync(connection);
                        await PerformOriginalMigrationAsync(connection);
                        await CreateSongPlayStatsTableAsync(connection);
                        await CreateTotalPlayStatsTableAsync(connection);
                    }
                }

                stopwatch.Stop();
                Log.Logger.Information($"数据库初始化完成，耗时: {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Log.Logger.Error(ex, $"数据库初始化失败，耗时: {stopwatch.ElapsedMilliseconds}ms");
            }
        }

        private async Task CreateSettingsTableAsync(SqliteConnection connection)
        {
            string createTableQuery = "CREATE TABLE IF NOT EXISTS Settings (Key TEXT NOT NULL UNIQUE, Value TEXT NOT NULL, MigrationVersion INTEGER DEFAULT 0);";
            using (var command = new SqliteCommand(createTableQuery, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task InsertInitialSettingsDataAsync(SqliteConnection connection)
        {
            string insertInitialDataQuery = @"
            INSERT INTO Settings (Key, Value) VALUES 
            ('GamePath', ''),
            ('TextContent', ''),
            ('UpdatePath', 'https://gitee.com/nibadianbanxiaban/software/raw/main/updata/update.xml'),
            ('LaunchCount', '0'),
            ('EnableCounting', 'false'),
            ('RetryCount', '2'),
            ('RetryDelay', '5'),
            ('adcode', ''),
            ('UpdateLogUrl', 'https://gitee.com/nibadianbanxiaban/software/raw/main/resources/update_log.json'),
            ('VersionColor', 'Red'),
            ('UpdateTimeColor', 'Blue'),
            ('Culture', 'zh-CN'),
            ('EnableAutoUpdate', 'true'),
            ('NewUpdatePath', 'https://gitee.com/nibadianbanxiaban/software/raw/main/updata/new_update.json'),
            ('LastUpdateTime', ''),
            ('MigrationVersion', '1');";

            using (var insertCommand = new SqliteCommand(insertInitialDataQuery, connection))
            {
                await insertCommand.ExecuteNonQueryAsync();
            }
        }

        private async Task CheckSettingsKeysAsync(SqliteConnection connection)
        {
            var requiredKeys = new Dictionary<string, string>
            {
                { "GamePath", "" },
                { "TextContent", "" },
                { "UpdatePath", "https://gitee.com/nibadianbanxiaban/software/raw/main/updata/update.xml" },
                { "LaunchCount", "0" },
                { "EnableCounting", "false" },
                { "RetryCount", "2" },
                { "RetryDelay", "5" },
                { "adcode", "" },
                { "UpdateLogUrl", "https://gitee.com/nibadianbanxiaban/software/raw/main/resources/update_log.json" },
                { "VersionColor", "Red" },
                { "UpdateTimeColor", "Blue" },
                { "Culture", "zh-CN" },
                { "EnableAutoUpdate", "true" },
                { "NewUpdatePath", "https://gitee.com/nibadianbanxiaban/software/raw/main/updata/new_update.json" },
                { "LastUpdateTime", "" },
                { "MigrationVersion", "1" }
            };

            string selectKeysQuery = "SELECT Key FROM Settings;";
            using (var selectCommand = new SqliteCommand(selectKeysQuery, connection))
            {
                var existingKeys = new HashSet<string>();
                using (var reader = await selectCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        existingKeys.Add(reader.GetString(0));
                    }
                }

                foreach (var key in requiredKeys)
                {
                    if (!existingKeys.Contains(key.Key))
                    {
                        string insertKeyQuery = "INSERT INTO Settings (Key, Value) VALUES (@key, @value);";
                        using (var insertCommand = new SqliteCommand(insertKeyQuery, connection))
                        {
                            insertCommand.Parameters.AddWithValue("@key", key.Key);
                            insertCommand.Parameters.AddWithValue("@value", key.Value);
                            await insertCommand.ExecuteNonQueryAsync();
                            Log.Logger.Information($"添加缺失的设置键: {key.Key}");
                        }
                    }
                }
            }
        }

        // 创建歌曲播放统计表
        private async Task CreateSongPlayStatsTableAsync(SqliteConnection connection)
        {
            string createTableQuery = @"
        CREATE TABLE IF NOT EXISTS SongPlayStats (
            FilePath TEXT NOT NULL PRIMARY KEY,
            PlayCount INTEGER NOT NULL DEFAULT 0,
            TotalPlayDuration REAL NOT NULL DEFAULT 0
        );";
            using (var command = new SqliteCommand(createTableQuery, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }

        // 创建总播放统计表
        private async Task CreateTotalPlayStatsTableAsync(SqliteConnection connection)
        {
            string createTableQuery = @"
        CREATE TABLE IF NOT EXISTS TotalPlayStats (
            Id INTEGER NOT NULL PRIMARY KEY DEFAULT 1,
            TotalPlayCount INTEGER NOT NULL DEFAULT 0,
            TotalPlayDuration REAL NOT NULL DEFAULT 0
        );";
            using (var command = new SqliteCommand(createTableQuery, connection))
            {
                await command.ExecuteNonQueryAsync();
            }

            // 插入初始行（如果不存在）
            string insertQuery = @"
        INSERT OR IGNORE INTO TotalPlayStats (Id, TotalPlayCount, TotalPlayDuration)
        VALUES (1, 0, 0);";
            using (var command = new SqliteCommand(insertQuery, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task<bool> ColumnExistsAsync(SqliteConnection connection, string tableName, string columnName)
        {
            string sql = $"PRAGMA table_info({tableName});";
            using (var command = new SqliteCommand(sql, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    string columnNameInTable = reader.GetString(1);
                    if (columnNameInTable == columnName)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private async Task DeleteColumnAsync(SqliteConnection connection, string tableName, string columnName)
        {
            string deleteColumnSql = $"ALTER TABLE {tableName} DROP COLUMN {columnName};";
            using (var command = new SqliteCommand(deleteColumnSql, connection))
            {
                await command.ExecuteNonQueryAsync();
                Log.Logger.Information($"从表 {tableName} 中删除列 {columnName}");
            }
        }

        private async Task PerformOriginalMigrationAsync(SqliteConnection connection)
        {
            try
            {
                string migrationVersion = await GetConfigValueFromDatabaseAsync(connection, "MigrationVersion") ?? "0";
                if (migrationVersion == "1")
                {
                    Log.Logger.Information("数据迁移已完成，无需重复执行");
                    return;
                }

                Log.Logger.Information("开始执行数据迁移");
                var appSettings = ConfigurationManager.AppSettings;
                foreach (var key in appSettings.AllKeys)
                {
                    string value = appSettings[key];
                    string upsertKeyQuery = @"  
INSERT INTO Settings (Key, Value) VALUES (@key, @value)  
ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";
                    using (var upsertCommand = new SqliteCommand(upsertKeyQuery, connection))
                    {
                        upsertCommand.Parameters.AddWithValue("@key", key);
                        upsertCommand.Parameters.AddWithValue("@value", value);
                        await upsertCommand.ExecuteNonQueryAsync();
                    }
                }

                var settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Software");
                if (Directory.Exists(settingsDirectory))
                {
                    var softwareUrlDirectories = Directory.GetDirectories(settingsDirectory)
                        .Where(dir => Path.GetFileName(dir).StartsWith("Software_Url_"));

                    var firstSoftwareUrlDirectory = softwareUrlDirectories.FirstOrDefault();
                    if (firstSoftwareUrlDirectory != null)
                    {
                        var firstSubDirectory = Directory.GetDirectories(firstSoftwareUrlDirectory).FirstOrDefault();
                        if (firstSubDirectory != null)
                        {
                            var userConfigPath = Path.Combine(firstSubDirectory, "user.config");
                            if (File.Exists(userConfigPath))
                            {
                                XDocument doc;
                                using (var stream = new FileStream(userConfigPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                                {
                                    doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
                                }

                                var settingsElement = doc.Root?.Element("userSettings")?.Element("Software.Properties.Settings");
                                if (settingsElement != null)
                                {
                                    await CreateButtonVisibilityTableAsync(connection);

                                    foreach (var setting in settingsElement.Elements("setting"))
                                    {
                                        var nameAttr = setting.Attribute("name");
                                        if (nameAttr == null) continue;

                                        string rawKey = nameAttr.Value;
                                        var valueElement = setting.Element("value");
                                        if (valueElement == null) continue;

                                        string value = valueElement.Value;

                                        if (rawKey != "LastUpdateTime")
                                        {
                                            if (bool.TryParse(value, out bool isVisible))
                                            {
                                                string upsertButtonVisibilityQuery = @"  
INSERT INTO ButtonVisibility (ButtonName, IsVisible)  
VALUES (@ButtonName, @IsVisible)  
ON CONFLICT(ButtonName) DO UPDATE SET IsVisible = excluded.IsVisible;";

                                                using (var upsertCommand = new SqliteCommand(upsertButtonVisibilityQuery, connection))
                                                {
                                                    int isVisibleInt = isVisible ? 1 : 0;
                                                    upsertCommand.Parameters.AddWithValue("@ButtonName", rawKey);
                                                    upsertCommand.Parameters.AddWithValue("@IsVisible", isVisibleInt);
                                                    await upsertCommand.ExecuteNonQueryAsync();
                                                }
                                            }
                                            else
                                            {
                                                Log.Logger.Warning($"无法解析键 '{rawKey}' 的布尔值: {value}");
                                            }
                                        }
                                        else
                                        {
                                            string upsertKeyQuery = @"  
INSERT INTO Settings (Key, Value) VALUES (@key, @value)  
ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";

                                            using (var upsertCommand = new SqliteCommand(upsertKeyQuery, connection))
                                            {
                                                upsertCommand.Parameters.AddWithValue("@key", rawKey);
                                                upsertCommand.Parameters.AddWithValue("@value", value);
                                                await upsertCommand.ExecuteNonQueryAsync();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                string markMigrationQuery = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('MigrationVersion', '1');";
                using (var markCommand = new SqliteCommand(markMigrationQuery, connection))
                {
                    await markCommand.ExecuteNonQueryAsync();
                }

                Log.Logger.Information("数据迁移完成");
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "数据迁移失败");
            }
        }

        private async Task CreateButtonVisibilityTableAsync(SqliteConnection connection)
        {
            string createButtonVisibilityTableQuery = "CREATE TABLE IF NOT EXISTS ButtonVisibility (ButtonName TEXT NOT NULL UNIQUE, IsVisible INTEGER NOT NULL);";
            using (var buttonVisibilityCommand = new SqliteCommand(createButtonVisibilityTableQuery, connection))
            {
                await buttonVisibilityCommand.ExecuteNonQueryAsync();
            }
        }

        private async Task<string> GetConfigValueFromDatabaseAsync(SqliteConnection connection, string key)
        {
            try
            {
                string query = "SELECT Value FROM Settings WHERE Key = @Key;";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Key", key);
                    var result = await command.ExecuteScalarAsync();
                    return result?.ToString();
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"读取配置值时发生错误 (Key: {key})");
                return null;
            }
        }

        private async Task StartWebHostAsync(string[] args)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                _webHost = Host.CreateDefaultBuilder(args)
                    .UseSerilog()
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseStartup<Startup>()
                                  .UseUrls("http://localhost:5000");
                    }).Build();

                Log.Logger.Information("开始启动Web主机");
                await _webHost.StartAsync(); // 使用StartAsync而不是RunAsync，这样不会阻塞
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Log.Logger.Error(ex, $"Web主机启动失败，耗时: {stopwatch.ElapsedMilliseconds}ms");
            }
        }
    }
}