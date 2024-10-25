using AutoUpdaterDotNET;
using Microsoft.Win32;
using Newtonsoft.Json;
using Serilog;
using System.Diagnostics;
using Software.ViewModels;
using Software.其他界面;
using System;
using System.Configuration;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Xml.Linq;
using Software.Models;
using Microsoft.Data.Sqlite;
using Windows.Devices.Geolocation;

namespace Software
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        Frame frameHome = new Frame() { Content = new 其他界面.PageHome() };
        Frame frameMap = new Frame() { Content = new 其他界面.PageGenshinMap() };
        Frame frameVersion = new Frame() { Content = new 其他界面.PageVersion() };
        Frame frameBing = new Frame() { Content = new 其他界面.PageBing() };
        Frame frameMoveChest = new Frame() { Content = new 其他界面.PageMoveChest() };
        Frame frameSettings = new Frame() { Content = new 其他界面.PageSettings() };
        Frame frameImage = new Frame() { Content = new 其他界面.PageImage() };
        Frame frameLog = new Frame() { Content = new 其他界面.PageLog() };
        Frame frameStreePortal = new Frame() { Content = new 其他界面.PageStreePortal() };

        private 其他界面.PageSettings pageSettings;

        string databasePath = DatabaseHelper.GetDatabasePath();

        private ILogger logger;

        public ILogger MyLoger
        {
            get
            {
                if (logger == null)
                {
                    logger = Log.ForContext<MainWindow>();
                }
                return logger;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            ApplySavedCultureInfo();


            this.Loaded += (s, e) =>
            {
                string titleSuffix = "";
                //检查是否为管理员启动
                if (IsRunningAsAdministrator())
                {
                    titleSuffix += " (Administrator)";
                }
                //检查是否为debug版
                if (IsDebugMode())
                {
                    titleSuffix += " Debug";
                }
                this.Title = "MainWindow" + titleSuffix;
                MyLoger.Information("MainWindow加载完成");
            };
            
            pageSettings = new 其他界面.PageSettings(); // 创建PageSettings的实例
            LoadSettings(); // 在窗口加载时读取设置

            contentcon.Content = frameHome;
        }

        private void LoadSettings()
        {
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

                            SetButtonVisibility(buttonName, isVisible);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("读取设置时发生错误:{error}", ex.ToString());
                MessageBox.Show("读取设置时发生错误: " + ex.Message);
            }
        }

        private void SetButtonVisibility(string buttonName, bool isVisible)
        {
            var button = FindName(buttonName) as Button;
            if (button != null)
            {
                button.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public bool IsRunningAsAdministrator()
        {
            var wi = WindowsIdentity.GetCurrent();
            var wp = new WindowsPrincipal(wi);

            return wp.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private bool IsDebugMode()
        {
            #if DEBUG
                return true;
            #else
                return false;
            #endif
        }

        private void ApplySavedCultureInfo()
        {
            string cultureName = ConfigurationManager.AppSettings["Culture"];
            if (!string.IsNullOrEmpty(cultureName))
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureName);
            }
        }

        private void MoveUpdaterFilesIfNeeded()
        {
            try
            {
                // 获取用户文档文件夹路径
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                // 构建目标目录路径
                string targetDir = Path.Combine(documentsPath, "StreeDB", "update");

                // 确保目标目录存在
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // 定义需要移动的文件列表
                string[] filesToMove = new string[]
                {
                "Updater.exe"
                };

                foreach (string fileName in filesToMove)
                {
                    string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                    string targetPath = Path.Combine(targetDir, fileName);

                    // 复制文件到目标路径并覆盖
                    File.Copy(sourcePath, targetPath, true);
                    MyLoger.Information("{fileName} 已复制到: {targetPath}", fileName, targetPath);

                    // 删除源文件
                    File.Delete(sourcePath);
                }
            }
            catch (Exception ex)
            {
                MyLoger.Warning(ex.ToString());
            }            
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                MoveUpdaterFilesIfNeeded();

                // 从数据库读取配置
                string databasePath = DatabaseHelper.GetDatabasePath();

                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();

                    // 读取 "EnableAutoUpdate" 配置项的值
                    string enableAutoUpdateQuery = "SELECT Value FROM Settings WHERE Key = 'EnableAutoUpdate';";
                    var enableAutoUpdateCommand = new SqliteCommand(enableAutoUpdateQuery, connection);
                    string enableAutoUpdateValue = enableAutoUpdateCommand.ExecuteScalar()?.ToString();
                    bool dd_enableAutoUpdate = enableAutoUpdateValue != null ? bool.Parse(enableAutoUpdateValue) : true;

                    // 读取 "updatePath" 配置项的值
                    string updatePathQuery = "SELECT Value FROM Settings WHERE Key = 'UpdatePath';";
                    var updatePathCommand = new SqliteCommand(updatePathQuery, connection);
                    string dd_updatePath = updatePathCommand.ExecuteScalar()?.ToString();

                    if (dd_enableAutoUpdate)
                    {
                        string updatePath = dd_updatePath;

                        // 获取更新服务器的IP地址
                        var UpdateIP = updatePath;
                        // 获取当前正在执行的程序集
                        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                        // 获取版本信息
                        System.Version softwareversion = assembly.GetName().Version;

                        // 创建一个新的HttpClient实例
                        var httpClient = new HttpClient();
                        // 从更新服务器获取XML字符串
                        var xmlString = await httpClient.GetStringAsync($"{UpdateIP}");

                        // 解析XML字符串
                        var xdoc = XDocument.Parse(xmlString);
                        // 获取XML中的"version"元素
                        var versionElement = xdoc.Descendants("version").FirstOrDefault();
                        if (versionElement != null)
                        {
                            // 解析"version"元素的值为Version对象
                            var version = Version.Parse(versionElement.Value);

                            // 如果服务器的版本高于当前版本，则启动自动更新
                            if (version > Assembly.GetExecutingAssembly().GetName().Version)
                            {
                                其他窗口.WindowUpdate nextwindow = new();
                                nextwindow.Show();
                            }
                        }
                    }
                }

                其他界面.PageSettings pageSettings = new 其他界面.PageSettings();
                pageSettings.HandleLaunchCount();
            }
            catch (Exception ex)
            {
                // 将异常信息写入日志
                File.WriteAllText("error.log", ex.ToString());
                MyLoger.Warning("应用程序在启动时遇到了一个错误。请查看 error.log 文件以获取更多信息。");
                // 显示一个错误消息
                MessageBox.Show("应用程序在启动时遇到了一个错误。请查看 error.log 文件以获取更多信息。");
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetSystemTime(ref SYSTEMTIME lpSystemTime);

        private void Button_Click_Home(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            contentcon.Content = frameHome;
            this.beta_tabel.Visibility = Visibility.Visible;
        }

        private void Button_Click_GenshinMap(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            contentcon.Content = frameMap;
            beta_tabel.Visibility = Visibility.Hidden;
        }

        private void Button_Click_SelectUP(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            其他窗口.WindowInquirySystem nextwindow = new();
            nextwindow.Show();
        }

        private void Button_Click_PlayGames(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            try
            {
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();

                    // 读取游戏路径
                    string gamePathQuery = "SELECT Value FROM Settings WHERE Key = 'GamePath';";
                    var gamePathCommand = new SqliteCommand(gamePathQuery, connection);
                    string gamePath = gamePathCommand.ExecuteScalar()?.ToString();

                    if (string.IsNullOrEmpty(gamePath) || !System.IO.File.Exists(gamePath))
                    {
                        // 如果游戏路径没有被设置或者文件不存在，那么打开一个对话框让用户选择一个路径
                        var dialog = new OpenFileDialog();
                        dialog.ValidateNames = false;
                        dialog.CheckFileExists = true;
                        dialog.CheckPathExists = true;
                        dialog.FileName = "Select Game";
                        if (dialog.ShowDialog() == true)
                        {
                            gamePath = dialog.FileName;
                            // 保存新的游戏路径
                            string updateGamePathQuery = "UPDATE Settings SET Value = @value WHERE Key = 'GamePath';";
                            var updateCommand = new SqliteCommand(updateGamePathQuery, connection);
                            updateCommand.Parameters.AddWithValue("@value", gamePath);
                            updateCommand.ExecuteNonQuery();
                        }
                    }

                    if (!string.IsNullOrEmpty(gamePath) && System.IO.File.Exists(gamePath))
                    {
                        _ = System.Diagnostics.Process.Start(gamePath);
                    }
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error(ex, "An error occurred while performing an operation.");
                MessageBox.Show(ex.Message);
            }
        }

        private void Button_Click_GenshinRole(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            其他窗口.WindowGenshinRole nextwindow = new();
            nextwindow.Show();
        }

        private void Button_Click_HonkaiImpact3(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            其他窗口.WindowHonkaiImpact3 nextwindow = new();
            nextwindow.Show();
        }

        private void Button_Click_StarRail(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            其他窗口.WindowStarRail nextwindow = new();
            nextwindow.Show();
        }

        private void Button_Click_MoveChest(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            其他窗口.WindowMoveChest nextwindow = new();
            nextwindow.Show();
            //PageMoveChest page = new PageMoveChest(); // 创建PageMoveChest实例

            //contentcon.Content = page; // 将PageMoveChest实例设置为contentcon的内容
        }

        private void Button_Click_Bing(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            contentcon.Content = frameBing;
            beta_tabel.Visibility = Visibility.Hidden;
            //其他窗口.WindowBing nextwindow = new();
            //nextwindow.Show();
        }

        private void Button_Click_Updata(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);

            其他窗口.WindowUpdate nextwindow = new();
            nextwindow.Show();
            
            //// 初始化一个空的字符串变量
            //string versionString = string.Empty;
            //try
            //{
            //    Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            //    string updatePath = config.AppSettings.Settings["updatePath"].Value;

            //    // 获取更新服务器的IP地址
            //    var UpdateIP = updatePath;
            //    // 获取当前正在执行的程序集
            //    System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            //    // 获取版本信息
            //    System.Version softwareversion = assembly.GetName().Version;
            //    // 将版本信息转换为字符串
            //    versionString = softwareversion.ToString();

            //    // 创建一个新的HttpClient实例
            //    var httpClient = new HttpClient();
            //    // 从更新服务器获取XML字符串
            //    var xmlString = await httpClient.GetStringAsync($"{UpdateIP}");

            //    // 解析XML字符串
            //    var xdoc = XDocument.Parse(xmlString);
            //    // 获取XML中的"version"元素
            //    var versionElement = xdoc.Descendants("version").FirstOrDefault();
            //    if (versionElement != null)
            //    {
            //        // 解析"version"元素的值为Version对象
            //        var version = Version.Parse(versionElement.Value);

            //        // 如果服务器的版本高于当前版本，则启动自动更新
            //        if (version > Assembly.GetExecutingAssembly().GetName().Version)
            //        {
            //            AutoUpdater.Start($"{UpdateIP}");
            //        }
            //        else
            //        {
            //            // 如果当前版本已经是最新的，则显示消息框
            //            MessageBox.Show($"当前{versionString}已是最新版本");
            //        }
            //    }
            //}
            //catch (Exception ex)
            //{
            //    // 在这里处理异常，例如显示错误消息
            //    MyLoger.Error(ex, "An error occurred while performing an operation.");
            //    MessageBox.Show($"更新检查失败：{ex.Message}");
            //}

        }

        private void Button_Click_Version(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            contentcon.Content = frameVersion;
            beta_tabel.Visibility = Visibility.Hidden;
        }

        private void Button_Click_Settings(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            contentcon.Content = frameSettings;
            beta_tabel.Visibility = Visibility.Hidden;
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            NavigationWindow window = new()
            {
                Source = new Uri("其他界面/PageDetails.xaml", UriKind.Relative)
            };
            window.Show();
        }

        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            其他窗口.WindowVideoPlayer nextwindow = new();
            nextwindow.Show();
        }

        private void Button_Click_PlayVideo(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            其他窗口.WindowVideoPlayer nextwindow = new();
            nextwindow.Show();
        }

        private void Button_Click_Image(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            contentcon.Content = frameImage;
            this.beta_tabel.Visibility = Visibility.Hidden;
        }

        private void Button_Click_StreePortal(object sender, RoutedEventArgs e)
        {
            MyLoger.Information("Button clicked: {ButtonName}", ((Button)sender).Name);
            contentcon.Content = frameStreePortal;
            this.beta_tabel.Visibility = Visibility.Hidden;
        }
    }
}
