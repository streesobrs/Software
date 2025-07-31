using Software.其他界面;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

namespace Software
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        string databasePath = DatabaseHelper.GetDatabasePath();

        // 全局变量：存储更新相关参数
        public static bool IsUpdated { get; private set; } = false;
        public static string UpdateTime { get; private set; } = "";
        public static string UpdateVersion { get; private set; } = "";

        private string GetConfigValueFromDatabase(string dbPath, string key)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    string query = "SELECT Value FROM Settings WHERE Key = @Key;";
                    var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@Key", key);
                    return command.ExecuteScalar()?.ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取配置值时发生错误: " + ex.Message);
                return null;
            }
        }

        public App()
        {
            var culture = GetConfigValueFromDatabase(databasePath, "Culture");
            CultureInfo cultureInfo;

            if (!string.IsNullOrEmpty(culture))
            {
                try
                {
                    cultureInfo = new CultureInfo(culture);
                }
                catch (CultureNotFoundException)
                {
                    MessageBox.Show($"未找到指定的区域设置: {culture}，将使用默认区域设置。");
                    cultureInfo = CultureInfo.CurrentCulture;
                }
            }
            else
            {
                cultureInfo = CultureInfo.CurrentCulture;
            }
            LocalizeDictionary.Instance.Culture = cultureInfo;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // 1. 处理启动参数（优先执行，确保更新时间尽早保存）
                HandleStartupArguments(e.Args);

                base.OnStartup(e);

                // 2. 初始化数据库
                InitializeDatabase();

                #region Serilog配置
                string logOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} || {Level} || {SourceContext:l} || {Message} || {Exception} ||end {NewLine}";
                Log.Logger = new LoggerConfiguration()
                  .MinimumLevel.Override("Default", LogEventLevel.Information)
                  .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                  .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                  .Enrich.FromLogContext()
                  .WriteTo.File($"{AppContext.BaseDirectory}logs/log.log", rollingInterval: RollingInterval.Day, outputTemplate: logOutputTemplate)
                  .CreateLogger();
                #endregion

                #region 启动ASP.NET Core主机
                var host = Host.CreateDefaultBuilder(e.Args)
                    .UseSerilog()
                    .ConfigureWebHostDefaults(webBuilder => {
                        webBuilder.UseStartup<Startup>();
                    }).Build();

                host.RunAsync();
                #endregion
            }
            catch (Exception ex)
            {
                // 异常处理
                File.WriteAllText("error.log", ex.ToString());
                Log.Logger.Error(ex.ToString());
                MessageBox.Show("应用程序在启动时遇到了一个错误。请查看 error.log 文件以获取更多信息。");
                this.Shutdown();
            }
        }

        /// <summary>
        /// 处理启动参数，区分正常启动和更新后启动
        /// </summary>
        private void HandleStartupArguments(string[] args)
        {
            if (args.Length == 0)
            {
                Debug.WriteLine("正常启动：无参数");
                return;
            }

            try
            {
                // 只校验第一个参数是否为"updated"，并提取时间
                if (args[0] == "updated")
                {
                    IsUpdated = true;
                    // 只处理第二个参数（更新时间）
                    UpdateTime = args.Length > 1 ? args[1] : "未知时间";
                    Debug.WriteLine($"更新后启动：时间={UpdateTime}");

                    // 保存更新时间到数据库（逻辑不变）
                    if (!string.IsNullOrEmpty(UpdateTime) && UpdateTime != "未知时间")
                    {
                        SaveLastUpdateTime(databasePath, UpdateTime);
                    }
                }
                else
                {
                    Debug.WriteLine("参数格式不匹配，按正常启动处理");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"参数解析错误：{ex.Message}");
            }
        }

        /// <summary>
        /// 保存上次更新时间到数据库
        /// </summary>
        private void SaveLastUpdateTime(string dbPath, string lastUpdateTime)
        {
            try
            {
                // 验证数据库路径
                if (!File.Exists(dbPath))
                {
                    Debug.WriteLine($"数据库文件不存在：{dbPath}");
                    return;
                }

                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    // 使用INSERT OR REPLACE确保存在则更新，不存在则插入
                    string query = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('LastUpdateTime', @LastUpdateTime);";
                    var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@LastUpdateTime", lastUpdateTime);
                    int rowsAffected = command.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        Debug.WriteLine($"成功保存更新时间到数据库：{lastUpdateTime}");
                        Log.Logger.Information($"更新时间已保存：{lastUpdateTime}");
                    }
                    else
                    {
                        Debug.WriteLine("保存更新时间到数据库，但未影响任何行");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error("保存LastUpdateTime时发生错误: {error}", ex.ToString());
                MessageBox.Show("保存更新时间时发生错误: " + ex.Message);
            }
        }

        // 获取数据库路径的方法
        private string GetDatabasePath()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string folderPath = Path.Combine(documentsPath, "StreeDB");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

#if DEBUG
            return Path.Combine(folderPath, "debug_SoftwareDatabase.db");
#else
                return Path.Combine(folderPath, "SoftwareDatabase.db");
#endif
        }

        // 初始化数据库的方法
        private void InitializeDatabase()
        {
            string databasePath = GetDatabasePath();
            bool isNewDatabase = !File.Exists(databasePath);

            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();

                try
                {
                    if (isNewDatabase)
                    {
                        CreateSettingsTable(connection);
                        CreateButtonVisibilityTable(connection);
                        InsertInitialData(connection);
                    }
                    else
                    {
                        if (ColumnExists(connection, "Settings", "MigrationVersion"))
                        {
                            DeleteColumn(connection, "Settings", "MigrationVersion");
                        }

                        CreateSettingsTable(connection);
                        CreateButtonVisibilityTable(connection);
                        CheckSettingsKeys(connection);
                        CheckButtonVisibilityKeys(connection);
                        PerformOriginalMigration(connection);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Database initialization failed: " + ex.Message);
                    Log.Logger.Error("数据库初始化失败: {error}", ex.ToString());
                }
            }
        }

        private void CreateSettingsTable(SqliteConnection connection)
        {
            string createTableQuery = "CREATE TABLE IF NOT EXISTS Settings (Key TEXT NOT NULL UNIQUE, Value TEXT NOT NULL, MigrationVersion INTEGER DEFAULT 0);";
            var command = new SqliteCommand(createTableQuery, connection);
            command.ExecuteNonQuery();
        }

        private void CreateButtonVisibilityTable(SqliteConnection connection)
        {
            string createButtonVisibilityTableQuery = "CREATE TABLE IF NOT EXISTS ButtonVisibility (ButtonName TEXT NOT NULL UNIQUE, IsVisible INTEGER NOT NULL);";
            var buttonVisibilityCommand = new SqliteCommand(createButtonVisibilityTableQuery, connection);
            buttonVisibilityCommand.ExecuteNonQuery();
        }

        private void InsertInitialData(SqliteConnection connection)
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
            ('LastUpdateTime', '');";

            var insertCommand = new SqliteCommand(insertInitialDataQuery, connection);
            insertCommand.ExecuteNonQuery();

            string insertButtonVisibilityDataQuery = @"
            INSERT INTO ButtonVisibility (ButtonName, IsVisible) VALUES 
            ('Button_GenshinMap', 0),
            ('Button_SelectUP', 0),
            ('Button_PlayGames', 0),
            ('Button_GenshinRole', 0),
            ('Button_HonkaiImpact3', 0),
            ('Button_StarRail', 0),
            ('Button_MoveChest', 0),
            ('Button_Bing', 1),
            ('Button_StreePortal', 1),
            ('Button_MusicPlayer', 1);";

            var insertButtonVisibilityCommand = new SqliteCommand(insertButtonVisibilityDataQuery, connection);
            insertButtonVisibilityCommand.ExecuteNonQuery();
        }

        private void CheckSettingsKeys(SqliteConnection connection)
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
                { "LastUpdateTime", "" }
            };

            string selectKeysQuery = "SELECT Key FROM Settings;";
            var selectCommand = new SqliteCommand(selectKeysQuery, connection);
            var existingKeys = new HashSet<string>();

            using (var reader = selectCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    existingKeys.Add(reader.GetString(0));
                }
            }

            foreach (var key in requiredKeys)
            {
                if (!existingKeys.Contains(key.Key))
                {
                    string insertKeyQuery = "INSERT INTO Settings (Key, Value) VALUES (@key, @value);";
                    var insertCommand = new SqliteCommand(insertKeyQuery, connection);
                    insertCommand.Parameters.AddWithValue("@key", key.Key);
                    insertCommand.Parameters.AddWithValue("@value", key.Value);
                    insertCommand.ExecuteNonQuery();
                }
            }
        }

        private void CheckButtonVisibilityKeys(SqliteConnection connection)
        {
            var requiredButtons = new Dictionary<string, int>
            {
                { "Button_GenshinMap", 0 },
                { "Button_SelectUP", 0 },
                { "Button_PlayGames", 0 },
                { "Button_GenshinRole", 0 },
                { "Button_HonkaiImpact3", 0 },
                { "Button_StarRail", 0 },
                { "Button_MoveChest", 0 },
                { "Button_Bing", 1 },
                { "Button_StreePortal", 1 },
                { "Button_MusicPlayer", 1 }
            };

            string selectButtonsQuery = "SELECT ButtonName FROM ButtonVisibility;";
            var selectCommand = new SqliteCommand(selectButtonsQuery, connection);
            var existingButtons = new HashSet<string>();

            using (var reader = selectCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    existingButtons.Add(reader.GetString(0));
                }
            }

            foreach (var button in requiredButtons)
            {
                if (!existingButtons.Contains(button.Key))
                {
                    string insertButtonQuery = "INSERT INTO ButtonVisibility (ButtonName, IsVisible) VALUES (@buttonName, @isVisible);";
                    var insertCommand = new SqliteCommand(insertButtonQuery, connection);
                    insertCommand.Parameters.AddWithValue("@buttonName", button.Key);
                    insertCommand.Parameters.AddWithValue("@isVisible", button.Value);
                    insertCommand.ExecuteNonQuery();
                }
            }
        }

        private bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
        {
            bool columnExists = false;
            string sql = $"PRAGMA table_info({tableName});";

            using (var command = new SqliteCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string columnNameInTable = reader.GetString(1);
                    if (columnNameInTable == columnName)
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            return columnExists;
        }

        private void DeleteColumn(SqliteConnection connection, string tableName, string columnName)
        {
            string deleteColumnSql = $"ALTER TABLE {tableName} DROP COLUMN {columnName};";
            using (var command = new SqliteCommand(deleteColumnSql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private void MigrateDataFromConfig(SqliteConnection connection)
        {
            try
            {
                string getVersionQuery = "SELECT Value FROM Settings WHERE Key = 'MigrationVersion';";
                var getVersionCommand = new SqliteCommand(getVersionQuery, connection);
                int currentVersion = Convert.ToInt32(getVersionCommand.ExecuteScalar());
                int latestVersion = 1;

                for (int version = currentVersion + 1; version <= latestVersion; version++)
                {
                    switch (version)
                    {
                        case 2:
                            MigrateToVersion2(connection);
                            break;
                    }

                    string updateVersionQuery = "UPDATE Settings SET Value = @version WHERE Key = 'MigrationVersion';";
                    var updateVersionCommand = new SqliteCommand(updateVersionQuery, connection);
                    updateVersionCommand.Parameters.AddWithValue("@version", version);
                    updateVersionCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数据迁移失败: {ex.Message}");
            }
        }

        private void MigrateToVersion2(SqliteConnection connection)
        {
            // 版本2的迁移逻辑
        }

        private void PerformOriginalMigration(SqliteConnection connection)
        {
            try
            {
                string checkMigrationQuery = "SELECT COUNT(*) FROM Settings WHERE Key = 'MigrationCompleted';";
                using (var checkCommand = new SqliteCommand(checkMigrationQuery, connection))
                {
                    int migrationCount = Convert.ToInt32(checkCommand.ExecuteScalar());

                    if (migrationCount == 0)
                    {
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
                                upsertCommand.ExecuteNonQuery();
                            }
                        }

                        var settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Software");
                        var softwareUrlDirectories = Directory.GetDirectories(settingsDirectory)
                            .Where(dir => Path.GetFileName(dir).StartsWith("Software_Url_"));

                        var firstSoftwareUrlDirectory = softwareUrlDirectories.FirstOrDefault();
                        if (firstSoftwareUrlDirectory != null)
                        {
                            var firstSubDirectory = Directory.GetDirectories(firstSoftwareUrlDirectory).FirstOrDefault();
                            if (firstSubDirectory != null)
                            {
                                var userConfigPath = Path.Combine(firstSubDirectory, "user.config");
                                XDocument doc = XDocument.Load(userConfigPath);
                                var settingsElement = doc.Root.Element("userSettings").Element("Software.Properties.Settings");
                                if (settingsElement != null)
                                {
                                    foreach (var setting in settingsElement.Elements("setting"))
                                    {
                                        string rawKey = setting.Attribute("name").Value;
                                        string key = rawKey.EndsWith("_Display") ? rawKey.Substring(0, rawKey.Length - "_Display".Length) : rawKey;
                                        string value = setting.Element("value").Value;

                                        if (key != "LastUpdateTime")
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
                                                    upsertCommand.Parameters.AddWithValue("@ButtonName", key);
                                                    upsertCommand.Parameters.AddWithValue("@IsVisible", isVisibleInt);
                                                    upsertCommand.ExecuteNonQuery();
                                                }
                                            }
                                            else
                                            {
                                                MessageBox.Show($"警告: 无法解析键 '{key}' 的布尔值: {value}");
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
                                                upsertCommand.ExecuteNonQuery();
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        string markMigrationQuery = "INSERT INTO Settings (Key, Value) VALUES ('MigrationCompleted', 'true');";
                        using (var markCommand = new SqliteCommand(markMigrationQuery, connection))
                        {
                            markCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数据迁移失败: {ex.Message}");
            }
        }
    }
}
