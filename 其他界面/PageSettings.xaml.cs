using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Software.ViewModels;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection.Emit;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
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
        //MainWindow mainWindow;
        private ViewModels.MusicPlayer musicPlayer;

        public PageSettings()
        {
            InitializeComponent();
            musicPlayer = new MusicPlayer(PageHome.Instance.mediaElement, PageHome.Instance.music_name, PageHome.Instance.playPauseButton);

        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // 读取配置文件
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            // 读取"EnableCounting"的值
            bool enableCounting = bool.Parse(ConfigurationManager.AppSettings["EnableCounting"]);
            bool enableAutoUpdate = bool.Parse(ConfigurationManager.AppSettings["EnableAutoUpdate"]);
            //读取值
            string updatePath = config.AppSettings.Settings["updatePath"].Value;
            string updateLogPath = config.AppSettings.Settings["UpdateLogUrl"].Value;
            string currentPath = config.AppSettings.Settings["GamePath"].Value;

            // 设置CheckBox的状态
            EnableCountingCheckBox.IsChecked = enableCounting;
            EnableAutoUpdateCheckBox.IsChecked = enableAutoUpdate;
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

            try
            {
                ToggleSwitch_Button_GenshinMap_Display.IsChecked = (bool)Properties.Settings.Default["Button_GenshinMap_Display"];
                ToggleSwitch_Button_SelectUP_Display.IsChecked = (bool)Properties.Settings.Default["Button_SelectUP_Display"];
                ToggleSwitch_Button_PlayGames_Display.IsChecked = (bool)Properties.Settings.Default["Button_PlayGames_Display"];
                ToggleSwitch_Button_GenshinRole_Display.IsChecked = (bool)Properties.Settings.Default["Button_GenshinRole_Display"];
                ToggleSwitch_Button_HonkaiImpact3_Display.IsChecked = (bool)Properties.Settings.Default["Button_HonkaiImpact3_Display"];
                ToggleSwitch_Button_StarRail_Display.IsChecked = (bool)Properties.Settings.Default["Button_StarRail_Display"];
                ToggleSwitch_Button_MoveChest_Display.IsChecked = (bool)Properties.Settings.Default["Button_MoveChest_Display"];
                ToggleSwitch_Button_Bing_Display.IsChecked = (bool)Properties.Settings.Default["Button_Bing_Display"];
            }
            catch (Exception ex)
            {
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
            // 读取"EnableCounting"的值
            string enableCountingValue = ConfigurationManager.AppSettings["EnableCounting"];
            bool enableCounting = enableCountingValue != null ? bool.Parse(enableCountingValue) : false;

            // 设置CheckBox的状态
            EnableCountingCheckBox.IsChecked = enableCounting;

            // 如果启用计数，则执行计数逻辑
            if (enableCounting)
            {
                // 读取并增加启动次数
                int launchCount = int.Parse(ConfigurationManager.AppSettings["LaunchCount"]) + 1;

                // 更新启动次数
                UpdateLaunchCount(launchCount);

                // 显示启动次数
                MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
                mainWindow.LaunchCount.Content = $"软件已启动 {launchCount} 次 ";
            }
        }

        private void UpdateLaunchCount(int launchCount)
        {
            // 打开配置文件
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            // 更新启动次数
            config.AppSettings.Settings["LaunchCount"].Value = launchCount.ToString();

            // 保存配置文件
            config.Save(ConfigurationSaveMode.Modified);

            // 刷新配置文件
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void UpdateEnableCounting(bool enableCounting)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["EnableCounting"].Value = enableCounting.ToString();
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void EnableCountingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            UpdateEnableCounting(true);
            mainWindow.LaunchCount.Visibility = Visibility.Visible;
        }

        private void EnableCountingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            UpdateEnableCounting(false);
            mainWindow.LaunchCount.Visibility = Visibility.Hidden;
        }

        private void UpdateAutoUpdateCounting(bool enableAutoUpdate)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["EnableAutoUpdate"].Value = enableAutoUpdate.ToString();
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void EnableAutoUpdateCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateAutoUpdateCounting(true);
        }

        private void EnableAutoUpdateCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateAutoUpdateCounting(false);
        }

        private void Update_IP_address_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = Update_IP_address.Text;
            // 保存到配置文件
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["UpdatePath"].Value = text;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void Update_Log_IP_address_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = Update_Log_IP_address.Text;
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["UpdateLogUrl"].Value = text;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void Text_GamePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = Text_GamePath.Text;
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["GamePath"].Value = text;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("aappSettings");
        }

        private void RadioButton_Click_English(object sender, RoutedEventArgs e)
        {
            SaveCultureInfo("en-US");
        }

        private void RadioButton_Click_Chinese(object sender, RoutedEventArgs e)
        {
            SaveCultureInfo("zh-CN");
        }

        private void SaveCultureInfo(string cultureName)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["Culture"].Value = cultureName;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
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
        
        //添加ToolTip
        string publishJson = "此功能迁移中 会横跨多个版本 使用需谨慎";
        //添加打开文件夹
        string rootDirectoryFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
        string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
        string resourcesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources");
        string musicFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources\\sound\\music");

        private void Button_Click_Open_Root_Directory_Folder(object sender, RoutedEventArgs e)
        {

            Process.Start("explorer.exe", rootDirectoryFolder);
        }

        private void Button_Click_Open_Log_Folder(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", logFolder);
        }

        private void Button_Click_Open_Resources_Folder(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", resourcesFolder);
        }

        private void Button_Click_Open_Music_Folder(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", musicFolder);
        }

        private void Button_Click_Delete_Folder(object sender, RoutedEventArgs e)
        {
            bool hasError = false;

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
                        MessageBox.Show($"删除文件夹{folderPath}失败：{ex.Message}", "删除文件夹错误");
                        hasError = true;
                    }
                }
                else
                {
                    MessageBox.Show($"根目录下没有名为“{Path.GetFileName(folderPath)}”的文件夹/你删过了", "删除文件夹提示");
                }
            }
            if (!hasError)
            {
                MessageBox.Show("所有文件夹删除成功", "删除文件夹提示");
            }
        }

        private async void Button_Click_BuildJson(object sender, RoutedEventArgs e)
        {
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

                //获取文本
                string contentTextBox = ConfigurationManager.AppSettings["TextContent"];

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
                    Console.WriteLine("Error: Some required data is missing from the JSON file.");
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

                Debug.WriteLine(json);

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
                MessageBox.Show($"发生错误: {ex.Message}");
            }
        }

        private void Button_Click_Reboot_Software(object sender, RoutedEventArgs e)
        {
            RebootSoftware();
        }

        private void Button_Click_Root_Reboot_Software(object sender, RoutedEventArgs e)
        {
            RootRebootSoftware();
        }

        private async void Button_Click_City_Address(object sender, RoutedEventArgs e)
        {
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
                    MessageBox.Show("未找到对应的adcode，请检查地址是否正确。", "错误", MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
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
                        break;
                    case "ToggleSwitch_Button_SelectUP_Display":
                        mainWindow.Button_SelectUP.Visibility = Visibility.Visible;
                        break;
                    case "ToggleSwitch_Button_PlayGames_Display":
                        mainWindow.Button_PlayGames.Visibility = Visibility.Visible;
                        break;
                    case "ToggleSwitch_Button_GenshinRole_Display":
                        mainWindow.Button_GenshinRole.Visibility = Visibility.Visible;
                        break;
                    case "ToggleSwitch_Button_HonkaiImpact3_Display":
                        mainWindow.Button_HonkaiImpact3.Visibility = Visibility.Visible;
                        break;
                    case "ToggleSwitch_Button_StarRail_Display":
                        mainWindow.Button_StarRail.Visibility = Visibility.Visible;
                        break;
                    case "ToggleSwitch_Button_MoveChest_Display":
                        mainWindow.Button_MoveChest.Visibility = Visibility.Visible;
                        break;
                    case "ToggleSwitch_Button_Bing_Display":
                        mainWindow.Button_Bing.Visibility = Visibility.Visible;
                        break;
                    default:
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
                        break;
                    case "ToggleSwitch_Button_SelectUP_Display":
                        mainWindow.Button_SelectUP.Visibility = Visibility.Collapsed;
                        break;
                    case "ToggleSwitch_Button_PlayGames_Display":
                        mainWindow.Button_PlayGames.Visibility = Visibility.Collapsed;
                        break;
                    case "ToggleSwitch_Button_GenshinRole_Display":
                        mainWindow.Button_GenshinRole.Visibility = Visibility.Collapsed;
                        break;
                    case "ToggleSwitch_Button_HonkaiImpact3_Display":
                        mainWindow.Button_HonkaiImpact3.Visibility = Visibility.Collapsed;
                        break;
                    case "ToggleSwitch_Button_StarRail_Display":
                        mainWindow.Button_StarRail.Visibility = Visibility.Collapsed;
                        break;
                    case "ToggleSwitch_Button_MoveChest_Display":
                        mainWindow.Button_MoveChest.Visibility = Visibility.Collapsed;
                        break;
                    case "ToggleSwitch_Button_Bing_Display":
                        mainWindow.Button_Bing.Visibility = Visibility.Collapsed;
                        break;
                    default:
                        break;
                }
            }
            SaveSettings(); // 在每次切换时保存设置
        }

        public void SaveSettings()
        {
            try
            {
                Properties.Settings.Default["Button_GenshinMap_Display"] = ToggleSwitch_Button_GenshinMap_Display.IsChecked;
                Properties.Settings.Default["Button_SelectUP_Display"] = ToggleSwitch_Button_SelectUP_Display.IsChecked;
                Properties.Settings.Default["Button_PlayGames_Display"] = ToggleSwitch_Button_PlayGames_Display.IsChecked;
                Properties.Settings.Default["Button_GenshinRole_Display"] = ToggleSwitch_Button_GenshinRole_Display.IsChecked;
                Properties.Settings.Default["Button_HonkaiImpact3_Display"] = ToggleSwitch_Button_HonkaiImpact3_Display.IsChecked;
                Properties.Settings.Default["Button_StarRail_Display"] = ToggleSwitch_Button_StarRail_Display.IsChecked;
                Properties.Settings.Default["Button_MoveChest_Display"] = ToggleSwitch_Button_MoveChest_Display.IsChecked;
                Properties.Settings.Default["Button_Bing_Display"] = ToggleSwitch_Button_Bing_Display.IsChecked;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存设置时发生错误: " + ex.Message);
            }
        }
    }
}
