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

namespace Software
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {

                base.OnStartup(e);

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
                // 将异常信息写入日志
                File.WriteAllText("error.log", ex.ToString());
                Log.Logger.Error(ex.ToString());

                // 显示一个错误消息
                MessageBox.Show("应用程序在启动时遇到了一个错误。请查看 error.log 文件以获取更多信息。");

                // 关闭应用程序
                this.Shutdown();
            }

            base.OnStartup(e);
        }

        // 获取数据库路径的方法
        private string GetDatabasePath()
        {
            // 获取用户文档目录的路径
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            // 在文档目录下创建一个名为StreeDB的文件夹
            string folderPath = Path.Combine(documentsPath, "StreeDB");

            // 如果文件夹不存在，则创建它
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // 根据编译条件（Debug或Release）设置数据库文件的名称
            #if DEBUG
                return Path.Combine(folderPath, "debug_SoftwareDatabase.db");
            #else
                return Path.Combine(folderPath, "SoftwareDatabase.db");
            #endif
        }

        // 初始化数据库的方法
        private void InitializeDatabase()
        {
            // 获取数据库文件的完整路径
            string databasePath = GetDatabasePath();
            // 判断数据库文件是否存在
            bool isNewDatabase = !File.Exists(databasePath);

            // 使用SQLite连接到数据库
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();

                // 如果是新数据库，则创建表格并插入初始数据
                if (isNewDatabase)
                {
                    CreateSettingsTable(connection);
                    CreateButtonVisibilityTable(connection);
                    InsertInitialData(connection);
                }
                else
                {
                    // 先创建表格
                    CreateSettingsTable(connection);
                    CreateButtonVisibilityTable(connection);

                    // 然后检查并补充缺失的键
                    CheckSettingsKeys(connection);
                    CheckButtonVisibilityKeys(connection);
                }

                // 进行数据迁移
                MigrateDataFromConfig(connection);
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

        // 插入初始数据的方法
        private void InsertInitialData(SqliteConnection connection)
        {
            // 插入初始数据的SQL查询
            string insertInitialDataQuery = @"
            INSERT INTO Settings (Key, Value) VALUES 
            ('GamePath', ''),
            ('TextContent', ''),
            ('UpdatePath', 'https://gitee.com/nibadianbanxiaban/software/releases/download/resources/update.xml'),
            ('LaunchCount', '0'),
            ('EnableCounting', 'false'),
            ('RetryCount', '2'),
            ('RetryDelay', '5'),
            ('adcode', ''),
            ('UpdateLogUrl', 'https://gitee.com/nibadianbanxiaban/software/releases/download/resources/update_log.json'),
            ('VersionColor', 'Red'),
            ('UpdateTimeColor', 'Blue'),
            ('Culture', 'zh-CN'),
            ('EnableAutoUpdate', 'true'),
            ('NewUpdatePath', 'https://gitee.com/nibadianbanxiaban/software/releases/download/resources/new_update.json'),
            ('LastUpdateTime', '');";

            // 创建SQL命令并执行
            var insertCommand = new SqliteCommand(insertInitialDataQuery, connection);
            insertCommand.ExecuteNonQuery();

            // 插入ButtonVisibility表的初始数据
            string insertButtonVisibilityDataQuery = @"
            INSERT INTO ButtonVisibility (ButtonName, IsVisible) VALUES 
            ('Button_GenshinMap', 1),
            ('Button_SelectUP', 1),
            ('Button_PlayGames', 1),
            ('Button_GenshinRole', 1),
            ('Button_HonkaiImpact3', 1),
            ('Button_StarRail', 1),
            ('Button_MoveChest', 1),
            ('Button_Bing', 1),
            ('Button_StreePortal', 1);";

            var insertButtonVisibilityCommand = new SqliteCommand(insertButtonVisibilityDataQuery, connection);
            insertButtonVisibilityCommand.ExecuteNonQuery();
        }

        // 检查并补充缺失的键的方法
        private void CheckSettingsKeys(SqliteConnection connection)
        {
            // 定义所有需要的键和值
            var requiredKeys = new Dictionary<string, string>
            {
                { "GamePath", "" },
                { "TextContent", "" },
                { "UpdatePath", "https://gitee.com/nibadianbanxiaban/software/releases/download/resources/update.xml" },
                { "LaunchCount", "0" },
                { "EnableCounting", "false" },
                { "RetryCount", "2" },
                { "RetryDelay", "5" },
                { "adcode", "" },
                { "UpdateLogUrl", "https://gitee.com/nibadianbanxiaban/software/releases/download/resources/update_log.json" },
                { "VersionColor", "Red" },
                { "UpdateTimeColor", "Blue" },
                { "Culture", "zh-CN" },
                { "EnableAutoUpdate", "true" },
                { "NewUpdatePath", "https://gitee.com/nibadianbanxiaban/software/releases/download/resources/new_update.json" },
                { "LastUpdateTime", "" }
            };

            // 获取数据库中已有的键
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

            // 插入缺失的键
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
            // 定义所有需要的按钮及其可见性
            var requiredButtons = new Dictionary<string, int>
            {
                { "Button_GenshinMap", 1 },
                { "Button_SelectUP", 1 },
                { "Button_PlayGames", 1 },
                { "Button_GenshinRole", 1 },
                { "Button_HonkaiImpact3", 1 },
                { "Button_StarRail", 1 },
                { "Button_MoveChest", 1 },
                { "Button_Bing", 1 },
                { "Button_StreePortal", 1 }
            };

            // 获取数据库中已有的按钮
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

            // 插入缺失的按钮
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

        // 从配置文件迁移数据的方法
        private void MigrateDataFromConfig(SqliteConnection connection)
        {
            try
            {
                // 获取当前的迁移版本
                string getVersionQuery = "SELECT Value FROM Settings WHERE Key = 'MigrationVersion';";
                var getVersionCommand = new SqliteCommand(getVersionQuery, connection);
                int currentVersion = Convert.ToInt32(getVersionCommand.ExecuteScalar());

                // 定义最新的迁移版本
                int latestVersion = 1; // 设置数据库版本号

                // 逐个版本进行迁移
                for (int version = currentVersion + 1; version <= latestVersion; version++)
                {
                    switch (version)
                    {
                        case 2:
                            // 执行版本2的迁移
                            MigrateToVersion2(connection);
                            break;
                            // 添加更多版本的迁移
                    }

                    // 更新迁移版本
                    string updateVersionQuery = "UPDATE Settings SET Value = @version WHERE Key = 'MigrationVersion';";
                    var updateVersionCommand = new SqliteCommand(updateVersionQuery, connection);
                    updateVersionCommand.Parameters.AddWithValue("@version", version);
                    updateVersionCommand.ExecuteNonQuery();
                }

                // 执行你原本的迁移方法
                PerformOriginalMigration(connection);
            }
            catch (Exception ex)
            {
                // 记录错误日志或显示错误消息
                MessageBox.Show($"数据迁移失败: {ex.Message}");
            }
        }

        // 定义迁移方法
        private void MigrateToVersion2(SqliteConnection connection)
        {
            // 版本2的迁移逻辑
            // 例如：更新现有配置项
            //string updateQuery = "UPDATE Settings SET Value = 'UpdatedValue' WHERE Key = 'ExistingConfigKey';";
            //var updateCommand = new SqliteCommand(updateQuery, connection);
            //updateCommand.ExecuteNonQuery();
        }

        // 执行你原本的迁移方法
        private void PerformOriginalMigration(SqliteConnection connection)
        {
            try
            {
                // 检查是否已经进行过迁移  
                string checkMigrationQuery = "SELECT COUNT(*) FROM Settings WHERE Key = 'MigrationCompleted';";
                using (var checkCommand = new SqliteCommand(checkMigrationQuery, connection))
                {
                    int migrationCount = Convert.ToInt32(checkCommand.ExecuteScalar());

                    if (migrationCount == 0)
                    {
                        // 读取配置文件中的所有设置项（这里假设是 app.config 或其他配置文件）  
                        var appSettings = ConfigurationManager.AppSettings;

                        // 遍历所有设置项的键，并插入到 Settings 表中  
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

                        // 获取以“Software_Url_”开头的文件夹  
                        var settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Software");
                        var softwareUrlDirectories = Directory.GetDirectories(settingsDirectory)
                            .Where(dir => Path.GetFileName(dir).StartsWith("Software_Url_"));

                        // 假设我们只关心第一个这样的文件夹  
                        var firstSoftwareUrlDirectory = softwareUrlDirectories.FirstOrDefault();
                        if (firstSoftwareUrlDirectory != null)
                        {
                            // 获取该文件夹下的第一个子文件夹  
                            var firstSubDirectory = Directory.GetDirectories(firstSoftwareUrlDirectory)
                                .FirstOrDefault();
                            if (firstSubDirectory != null)
                            {
                                // 构建 user.config 文件的完整路径  
                                var userConfigPath = Path.Combine(firstSubDirectory, "user.config");

                                // 使用 XDocument 读取 XML 文件  
                                XDocument doc = XDocument.Load(userConfigPath);

                                // 正确地导航到 <Software.Properties.Settings> 元素  
                                var settingsElement = doc.Root.Element("userSettings").Element("Software.Properties.Settings");
                                if (settingsElement != null)
                                {
                                    foreach (var setting in settingsElement.Elements("setting"))
                                    {
                                        string rawKey = setting.Attribute("name").Value; // 获取原始的 name 属性值  
                                        string key = rawKey.EndsWith("_Display") ? rawKey.Substring(0, rawKey.Length - "_Display".Length) : rawKey; // 去除 "_Display" 后缀  

                                        string value = setting.Element("value").Value; // 获取 value 元素的值  
                                        if (key != "LastUpdateTime")
                                        {
                                            bool isVisible;
                                            if (bool.TryParse(value, out isVisible))
                                            {
                                                // 插入或更新数据库，使用修改后的 key  
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
                                                // 处理无法解析为布尔值的情况  
                                                MessageBox.Show($"警告: 无法解析键 '{key}' 的布尔值: {value}");
                                            }
                                        }
                                        else
                                        {
                                            // 直接插入或更新 "LastUpdateTime" 到 Settings 表，使用原始 key  
                                            string upsertKeyQuery = @"  
INSERT INTO Settings (Key, Value) VALUES (@key, @value)  
ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";

                                            using (var upsertCommand = new SqliteCommand(upsertKeyQuery, connection))
                                            {
                                                upsertCommand.Parameters.AddWithValue("@key", rawKey); // 注意这里使用原始 key  
                                                upsertCommand.Parameters.AddWithValue("@value", value);
                                                upsertCommand.ExecuteNonQuery();
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // 标记迁移已完成  
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
                // 记录错误日志或显示错误消息  
                MessageBox.Show($"数据迁移失败: {ex.Message}");
            }
        }

    }
}
