using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Software.Models;
using Software.ViewModels;
using System;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace Software.其他界面
{
    /// <summary>
    /// PageSettings.xaml 的交互逻辑
    /// </summary>
    public partial class PageSettings : Page
    {
        private bool enableCounting;
        private bool enableAutoUpdate;
        private string updatePath;
        private string updateLogPath;
        private string currentPath;

        private MainWindow mainWindow;

        private ViewModels.MusicPlayer musicPlayer;
        
        string databasePath = DatabaseHelper.GetDatabasePath();

        private StatusViewModel _viewModel;
        private DispatcherTimer _timer;
        private PerformanceCounter cpuCounter;

        private ILogger logger;

        public ILogger MyLoger
        {
            get
            {
                if (logger == null)
                {
                    logger = Log.ForContext<PageSettings>();
                }
                return logger;
            }
        }

        public PageSettings()
        {
            try
            {
                InitializeComponent();
                musicPlayer = new MusicPlayer(PageHome.Instance.mediaElement, PageHome.Instance.music_name, PageHome.Instance.playPauseButton);

                // 确保所有对象都已初始化
                _viewModel = new StatusViewModel();
                this.DataContext = _viewModel;

                // 初始化 PerformanceCounter
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

                // 定时更新状态
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(1);
                _timer.Tick += UpdateStatus;
                _timer.Start();

                MyLoger.Information("PageSettings初始化完成");
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void UpdateStatus(object sender, EventArgs e)
        {
            try
            {
                var process = Process.GetCurrentProcess();
                _viewModel.MemoryUsage = process.WorkingSet64 / (1024.0 * 1024.0); // 将内存使用转换为MB
                _viewModel.CPUUsage = GetCpuUsage();
                _viewModel.Uptime = DateTime.Now - process.StartTime;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发生了一个意外错误: {ex.Message}");
            }
        }

        private double GetCpuUsage()
        {
            // 获取 CPU 使用率
            return cpuCounter.NextValue() / Environment.ProcessorCount;
        }

        /// <summary>
        /// 从数据库中读取配置值
        /// </summary>
        /// <param name="dbPath">数据库路径</param>
        /// <param name="key">配置键</param>
        /// <returns>配置值</returns>
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
                MyLoger.Error("读取配置值时发生错误:{error}", ex.ToString());
                MessageBox.Show("读取配置值时发生错误: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 从数据库中读取布尔值
        /// </summary>
        /// <param name="dbPath">数据库路径</param>
        /// <param name="key">配置键</param>
        /// <returns>布尔值</returns>
        private bool GetBooleanConfigValueFromDatabase(string dbPath, string key)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    string query = "SELECT Value FROM Settings WHERE Key = @Key;";
                    var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@Key", key);
                    string result = command.ExecuteScalar()?.ToString();
                    return result == "1" || result.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("读取布尔配置值时发生错误:{error}", ex.ToString());
                MessageBox.Show("读取布尔配置值时发生错误: " + ex.Message);
                return false;
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // 从数据库读取布尔值
            enableCounting = GetBooleanConfigValueFromDatabase(databasePath, "EnableCounting");
            enableAutoUpdate = GetBooleanConfigValueFromDatabase(databasePath, "EnableAutoUpdate");

            // 设置CheckBox的状态
            EnableCountingCheckBox.IsChecked = enableCounting;
            EnableAutoUpdateCheckBox.IsChecked = enableAutoUpdate;

            // 从数据库读取其他配置值
            updatePath = GetConfigValueFromDatabase(databasePath, "UpdatePath");
            updateLogPath = GetConfigValueFromDatabase(databasePath, "UpdateLogUrl");
            currentPath = GetConfigValueFromDatabase(databasePath, "GamePath");

            // 设置TextBox的内容
            Update_IP_address.Text = updatePath;
            Update_Log_IP_address.Text = updateLogPath;
            Text_GamePath.Text = currentPath;

            //设置按钮的ToolTip
            BuildJson.ToolTip = publishJson;
            Open_Root_Directory_Folder.ToolTip = rootDirectoryFolder;
            Open_Log_Folder.ToolTip = logFolder;
            Open_Resources_Folder.ToolTip = resourcesFolder;
            Open_Music_Folder.ToolTip = musicFolder;

            LoadSettings();

        }

        private void LoadSettings()
        {
            // 禁用事件
            ToggleSwitch_Button_GenshinMap_Display.Checked -= ToggleSwitch_Checked;
            ToggleSwitch_Button_GenshinMap_Display.Unchecked -= ToggleSwitch_Unchecked;
            ToggleSwitch_Button_SelectUP_Display.Checked -= ToggleSwitch_Checked;
            ToggleSwitch_Button_SelectUP_Display.Unchecked -= ToggleSwitch_Unchecked;
            ToggleSwitch_Button_PlayGames_Display.Checked -= ToggleSwitch_Checked;
            ToggleSwitch_Button_PlayGames_Display.Unchecked -= ToggleSwitch_Unchecked;
            ToggleSwitch_Button_GenshinRole_Display.Checked -= ToggleSwitch_Checked;
            ToggleSwitch_Button_GenshinRole_Display.Unchecked -= ToggleSwitch_Unchecked;
            ToggleSwitch_Button_HonkaiImpact3_Display.Checked -= ToggleSwitch_Checked;
            ToggleSwitch_Button_HonkaiImpact3_Display.Unchecked -= ToggleSwitch_Unchecked;
            ToggleSwitch_Button_StarRail_Display.Checked -= ToggleSwitch_Checked;
            ToggleSwitch_Button_StarRail_Display.Unchecked -= ToggleSwitch_Unchecked;
            ToggleSwitch_Button_MoveChest_Display.Checked -= ToggleSwitch_Checked;
            ToggleSwitch_Button_MoveChest_Display.Unchecked -= ToggleSwitch_Unchecked;
            ToggleSwitch_Button_Bing_Display.Checked -= ToggleSwitch_Checked;
            ToggleSwitch_Button_Bing_Display.Unchecked -= ToggleSwitch_Unchecked;
            ToggleSwitch_Button_StreePortal_Display.Checked -= ToggleSwitch_Checked;
            ToggleSwitch_Button_StreePortal_Display.Unchecked -= ToggleSwitch_Unchecked;

            try
            {
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();

                    string selectQuery = "SELECT ButtonName, IsVisible FROM ButtonVisibility;";
                    var selectCommand = new SqliteCommand(selectQuery, connection);

                    using (var reader = selectCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string buttonName = reader.GetString(0);
                            bool isVisible = reader.GetInt32(1) == 1;

                            switch (buttonName)
                            {
                                case "Button_GenshinMap":
                                    ToggleSwitch_Button_GenshinMap_Display.IsChecked = isVisible;
                                    break;
                                case "Button_SelectUP":
                                    ToggleSwitch_Button_SelectUP_Display.IsChecked = isVisible;
                                    break;
                                case "Button_PlayGames":
                                    ToggleSwitch_Button_PlayGames_Display.IsChecked = isVisible;
                                    break;
                                case "Button_GenshinRole":
                                    ToggleSwitch_Button_GenshinRole_Display.IsChecked = isVisible;
                                    break;
                                case "Button_HonkaiImpact3":
                                    ToggleSwitch_Button_HonkaiImpact3_Display.IsChecked = isVisible;
                                    break;
                                case "Button_StarRail":
                                    ToggleSwitch_Button_StarRail_Display.IsChecked = isVisible;
                                    break;
                                case "Button_MoveChest":
                                    ToggleSwitch_Button_MoveChest_Display.IsChecked = isVisible;
                                    break;
                                case "Button_Bing":
                                    ToggleSwitch_Button_Bing_Display.IsChecked = isVisible;
                                    break;
                                case "Button_StreePortal":
                                    ToggleSwitch_Button_StreePortal_Display.IsChecked = isVisible;
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MyLoger.Warning("读取设置时发生错误: {warning}", ex.ToString());
                MessageBox.Show("读取设置时发生错误: " + ex.Message);
            }

            // 启用事件
            ToggleSwitch_Button_GenshinMap_Display.Checked += ToggleSwitch_Checked;
            ToggleSwitch_Button_GenshinMap_Display.Unchecked += ToggleSwitch_Unchecked;
            ToggleSwitch_Button_SelectUP_Display.Checked += ToggleSwitch_Checked;
            ToggleSwitch_Button_SelectUP_Display.Unchecked += ToggleSwitch_Unchecked;
            ToggleSwitch_Button_PlayGames_Display.Checked += ToggleSwitch_Checked;
            ToggleSwitch_Button_PlayGames_Display.Unchecked += ToggleSwitch_Unchecked;
            ToggleSwitch_Button_GenshinRole_Display.Checked += ToggleSwitch_Checked;
            ToggleSwitch_Button_GenshinRole_Display.Unchecked += ToggleSwitch_Unchecked;
            ToggleSwitch_Button_HonkaiImpact3_Display.Checked += ToggleSwitch_Checked;
            ToggleSwitch_Button_HonkaiImpact3_Display.Unchecked += ToggleSwitch_Unchecked;
            ToggleSwitch_Button_StarRail_Display.Checked += ToggleSwitch_Checked;
            ToggleSwitch_Button_StarRail_Display.Unchecked += ToggleSwitch_Unchecked;
            ToggleSwitch_Button_MoveChest_Display.Checked += ToggleSwitch_Checked;
            ToggleSwitch_Button_MoveChest_Display.Unchecked += ToggleSwitch_Unchecked;
            ToggleSwitch_Button_Bing_Display.Checked += ToggleSwitch_Checked;
            ToggleSwitch_Button_Bing_Display.Unchecked += ToggleSwitch_Unchecked;
            ToggleSwitch_Button_StreePortal_Display.Checked += ToggleSwitch_Checked;
            ToggleSwitch_Button_StreePortal_Display.Unchecked += ToggleSwitch_Unchecked;
        }

        public void RebootSoftware()
        {
            // 获取当前应用程序的路径
            string appPath = Process.GetCurrentProcess().MainModule.FileName;

            // 创建一个新的进程来启动新的应用程序实例
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = appPath,
                UseShellExecute = true
            };

            // 启动新的进程
            Process.Start(psi);

            // 关闭当前的应用程序
            Application.Current.Shutdown();
        }

        public void RootRebootSoftware()
        {
            // 获取当前应用程序的路径
            string appPath = Process.GetCurrentProcess().MainModule.FileName;

            // 创建一个新的进程来启动新的应用程序实例
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = appPath,
                UseShellExecute = true,
                Verb = "runas"  // 运行新的进程时请求管理员权限
            };

            // 启动新的进程
            Process.Start(psi);

            // 关闭当前的应用程序
            Application.Current.Shutdown();
        }

        public void HandleLaunchCount()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();

                    // 读取 "EnableCounting" 的值
                    string enableCountingQuery = "SELECT Value FROM Settings WHERE Key = 'EnableCounting';";
                    var enableCountingCommand = new SqliteCommand(enableCountingQuery, connection);
                    string enableCountingValue = enableCountingCommand.ExecuteScalar()?.ToString();
                    bool enableCounting = enableCountingValue != null ? bool.Parse(enableCountingValue) : false;

                    // 设置 CheckBox 的状态
                    EnableCountingCheckBox.IsChecked = enableCounting;

                    // 如果启用计数，则执行计数逻辑
                    if (enableCounting)
                    {
                        // 读取并增加启动次数
                        string launchCountQuery = "SELECT Value FROM Settings WHERE Key = 'LaunchCount';";
                        var launchCountCommand = new SqliteCommand(launchCountQuery, connection);
                        int launchCount = int.Parse(launchCountCommand.ExecuteScalar()?.ToString() ?? "0") + 1;

                        // 更新启动次数
                        UpdateLaunchCount(launchCount);

                        // 显示启动次数
                        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
                        mainWindow.LaunchCount.Content = $"软件已启动 {launchCount} 次 ";
                    }
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("读取或更新启动次数时发生错误:{error}", ex.ToString());
                MessageBox.Show("读取或更新启动次数时发生错误: " + ex.Message);
            }
        }

        private void UpdateLaunchCount(int launchCount)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();

                    // 更新启动次数
                    string updateLaunchCountQuery = "UPDATE Settings SET Value = @value WHERE Key = 'LaunchCount';";
                    var updateCommand = new SqliteCommand(updateLaunchCountQuery, connection);
                    updateCommand.Parameters.AddWithValue("@value", launchCount.ToString());
                    updateCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("更新启动次数时发生错误:{error}", ex.ToString());
                MessageBox.Show("更新启动次数时发生错误: " + ex.Message);
            }
        }

        private void UpdateEnableCounting(bool enableCounting)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();
                    string updateEnableCountingQuery = "UPDATE Settings SET Value = @value WHERE Key = 'EnableCounting';";
                    var updateCommand = new SqliteCommand(updateEnableCountingQuery, connection);
                    updateCommand.Parameters.AddWithValue("@value", enableCounting ? "true" : "false");
                    updateCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("更新 EnableCounting 时发生错误:{error}", ex.ToString());
                MessageBox.Show("更新 EnableCounting 时发生错误: " + ex.Message);
            }
        }

        private void EnableCountingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("CheckBox clicked: {CheckBoxName}", ((CheckBox)sender).Name);
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            UpdateEnableCounting(true);
            mainWindow.LaunchCount.Visibility = Visibility.Visible;
        }

        private void EnableCountingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("CheckBox clicked: {CheckBoxName}", ((CheckBox)sender).Name);
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            UpdateEnableCounting(false);
            mainWindow.LaunchCount.Visibility = Visibility.Hidden;
        }

        private void UpdateAutoUpdateCounting(bool enableAutoUpdate)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();
                    string updateAutoUpdateQuery = "UPDATE Settings SET Value = @value WHERE Key = 'EnableAutoUpdate';";
                    var updateCommand = new SqliteCommand(updateAutoUpdateQuery, connection);
                    updateCommand.Parameters.AddWithValue("@value", enableAutoUpdate ? "true" : "false");
                    updateCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("更新 EnableAutoUpdate 时发生错误:{error}", ex.ToString());
                MessageBox.Show("更新 EnableAutoUpdate 时发生错误: " + ex.Message);
            }
        }

        private void EnableAutoUpdateCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("CheckBox clicked: {CheckBoxName}", ((CheckBox)sender).Name);
            UpdateAutoUpdateCounting(true);
        }

        private void EnableAutoUpdateCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("CheckBox clicked: {CheckBoxName}", ((CheckBox)sender).Name);
            UpdateAutoUpdateCounting(false);
        }

        private void Update_IP_address_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string text = Update_IP_address.Text;
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();

                    string updateQuery = "UPDATE Settings SET Value = @value WHERE Key = 'UpdatePath';";
                    var updateCommand = new SqliteCommand(updateQuery, connection);
                    updateCommand.Parameters.AddWithValue("@value", text);
                    updateCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("保存 UpdatePath 时发生错误:{error}", ex.ToString());
                MessageBox.Show("保存 UpdatePath 时发生错误: " + ex.Message);
            }
        }

        private void Update_Log_IP_address_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string text = Update_Log_IP_address.Text;
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();

                    string updateQuery = "UPDATE Settings SET Value = @value WHERE Key = 'UpdateLogUrl';";
                    var updateCommand = new SqliteCommand(updateQuery, connection);
                    updateCommand.Parameters.AddWithValue("@value", text);
                    updateCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("保存 UpdateLogUrl 时发生错误:{error}", ex.ToString());
                MessageBox.Show("保存 UpdateLogUrl 时发生错误: " + ex.Message);
            }
        }

        private void Text_GamePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string text = Text_GamePath.Text;
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();

                    string updateQuery = "UPDATE Settings SET Value = @value WHERE Key = 'GamePath';";
                    var updateCommand = new SqliteCommand(updateQuery, connection);
                    updateCommand.Parameters.AddWithValue("@value", text);
                    updateCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("保存 GamePath 时发生错误:{error}", ex.ToString());
                MessageBox.Show("保存 GamePath 时发生错误: " + ex.Message);
            }
        }

        private void RadioButton_Click_English(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            SaveCultureInfo("en-US");
        }

        private void RadioButton_Click_Chinese(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            SaveCultureInfo("zh-CN");
        }

        private void SaveCultureInfo(string cultureName)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();

                    string updateQuery = "UPDATE Settings SET Value = @value WHERE Key = 'Culture';";
                    var updateCommand = new SqliteCommand(updateQuery, connection);
                    updateCommand.Parameters.AddWithValue("@value", cultureName);
                    updateCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("保存 Culture 时发生错误:{error}", ex.ToString());
                MessageBox.Show("保存 Culture 时发生错误: " + ex.Message);
            }
        }

        //要删除的文件夹名
        string[] folderPaths = new string[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "win-x64"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ar"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cs"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "da"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "de"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "es"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fr"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "it"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ja-JP"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ko"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lv"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nl"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pl"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pt"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pt-BR"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ru"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sk"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sv"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "th"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tr"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zh-TW"),
        };
        //要删除的文件名
        string[] filePaths = new string[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater.deps.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater.dll"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater.pdb"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater.runtimeconfig.json"),
        };

        //添加ToolTip
        string publishJson = "此功能迁移完成";
        //添加打开文件夹
        string rootDirectoryFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
        string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
        string resourcesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources");
        string musicFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources\\sound\\music");

        private void Button_Click_Open_Root_Directory_Folder(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            Process.Start("explorer.exe", rootDirectoryFolder);
        }

        private void Button_Click_Open_Log_Folder(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            Process.Start("explorer.exe", logFolder);
        }

        private void Button_Click_Open_Resources_Folder(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            Process.Start("explorer.exe", resourcesFolder);
        }

        private void Button_Click_Open_Music_Folder(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            Process.Start("explorer.exe", musicFolder);
        }

        private void Button_Click_Delete_Folder(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            bool hasError = false;

            // 删除文件夹
            foreach (string folderPath in folderPaths)
            {
                if (Directory.Exists(folderPath))
                {
                    try
                    {
                        Directory.Delete(folderPath, true);
                    }
                    catch (Exception ex)
                    {
                        MyLoger.Error("删除文件夹 {folderPath} 时发生错误: {error}", folderPath, ex.ToString());
                        MessageBox.Show($"删除文件夹{folderPath}失败：{ex.Message}", "删除文件夹错误");
                        hasError = true;
                    }
                }
                else
                {
                    MyLoger.Error("根目录下没有名为“{folderPath}”的文件夹/你删过了", folderPath);
                    MessageBox.Show($"根目录下没有名为“{Path.GetFileName(folderPath)}”的文件夹/你删过了", "删除文件夹提示");
                }
            }

            // 删除文件
            foreach (string filePath in filePaths)
            {
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        MyLoger.Error("删除文件 {filePath} 时发生错误: {error}", filePath, ex.ToString());
                        MessageBox.Show($"删除文件{filePath}失败：{ex.Message}", "删除文件错误");
                        hasError = true;
                    }
                }
                else
                {
                    MyLoger.Error("根目录下没有名为“{filePath}”的文件/你删过了", filePath);
                    MessageBox.Show($"根目录下没有名为“{Path.GetFileName(filePath)}”的文件/你删过了", "删除文件提示");
                }
            }

            if (!hasError)
            {
                MyLoger.Information("所有文件夹和文件删除成功");
                MessageBox.Show("所有文件夹和文件删除成功", "删除提示");
            }
        }

        private async void Button_Click_BuildJson(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            try
            {
                // 获取应用程序的名称和版本
                string appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                string appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

                // 获取当前的日期和时间
                string outputDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // 获取操作系统的版本和计算机的名称
                string osVersion = Environment.OSVersion.Version.ToString();
                string computerName = Environment.MachineName;

                // 获取当前线程的文化信息
                var language = Thread.CurrentThread.CurrentCulture;

                // 确保musicPlayer已初始化
                if (musicPlayer == null)
                {
                    MyLoger.Warning("音乐播放器未初始化");
                    MessageBox.Show("音乐播放器未初始化");
                    return;
                }

                //获取音乐名称和音乐路径
                if (musicPlayer == null)
                {
                    MessageBox.Show("第一次用记得先点播放音乐");
                }
                string musicName = musicPlayer.GetCurrentMusicName();
                string musicPath = musicPlayer.GetCurrentMusicPath();
                Uri musicUri = new Uri(musicPath);
                string musicUrl = musicUri.AbsoluteUri;

                // 从数据库获取文本
                string contentTextBox;

                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();

                    string contentTextBoxQuery = "SELECT Value FROM Settings WHERE Key = 'TextContent';";
                    var contentTextBoxCommand = new SqliteCommand(contentTextBoxQuery, connection);
                    contentTextBox = contentTextBoxCommand.ExecuteScalar()?.ToString();
                }

                // 从文件中读取数据
                string result;
                using (StreamReader file = File.OpenText("resources\\weather.json"))
                {
                    result = await file.ReadToEndAsync();
                }

                // 解析数据
                var jsonObject = JObject.Parse(result);
                var lives = jsonObject["lives"].First;

                // 检查 lives，lives["province"] 和 lives["city"] 是否为 null
                if (lives == null || lives["province"] == null || lives["city"] == null /* add other checks as needed */)
                {
                    // 如果 lives，lives["province"] 或 lives["city"] 为 null，则显示错误消息并返回
                    MyLoger.Error("无法从JSON文件中获取所有需要的数据。请检查文件内容是否正确。");
                    MessageBox.Show("无法从JSON文件中获取所有需要的数据。请检查文件内容是否正确。");
                    return;
                }

                // 创建一个新的 JSON 对象
                var json = new
                {
                    Name = appName,
                    ComputerName = computerName,
                    Windows = osVersion,
                    Language = language,
                    CityWeather = new
                    {
                        Province = lives["province"].ToString(),
                        City = lives["city"].ToString(),
                        Adcode = lives["adcode"].ToString(),
                        Weather = lives["weather"].ToString(),
                        Temperature = lives["temperature"].ToString(),
                        Winddirection = lives["winddirection"].ToString(),
                        Windpower = lives["windpower"].ToString(),
                        Humidity = lives["humidity"].ToString()
                    },
                    Music = string.IsNullOrEmpty(musicName) || string.IsNullOrEmpty(musicPath) ? null : new
                    {
                        MusicName = musicName,
                        MusicPath = musicPath,
                        MusicUrl = musicUrl
                    },
                    Detail = new
                    {
                        Time = outputDate,
                        Version = appVersion,
                        MemoryUsage = $"{(Process.GetCurrentProcess().WorkingSet64 / 1024f) / 1024f}MB",
                        Content = contentTextBox
                    }
                };

                // 将 JSON 对象序列化为字符串
                string jsonString = JsonConvert.SerializeObject(json, Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                // 检查是否需要创建 log 文件夹
                string logFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
                if (!Directory.Exists(logFolderPath))
                {
                    Directory.CreateDirectory(logFolderPath);
                }

                // 构造文件名
                string date = DateTime.Now.ToString("yyyyMMdd");
                string folderPath = "./log/";
                string fileName = $"SoftwareMessage_v{appVersion}_{date}.json";

                // 如果文件已存在，则在后面加上数字
                int count = 1;
                while (File.Exists(Path.Combine(folderPath, fileName)))
                {
                    fileName = $"SoftwareMessage_v{appVersion}_{date}_{count}.json";
                    count++;
                }

                // 将字符串写入到日志文件中
                string path = Path.Combine(folderPath, fileName);
                File.WriteAllText(path, jsonString);
            }
            catch (Exception ex)
            {
                MyLoger.Error("发生错误: {error}", ex.ToString());
                MessageBox.Show($"发生错误: {ex.Message}");
            }
        }

        private void Button_Click_Reboot_Software(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            RebootSoftware();
        }

        private void Button_Click_Root_Reboot_Software(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            RootRebootSoftware();
        }

        private async void Button_Click_City_Address(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            try
            {
                string address = Text_CityAddress.Text;

                var client = new HttpClient();
                var request = new HttpRequestMessage();
                request.RequestUri = new Uri($"https://restapi.amap.com/v3/geocode/geo?address={address}&output=JSON&key=71d6333d58f635ab3136a8955cec1e8c");
                request.Method = HttpMethod.Get;
                Debug.WriteLine($"{address}");
                var response = await client.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();

                JObject obj = JObject.Parse(json);
                Debug.WriteLine($"{json}");
                JArray geocodes = (JArray)obj["geocodes"];

                if (geocodes.Count > 0)
                {
                    JObject geocode = (JObject)geocodes[0];
                    string adcode = (string)geocode["adcode"];
                    Debug.WriteLine($"adcode: {adcode}");

                    Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    config.AppSettings.Settings["adcode"].Value = adcode;
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");

                    MessageBoxResult result = MessageBox.Show("修改成功，需重启后生效。\n是否重启？", "成功", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        RebootSoftware();
                    }
                }
                else
                {
                    MyLoger.Error("未找到对应的adcode，请检查地址是否正确。");
                    MessageBox.Show("未找到对应的adcode，请检查地址是否正确。", "错误", MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("发生错误:{error}", ex.ToString());
                MessageBox.Show($"发生错误：{ex.Message}", "错误", MessageBoxButton.OK);
            }
        }

        private void ToggleSwitch_Checked(object sender, RoutedEventArgs e)
        {
            var toggleSwitch = sender as ToggleSwitch;
            if (toggleSwitch != null)
            {
                string name = toggleSwitch.Name;
                MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
                switch (name)
                {
                    case "ToggleSwitch_Button_GenshinMap_Display":
                        mainWindow.Button_GenshinMap.Visibility = Visibility.Visible;
                        MyLoger.Information("ToggleSwitch {name} checked, setting Button_GenshinMap to Visible.", name);
                        break;
                    case "ToggleSwitch_Button_SelectUP_Display":
                        mainWindow.Button_SelectUP.Visibility = Visibility.Visible;
                        MyLoger.Information("ToggleSwitch {name} checked, setting Button_SelectUP to Visible.", name);
                        break;
                    case "ToggleSwitch_Button_PlayGames_Display":
                        mainWindow.Button_PlayGames.Visibility = Visibility.Visible;
                        MyLoger.Information("ToggleSwitch {name} checked, setting Button_PlayGames to Visible.", name);
                        break;
                    case "ToggleSwitch_Button_GenshinRole_Display":
                        mainWindow.Button_GenshinRole.Visibility = Visibility.Visible;
                        MyLoger.Information("ToggleSwitch {name} checked, setting Button_GenshinRole to Visible.", name);
                        break;
                    case "ToggleSwitch_Button_HonkaiImpact3_Display":
                        mainWindow.Button_HonkaiImpact3.Visibility = Visibility.Visible;
                        MyLoger.Information("ToggleSwitch {name} checked, setting Button_HonkaiImpact3 to Visible.", name);
                        break;
                    case "ToggleSwitch_Button_StarRail_Display":
                        mainWindow.Button_StarRail.Visibility = Visibility.Visible;
                        MyLoger.Information("ToggleSwitch {name} checked, setting Button_StarRail to Visible.", name);
                        break;
                    case "ToggleSwitch_Button_MoveChest_Display":
                        mainWindow.Button_MoveChest.Visibility = Visibility.Visible;
                        MyLoger.Information("ToggleSwitch {name} checked, setting Button_MoveChest to Visible.", name);
                        break;
                    case "ToggleSwitch_Button_Bing_Display":
                        mainWindow.Button_Bing.Visibility = Visibility.Visible;
                        MyLoger.Information("ToggleSwitch {name} checked, setting Button_Bing to Visible.", name);
                        break;
                    case "ToggleSwitch_Button_StreePortal_Display":
                        mainWindow.Button_StreePortal.Visibility = Visibility.Visible;
                        MyLoger.Information("ToggleSwitch {name} checked, setting Button_StreePortal to Visible.", name);
                        break;
                    default:
                        MyLoger.Warning("Unknown ToggleSwitch {name} checked.", name);
                        break;
                }
            }
            SaveSettings(); // 在每次切换时保存设置
        }

        private void ToggleSwitch_Unchecked(object sender, RoutedEventArgs e)
        {
            var toggleSwitch = sender as ToggleSwitch;
            if (toggleSwitch != null)
            {
                string name = toggleSwitch.Name;
                MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
                switch (name)
                {
                    case "ToggleSwitch_Button_GenshinMap_Display":
                        mainWindow.Button_GenshinMap.Visibility = Visibility.Collapsed;
                        MyLoger.Information("ToggleSwitch {name} unchecked, setting Button_GenshinMap to Collapsed.", name);
                        break;
                    case "ToggleSwitch_Button_SelectUP_Display":
                        mainWindow.Button_SelectUP.Visibility = Visibility.Collapsed;
                        MyLoger.Information("ToggleSwitch {name} unchecked, setting Button_SelectUP to Collapsed.", name);
                        break;
                    case "ToggleSwitch_Button_PlayGames_Display":
                        mainWindow.Button_PlayGames.Visibility = Visibility.Collapsed;
                        MyLoger.Information("ToggleSwitch {name} unchecked, setting Button_PlayGames to Collapsed.", name);
                        break;
                    case "ToggleSwitch_Button_GenshinRole_Display":
                        mainWindow.Button_GenshinRole.Visibility = Visibility.Collapsed;
                        MyLoger.Information("ToggleSwitch {name} unchecked, setting Button_GenshinRole to Collapsed.", name);
                        break;
                    case "ToggleSwitch_Button_HonkaiImpact3_Display":
                        mainWindow.Button_HonkaiImpact3.Visibility = Visibility.Collapsed;
                        MyLoger.Information("ToggleSwitch {name} unchecked, setting Button_HonkaiImpact3 to Collapsed.", name);
                        break;
                    case "ToggleSwitch_Button_StarRail_Display":
                        mainWindow.Button_StarRail.Visibility = Visibility.Collapsed;
                        MyLoger.Information("ToggleSwitch {name} unchecked, setting Button_StarRail to Collapsed.", name);
                        break;
                    case "ToggleSwitch_Button_MoveChest_Display":
                        mainWindow.Button_MoveChest.Visibility = Visibility.Collapsed;
                        MyLoger.Information("ToggleSwitch {name} unchecked, setting Button_MoveChest to Collapsed.", name);
                        break;
                    case "ToggleSwitch_Button_Bing_Display":
                        mainWindow.Button_Bing.Visibility = Visibility.Collapsed;
                        MyLoger.Information("ToggleSwitch {name} unchecked, setting Button_Bing to Collapsed.", name);
                        break;
                    case "ToggleSwitch_Button_StreePortal_Display":
                        mainWindow.Button_StreePortal.Visibility = Visibility.Collapsed;
                        MyLoger.Information("ToggleSwitch {name} unchecked, setting Button_StreePortal to Collapsed.", name);
                        break;
                    default:
                        MyLoger.Warning("Unknown ToggleSwitch {name} unchecked.", name);
                        break;
                }
            }
            SaveSettings(); // 在每次切换时保存设置
        }

        public void SaveSettings()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();

                    // 创建一个新的 SqliteCommand 对象，并设置其 CommandText
                    var updateCommand = new SqliteCommand
                    {
                        CommandText = @"
                            UPDATE ButtonVisibility SET IsVisible = @isVisible WHERE ButtonName = @buttonName;
                            INSERT INTO ButtonVisibility (ButtonName, IsVisible) 
                            SELECT @buttonName, @isVisible 
                            WHERE NOT EXISTS (SELECT 1 FROM ButtonVisibility WHERE ButtonName = @buttonName);",
                        Connection = connection
                    };

                    updateCommand.Parameters.Add(new SqliteParameter("@buttonName", SqliteType.Text));
                    updateCommand.Parameters.Add(new SqliteParameter("@isVisible", SqliteType.Integer));

                    void UpdateButtonVisibility(string buttonName, bool isVisible)
                    {
                        updateCommand.Parameters["@buttonName"].Value = buttonName;
                        updateCommand.Parameters["@isVisible"].Value = isVisible ? 1 : 0;
                        updateCommand.ExecuteNonQuery();
                    }

                    UpdateButtonVisibility("Button_GenshinMap", ToggleSwitch_Button_GenshinMap_Display.IsChecked == true);
                    UpdateButtonVisibility("Button_SelectUP", ToggleSwitch_Button_SelectUP_Display.IsChecked == true);
                    UpdateButtonVisibility("Button_PlayGames", ToggleSwitch_Button_PlayGames_Display.IsChecked == true);
                    UpdateButtonVisibility("Button_GenshinRole", ToggleSwitch_Button_GenshinRole_Display.IsChecked == true);
                    UpdateButtonVisibility("Button_HonkaiImpact3", ToggleSwitch_Button_HonkaiImpact3_Display.IsChecked == true);
                    UpdateButtonVisibility("Button_StarRail", ToggleSwitch_Button_StarRail_Display.IsChecked == true);
                    UpdateButtonVisibility("Button_MoveChest", ToggleSwitch_Button_MoveChest_Display.IsChecked == true);
                    UpdateButtonVisibility("Button_Bing", ToggleSwitch_Button_Bing_Display.IsChecked == true);
                    UpdateButtonVisibility("Button_StreePortal", ToggleSwitch_Button_StreePortal_Display.IsChecked == true);
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("保存设置时发生错误: {error}", ex.ToString());
                MessageBox.Show("保存设置时发生错误: " + ex.Message);
            }
        }

        private void Button_Click_Open_LogDashboard(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            OpenUrl("http://localhost:5000/logdashboard");
        }

        private void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }

        private async void TestNetworkConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            string targetAddress = TargetAddressTextBox.Text;
            if (string.IsNullOrEmpty(targetAddress))
            {
                NetworkTestResultTextBlock.Text = "请输入目标地址。";
                return;
            }

            try
            {
                string host;

                // 检查输入是否为URL
                if (Uri.IsWellFormedUriString(targetAddress, UriKind.Absolute))
                {
                    Uri uri = new Uri(targetAddress);
                    host = uri.Host;
                }
                else
                {
                    host = targetAddress;
                }

                Ping pingSender = new Ping();
                PingReply reply = await Task.Run(() => pingSender.Send(host, 5000)); // 设置超时时间为5000毫秒

                if (reply != null && reply.Status == IPStatus.Success)
                {
                    NetworkTestResultTextBlock.Text = $"网络连接成功。\n" +
                                                      $"地址: {reply.Address}\n" +
                                                      $"往返时间: {reply.RoundtripTime}ms\n" +
                                                      $"TTL: {reply.Options.Ttl}\n" +
                                                      $"缓冲区大小: {reply.Buffer.Length}";
                }
                else if (reply != null)
                {
                    NetworkTestResultTextBlock.Text = $"网络连接失败：{reply.Status}";
                }
                else
                {
                    NetworkTestResultTextBlock.Text = "网络连接失败：未收到回复。";
                }
            }
            catch (PingException ex)
            {
                NetworkTestResultTextBlock.Text = $"网络连接失败：{ex.Message}";
            }
            catch (Exception ex)
            {
                NetworkTestResultTextBlock.Text = $"网络连接失败：{ex.Message}";
            }
        }

    }
}
