using AutoUpdaterDotNET;
using Microsoft.Win32;
using Newtonsoft.Json;
using Software.其他界面;
using System;
using System.Configuration;
using System.Diagnostics;
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

        private 其他界面.PageSettings pageSettings;

        public MainWindow()
        {
            InitializeComponent();
            ApplySavedCultureInfo();

            // 在窗口加载完成后检查是否以管理员权限运行
            this.Loaded += (s, e) =>
            {
                if (IsRunningAsAdministrator())
                {
                    this.Title = "MainWindow (Administrator)";
                }
                else
                {
                    this.Title = "MainWindow";
                }
                Debug.WriteLine("加载完成");
            };
            
            pageSettings = new 其他界面.PageSettings(); // 创建PageSettings的实例
            LoadSettings(); // 在窗口加载时读取设置

            contentcon.Content = frameHome;
        }

        private void LoadSettings()
        {
            try
            {
                SetButtonVisibility(Button_GenshinMap, "Button_GenshinMap_Display");
                SetButtonVisibility(Button_SelectUP, "Button_SelectUP_Display");
                SetButtonVisibility(Button_PlayGames, "Button_PlayGames_Display");
                SetButtonVisibility(Button_GenshinRole, "Button_GenshinRole_Display");
                SetButtonVisibility(Button_HonkaiImpact3, "Button_HonkaiImpact3_Display");
                SetButtonVisibility(Button_StarRail, "Button_StarRail_Display");
                SetButtonVisibility(Button_MoveChest, "Button_MoveChest_Display");
                SetButtonVisibility(Button_Bing, "Button_Bing_Display");
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取设置时发生错误: " + ex.Message);
            }
        }

        private void SetButtonVisibility(Button button, string settingName)
        {
            button.Visibility = (bool)Properties.Settings.Default[settingName] ? Visibility.Visible : Visibility.Collapsed;
        }

        public bool IsRunningAsAdministrator()
        {
            var wi = WindowsIdentity.GetCurrent();
            var wp = new WindowsPrincipal(wi);

            return wp.IsInRole(WindowsBuiltInRole.Administrator);
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 读取配置文件
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                // 检查 "EnableAutoUpdate" 配置项的值是否为 null
                string enableAutoUpdateValue = ConfigurationManager.AppSettings["EnableAutoUpdate"];
                bool dd_enableAutoUpdate = enableAutoUpdateValue != null ? bool.Parse(enableAutoUpdateValue) : true;

                string dd_updatePath = config.AppSettings.Settings["updatePath"].Value;
                if (dd_enableAutoUpdate)
                {
                    AutoUpdater.Start($"{dd_updatePath}");
                }

                其他界面.PageSettings pageSettings = new 其他界面.PageSettings();
                pageSettings.HandleLaunchCount();
            }
            catch (Exception ex)
            {
                // 将异常信息写入日志
                File.WriteAllText("error.log", ex.ToString());

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
            contentcon.Content = frameHome;
            this.beta_tabel.Visibility = Visibility.Visible;
        }

        private void Button_Click_GenshinMap(object sender, RoutedEventArgs e)
        {
            contentcon.Content = frameMap;
            //其他窗口.WindowGenshinMap nextwindow = new();
            //nextwindow.Show();
        }

        private void Button_Click_SelectUP(object sender, RoutedEventArgs e)
        {
            其他窗口.WindowInquirySystem nextwindow = new();
            nextwindow.Show();
        }

        private void Button_Click_PlayGames(object sender, RoutedEventArgs e)
        {
            try
            {
                string gamePath = ConfigurationManager.AppSettings["GamePath"];
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
                        UpdateGamePath(gamePath);  // 保存新的游戏路径
                    }
                }
                if (!string.IsNullOrEmpty(gamePath) && System.IO.File.Exists(gamePath))
                {
                    _ = System.Diagnostics.Process.Start(gamePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void UpdateGamePath(string newPath)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["GamePath"].Value = newPath;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void Button_Click_GenshinRole(object sender, RoutedEventArgs e)
        {
            其他窗口.WindowGenshinRole nextwindow = new();
            nextwindow.Show();
        }

        private void Button_Click_HonkaiImpact3(object sender, RoutedEventArgs e)
        {
            其他窗口.WindowHonkaiImpact3 nextwindow = new();
            nextwindow.Show();
        }

        private void Button_Click_StarRail(object sender, RoutedEventArgs e)
        {
            其他窗口.WindowStarRail nextwindow = new();
            nextwindow.Show();
        }

        private void Button_Click_MoveChest(object sender, RoutedEventArgs e)
        {
            其他窗口.WindowMoveChest nextwindow = new();
            nextwindow.Show();
            //PageMoveChest page = new PageMoveChest(); // 创建PageMoveChest实例

            //contentcon.Content = page; // 将PageMoveChest实例设置为contentcon的内容
        }

        private void Button_Click_Bing(object sender, RoutedEventArgs e)
        {
            contentcon.Content = frameBing;
            //其他窗口.WindowBing nextwindow = new();
            //nextwindow.Show();
        }

        private async void Button_Click_Updata(object sender, RoutedEventArgs e)
        {
            // 初始化一个空的字符串变量
            string versionString = string.Empty;

            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                string updatePath = config.AppSettings.Settings["updatePath"].Value;

                // 获取更新服务器的IP地址
                var UpdateIP = updatePath;
                // 获取当前正在执行的程序集
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                // 获取版本信息
                System.Version softwareversion = assembly.GetName().Version;
                // 将版本信息转换为字符串
                versionString = softwareversion.ToString();

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
                        AutoUpdater.Start($"{UpdateIP}");
                    }
                    else
                    {
                        // 如果当前版本已经是最新的，则显示消息框
                        MessageBox.Show($"当前{versionString}已是最新版本");
                    }
                }
            }
            catch (Exception ex)
            {
                // 在这里处理异常，例如显示错误消息
                MessageBox.Show($"更新检查失败：{ex.Message}");
            }

        }

        private void Button_Click_Version(object sender, RoutedEventArgs e)
        {
            contentcon.Content = frameVersion;
            beta_tabel.Visibility = Visibility.Hidden;
        }

        private void Button_Click_Settings(object sender, RoutedEventArgs e)
        {
            contentcon.Content = frameSettings;
            beta_tabel.Visibility = Visibility.Hidden;
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            NavigationWindow window = new()
            {
                Source = new Uri("其他界面/PageDetails.xaml", UriKind.Relative)
            };
            window.Show();
        }

        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            其他窗口.WindowVideoPlayer nextwindow = new();
            nextwindow.Show();
        }



        // 获取Music文件夹中的所有音乐文件路径
        //string[] musicFiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "resources\\sound\\music");

        private void MediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            // 随机选择一个音乐文件路径
            //Random random = new Random();
            //string selectedMusicFile = musicFiles[random.Next(musicFiles.Length)];

            // 设置MediaElement控件的Source属性为所选音乐文件路径
            //mediaElement.Source = new Uri(selectedMusicFile);
            //mediaElement.Play();
        }

        //private void Button_Click_BuildJson(object sender, RoutedEventArgs e)
        //{
        //    MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
        //    PageHome pageHome = (PageHome)mainWindow.FindName("PageHome");

        //    string appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;//获取软件名称
        //    string appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();//获取软件版本号
        //    string outputDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");//获取输出时间
        //    string osVersion = Environment.OSVersion.Version.ToString();//获取操作系统
        //    string computerName = Environment.MachineName;//获取系统用户名
        //    string contentText = pageHome.ContentTextBox.Text;//获取白框框里的内容

        //    var musicName = pageHome.music_name.Text;
        //    if (pageHome.mediaElement.Source != null)
        //    {
        //        string musicFilePath = pageHome.mediaElement.Source.LocalPath;
        //        if (!string.IsNullOrEmpty(musicFilePath))
        //        {
        //            musicName = System.IO.Path.GetFileName(musicFilePath);
        //        }
        //    }

        //    Uri playmusicpath = pageHome.mediaElement.Source;

        //    Uri defaultMusicPath = new Uri(AppDomain.CurrentDomain.BaseDirectory + "resources/sound/music/music_001.mp3");

        //    if (pageHome.mediaElement == null || pageHome.mediaElement.Source == null)
        //    {
        //        pageHome.mediaElement.Source = defaultMusicPath;
        //    }

        //    Uri musicPath = pageHome.mediaElement.Source;

        //    var language = Thread.CurrentThread.CurrentCulture;//获取当前语言

        //    if (string.IsNullOrEmpty(pageHome.ContentTextBox.Text))
        //    {
        //        pageHome.ContentTextBox.Text = "你什么也没输入";
        //    };

        //    var json = new
        //    {
        //        Name = appName,//软件名称
        //        ComputerName = computerName,//系统用户名
        //        Windows = osVersion,//系统版本号
        //        Language = language,//语言
        //        CityWeather = new
        //        {
        //            Province = pageHome.Data_Province.Text,
        //            City = pageHome.Data_City.Text,
        //            Adcode = pageHome.Data_Adcode.Text,
        //            Weather = pageHome.Data_Weather.Text,
        //            Temperature = pageHome.Data_Temperature.Text,
        //            Winddirection = pageHome.Data_Winddirection.Text,
        //            Windpower = pageHome.Data_Windpower.Text,
        //            Humidity = pageHome.Data_Humidity.Text
        //        },
        //        Detail = new
        //        {
        //            Time = outputDate,//时间
        //            Version = appVersion,//软件版本号
        //            MemoryUsage = $"{(Process.GetCurrentProcess().WorkingSet64 / 1024f) / 1024f}MB", //软件内存占用，单位是MB
        //            MusicName = musicName,
        //            MusicPath = musicPath.ToString(),
        //            Content = pageHome.ContentTextBox.Text//白框框里的内容
        //        }
        //    };

        //    //序列化为 JSON 字符串
        //    string jsonString = JsonConvert.SerializeObject(json, Formatting.Indented);

        //    //判断是否需要创建log文件夹
        //    string logFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
        //    if (!Directory.Exists(logFolderPath))
        //    {
        //        Directory.CreateDirectory(logFolderPath);
        //    }

        //    //构造文件名
        //    string date = DateTime.Now.ToString("yyyyMMdd");
        //    string folderPath = "./log/";
        //    string fileName = $"SoftwareMessage_v{appVersion}_{date}.json";

        //    //如果文件已存在，则在后面加上数字
        //    int count = 1;
        //    while (File.Exists(Path.Combine(folderPath, fileName)))
        //    {
        //        fileName = $"SoftwareMessage_v{appVersion}_{date}_{count}.json";
        //        count++;
        //    }

        //    //写入到日志文件中
        //    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log", fileName);
        //    File.WriteAllText(path, jsonString);

        //    pageHome.ContentTextBox.Text = contentText;

        //    pageHome.mediaElement.Source = playmusicpath;
        //}

        private void Button_Click_PlayVideo(object sender, RoutedEventArgs e)
        {
            其他窗口.WindowVideoPlayer nextwindow = new();
            nextwindow.Show();
        }

        private void Button_Click_Image(object sender, RoutedEventArgs e)
        {
            contentcon.Content = frameImage;
            this.beta_tabel.Visibility = Visibility.Hidden;
        }
    }
}
