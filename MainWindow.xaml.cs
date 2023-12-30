using AutoUpdaterDotNET;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Software.ViewModels;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Xml.Linq;

namespace Software
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        Frame frameVersion = new Frame() { Content = new 其他界面.PageVersion() };
        Frame frameBing = new Frame() { Content = new 其他界面.PageBing() };
        Frame frameMoveChest = new Frame() { Content = new 其他界面.PageMoveChest() };

        private Window dialog;
        private TextBox txtGamePath;
        private Weather weather;

        public MainWindow()
        {
            InitializeComponent();
            ApplySavedCultureInfo();

            // 检查配置文件是否存在
            if (!File.Exists("Software.dll.config"))
            {
                // 如果不存在，创建一个新的配置文件
                using (var stream = File.Create("Software.dll.config"))
                {
                    // 写入默认的设置
                    string defaultSettings = @"
<configuration>
    <appSettings>
        <add key=""GamePath"" value=""""/>
        <add key=""TextContent"" value=""""/>
        <add key=""UpdatePath"" value=""http://1.14.58.59/updata.xml""/>
        <add key=""LaunchCount"" value=""0""/>
        <add key=""EnableCounting"" value=""false""/>
        <add key=""RetryCount"" value=""5""/>
        <add key=""RetryDelay"" value=""10""/>
        <add key=""adcode"" value=""""/>
        <add key=""UpdateLogUrl"" value=""https://gitee.com/nibadianbanxiaban/software/releases/download/resources/update_log.json"" />
        <add key=""VersionColor"" value=""Red""/>
		<add key=""UpdateTimeColor"" value=""Blue""/>
    </appSettings>
</configuration>";
                    byte[] data = Encoding.UTF8.GetBytes(defaultSettings);
                    stream.Write(data, 0, data.Length);
                }
            }
            else
            {
                // 如果存在，检查是否缺少某些设置，并添加缺少的设置
                XDocument doc = XDocument.Load("Software.dll.config");
                XElement appSettings = doc.Root.Element("appSettings");

                // 检查每个需要的设置
                CheckAndAddSetting(appSettings, "GamePath", "");
                CheckAndAddSetting(appSettings, "TextContent", "");
                CheckAndAddSetting(appSettings, "UpdatePath", "http://1.14.58.59/updata.xml");
                CheckAndAddSetting(appSettings, "LaunchCount", "0");
                CheckAndAddSetting(appSettings, "EnableCounting", "false");
                CheckAndAddSetting(appSettings, "RetryCount", "5");
                CheckAndAddSetting(appSettings, "RetryDelay", "10");
                CheckAndAddSetting(appSettings, "adcode", "");
                CheckAndAddSetting(appSettings, "UpdateLogUrl", "https://gitee.com/nibadianbanxiaban/software/releases/download/resources/update_log.json");
                CheckAndAddSetting(appSettings, "VersionColor", "Red");
                CheckAndAddSetting(appSettings, "UpdateTimeColor", "Blue");
                // 保存修改后的配置文件
                doc.Save("Software.dll.config");
            }


            txtGamePath = new TextBox();
            weather = new Weather();
            this.DataContext = weather;

            Loaded += async (_, __) =>
            {
                await weather.LoadAsync();
                await weather.RefreshAsync();
            };
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

        // 检查一个设置是否存在，如果不存在，就添加这个设置
        void CheckAndAddSetting(XElement appSettings, string key, string value)
        {
            XElement setting = appSettings.Elements("add").FirstOrDefault(e => e.Attribute("key").Value == key);
            if (setting == null)
            {
                // 如果设置不存在，添加这个设置
                appSettings.Add(new XElement("add", new XAttribute("key", key), new XAttribute("value", value)));
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 读取配置文件
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            // 获取TextContent的值
            string contentTextBox = config.AppSettings.Settings["TextContent"].Value;
            string updatePath = config.AppSettings.Settings["updatePath"].Value;
            // 设置TextBox的文本
            ContentTextBox.Text = contentTextBox;
            Update_IP_address.Text = updatePath;


            // 读取"EnableCounting"的值
            bool enableCounting = bool.Parse(ConfigurationManager.AppSettings["EnableCounting"]);

            // 设置CheckBox的状态
            EnableCountingCheckBox.IsChecked = enableCounting;

            // 如果启用计数，则执行计数逻辑
            if (enableCounting)
            {
                // 读取并增加启动次数
                int launchCount = int.Parse(ConfigurationManager.AppSettings["LaunchCount"]) + 1;

                // 更新启动次数
                UpdateEnableCounting(launchCount);

                // 显示启动次数
                LaunchCount.Content = $"软件已启动 {launchCount} 次";
            }
        }

        private void UpdateEnableCounting(int launchCount)
        {
            // 打开配置文件
            Configuration config1 = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            // 更新启动次数
            config1.AppSettings.Settings["LaunchCount"].Value = launchCount.ToString();

            // 保存配置文件
            config1.Save(ConfigurationSaveMode.Modified);

            // 刷新配置文件
            ConfigurationManager.RefreshSection("appSettings");
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

        private void MediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            // 定时器将每隔 500 毫秒更新媒体播放位置
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += (s, args) =>
            {
                if (mediaElement.Source != null && mediaElement.NaturalDuration.HasTimeSpan)
                {
                    // 计算总时间和当前时间的比率，并更新 mediaPositionSlider 的值。
                    double ratio = mediaElement.Position.TotalSeconds / mediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                    mediaPositionSlider.Value = ratio * mediaPositionSlider.Maximum;
                }
            };
            timer.Start();
        }

        private void MediaPositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (mediaElement.IsLoaded && e.NewValue != e.OldValue)
            {
                // 计算当前滑块位置的时间。
                double ratio = e.NewValue / mediaPositionSlider.Maximum;
                TimeSpan position = TimeSpan.FromSeconds(mediaElement.NaturalDuration.TimeSpan.TotalSeconds * ratio);

                // 跳转媒体播放位置到所选位置。
                mediaElement.Position = position;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            其他窗口.WindowImage nextwindow = new();
            nextwindow.Show();
        }

        private void Button_Click_Home(object sender, RoutedEventArgs e)
        {
            // 创建并设置主页内容（这里是一个字符串）
            string homeContent = "";

            // 将主页内容分配给 ContentControl 的 Content 属性
            contentcon.Content = homeContent;

            this.beta_tabel.Visibility = Visibility.Visible;
        }

        private void Button_Click_GenshinMap(object sender, RoutedEventArgs e)
        {
            其他窗口.WindowGenshinMap nextwindow = new();
            nextwindow.Show();
        }

        private void Button_Click_SelectUP(object sender, RoutedEventArgs e)
        {
            其他窗口.WindowInquirySystem nextwindow = new();
            nextwindow.Show();
        }

        private void Button_Click_PlayGames(object sender, RoutedEventArgs e)
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
            其他窗口.WindowBing nextwindow = new();
            nextwindow.Show();
        }

        private async void Button_Click_Updata(object sender, RoutedEventArgs e)
        {
            // 初始化一个空的字符串变量
            string versionString = string.Empty;

            try
            {
                // 获取更新服务器的IP地址
                var UpdateIP = this.Update_IP_address.Text;
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
            this.beta_tabel.Visibility = Visibility.Hidden;
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

        class TimeSync
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool SetSystemTime(ref SYSTEMTIME st);

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

            public static void SyncTime()
            {
                try
                {
                    const string url = "https://f.m.suning.com/api/ct.do";
                    using (var client = new WebClient())
                    {
                        var jsonStr = client.DownloadString(url);
                        var jobject = JObject.Parse(jsonStr);
                        long timeStamp = Convert.ToInt64(jobject["sysTime2"].ToString());
                        DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timeStamp / 1000d).ToLocalTime();

                        SYSTEMTIME st = new SYSTEMTIME();
                        st.wYear = Convert.ToUInt16(dateTime.Year);
                        st.wMonth = Convert.ToUInt16(dateTime.Month);
                        st.wDay = Convert.ToUInt16(dateTime.Day);
                        st.wHour = Convert.ToUInt16(dateTime.Hour);
                        st.wMinute = Convert.ToUInt16(dateTime.Minute);
                        st.wSecond = Convert.ToUInt16(dateTime.Second);
                        st.wMilliseconds = Convert.ToUInt16(dateTime.Millisecond);

                        if (!SetSystemTime(ref st))
                        {
                            MessageBox.Show("时间同步失败: " + Marshal.GetLastWin32Error());
                        }
                        else
                        {
                            MessageBox.Show("时间同步成功");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("时间同步失败：" + ex.Message);
                }
            }
        }


        private void Button_Click_ColourEgg(object sender, RoutedEventArgs e)
        {
            // 创建提示框
            dialog = new Window
            {
                Width = 400,
                Height = 300,
                Title = "请选择要打开的文件夹目录",
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow,
                Owner = Application.Current.MainWindow,
                Content = new StackPanel()
            };

            string logFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
            var logButton = new Button
            {
                Content = "打开log文件夹",
                Padding = new Thickness(5),
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            logButton.Click += (sender, e) =>
            {
                Process.Start("explorer.exe", logFolder);
                dialog.Close();
            }; 

            string resourcesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources");
            var resourcesButton = new Button
            {
                Content = "打开resources文件夹",
                Padding = new Thickness(5),
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            resourcesButton.Click += (sender, e) =>
            {
                Process.Start("explorer.exe", resourcesFolder);
                dialog.Close();
            };

            string musicFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources\\sound\\music");
            var musicButton = new Button
            {
                Content = "打开music文件夹",
                Padding = new Thickness(5),
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            musicButton.Click += (sender, e) =>
            {
                Process.Start("explorer.exe", musicFolder);
                dialog.Close();
            };

            var syncButton = new Button
            {
                Content = "同步系统时间",
                Padding = new Thickness(5),
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            syncButton.Click += (sender, e) =>
            {
                TimeSync.SyncTime();

                dialog.Close();
            };

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
            var deleteFoldersButton = new Button
            {
                Content = "删除多个文件夹",
                Padding = new Thickness(5),
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            deleteFoldersButton.Click += (sender, e) =>
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
            };

            // 从配置文件中读取当前保存的路径
            string currentPath = ConfigurationManager.AppSettings["GamePath"];

            // 创建一个新的文本框用于输入路径
            var txtGamePath = new TextBox
            {
                Width = 250,
                Name = "txtGamePath"
            };

            // 设置文本框的值为当前保存的路径
            txtGamePath.Text = currentPath;

            // 创建一个新的按钮用于选择文件
            var selectFileButton = new Button
            {
                Content = "选择文件",
                Padding = new Thickness(5),
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            selectFileButton.Click += SelectFileButton_Click;

            // 创建一个新的按钮用于保存路径
            var savePathButton = new Button
            {
                Content = "保存路径",
                Padding = new Thickness(5),
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            savePathButton.Click += Button_Click_SavePath;

            // 创建一个新的StackPanel，将txtGamePath和selectFileButton放在同一行
            var pathPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            pathPanel.Children.Add(txtGamePath);
            pathPanel.Children.Add(selectFileButton);

            // 将所有按钮添加到提示框中
            var stackPanel = new StackPanel();
            stackPanel.Children.Add(logButton);
            stackPanel.Children.Add(resourcesButton);
            stackPanel.Children.Add(musicButton);
            stackPanel.Children.Add(syncButton);
            stackPanel.Children.Add(deleteFoldersButton);
            stackPanel.Children.Add(pathPanel);
            stackPanel.Children.Add(savePathButton);
            dialog.Content = stackPanel;

            // 打开提示框
            dialog.ShowDialog();
        }

        private void Button_Click_SavePath(object sender, RoutedEventArgs e)
        {
            if (txtGamePath != null)
            {
                string newPath = txtGamePath.Text;
                if (System.IO.File.Exists(newPath))
                {
                    UpdateGamePath(newPath);
                    MessageBox.Show("路径已经成功保存。", "成功");

                    // 关闭对话框
                    if (dialog != null)
                    {
                        dialog.Close();
                        dialog = null;
                    }
                }
                else
                {
                    MessageBox.Show("文件不存在，请输入一个有效的文件路径。", "错误");
                }
            }
            else
            {
                MessageBox.Show("文本框不存在。", "错误");
            }
        }

        private async void Button_Click_RefreshWeather(object sender, RoutedEventArgs e)
        {
            await weather.RefreshAsync();
            MessageBox.Show("刷新成功");
        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.ValidateNames = false;
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.Filter = "Executable Files (*.exe)|*.exe";
            if (dialog.ShowDialog() == true)
            {
                txtGamePath.Text = dialog.FileName;
            }
        }

        private void ContentTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 获取TextBox的文本
            string text = ContentTextBox.Text;

            // 保存到配置文件
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["TextContent"].Value = text;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
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

        private void EnableCountingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateEnableCounting(true);
            this.LaunchCount.Visibility = Visibility.Visible;
        }

        private void EnableCountingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateEnableCounting(false);
            this.LaunchCount.Visibility = Visibility.Hidden;
        }

        private void UpdateEnableCounting(bool enableCounting)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["EnableCounting"].Value = enableCounting.ToString();
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
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

        // 获取Music文件夹中的所有音乐文件路径
        string[] musicFiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "resources\\sound\\music");

        private void MediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            // 随机选择一个音乐文件路径
            //Random random = new Random();
            //string selectedMusicFile = musicFiles[random.Next(musicFiles.Length)];

            // 设置MediaElement控件的Source属性为所选音乐文件路径
            //mediaElement.Source = new Uri(selectedMusicFile);
            //mediaElement.Play();
        }

        bool isPlaying = false; // 标记当前媒体文件是否正在播放

        private void Button_Click_MusicPlay_MusicPause(object sender, RoutedEventArgs e)
        {
            if (null == mediaElement.Source)
            {
                // 随机选择一个音乐文件路径
                Random random = new Random();
                string selectedMusicFile = musicFiles[random.Next(musicFiles.Length)];
                // 设置播放路径
                mediaElement.Source = new Uri(selectedMusicFile);
                // 把歌名丢进文本框
                var musicName = System.IO.Path.GetFileName(mediaElement.Source.LocalPath);
                this.music_name.Text = musicName;
            }
            if (isPlaying) // 如果正在播放，则暂停媒体文件
            {
                mediaElement.Pause();
                playPauseButton.Content = "播放";
                playPauseButton.ToolTip = "字面意思，播放";
                isPlaying = false;
            }
            else // 如果没有播放，则开始播放媒体文件
            {
                mediaElement.Play();
                playPauseButton.Content = "暂停";
                playPauseButton.ToolTip = "字面意思，暂停";
                isPlaying = true;
            }
        }

        private void Button_Click_MusicStop(object sender, RoutedEventArgs e)
        {
            mediaElement.Stop();
        }

        private void Button_Click_MusicHandOff(object sender, RoutedEventArgs e)
        {

            // 随机选择一个音乐文件路径
            Random random = new Random();
            string selectedMusicFile = musicFiles[random.Next(musicFiles.Length)];

            // 设置MediaElement控件的Source属性为所选音乐文件路径
            mediaElement.Source = new Uri(selectedMusicFile);
            mediaElement.Play();
            var musicName = System.IO.Path.GetFileName(mediaElement.Source.LocalPath);
            this.music_name.Text = musicName;
        }

        private void volumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mediaElement.Volume = volumeSlider.Value;
        }

        private void Button_Click_BuildJson(object sender, RoutedEventArgs e)
        {
            string appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;//获取软件名称
            string appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();//获取软件版本号
            string outputDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");//获取输出时间
            string osVersion = Environment.OSVersion.Version.ToString();//获取操作系统
            string computerName = Environment.MachineName;//获取系统用户名
            string contentText = ContentTextBox.Text;//获取白框框里的内容

            var musicName = this.music_name.Text;
            if (mediaElement.Source != null)
            {
                string musicFilePath = mediaElement.Source.LocalPath;
                if (!string.IsNullOrEmpty(musicFilePath))
                {
                    musicName = System.IO.Path.GetFileName(musicFilePath);
                }
            }

            Uri playmusicpath = mediaElement.Source;

            Uri defaultMusicPath = new Uri(AppDomain.CurrentDomain.BaseDirectory + "resources/sound/music/music_001.mp3");

            if (mediaElement == null || mediaElement.Source == null)
            {
                mediaElement.Source = defaultMusicPath;
            }

            Uri musicPath = mediaElement.Source;

            var language = Thread.CurrentThread.CurrentCulture;//获取当前语言

            if (string.IsNullOrEmpty(ContentTextBox.Text))
            {
                ContentTextBox.Text = "你什么也没输入";
            };

            var json = new
            {
                Name = appName,//软件名称
                ComputerName = computerName,//系统用户名
                Windows = osVersion,//系统版本号
                Language = language,//语言
                CityWeather = new
                {
                    Province = Data_Province.Text,
                    City = Data_City.Text,
                    Adcode = Data_Adcode.Text,
                    Weather = Data_Weather.Text,
                    Temperature = Data_Temperature.Text,
                    Winddirection = Data_Winddirection.Text,
                    Windpower = Data_Windpower.Text,
                    Humidity = Data_Humidity.Text
                },
                Detail = new
                {
                    Time = outputDate,//时间
                    Version = appVersion,//软件版本号
                    MemoryUsage = $"{(Process.GetCurrentProcess().WorkingSet64 / 1024f) / 1024f}MB", //软件内存占用，单位是MB
                    MusicName = musicName,
                    MusicPath = musicPath.ToString(),
                    Content = ContentTextBox.Text//白框框里的内容
                }
            };

            //序列化为 JSON 字符串
            string jsonString = JsonConvert.SerializeObject(json, Formatting.Indented);

            //判断是否需要创建log文件夹
            string logFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
            if (!Directory.Exists(logFolderPath))
            {
                Directory.CreateDirectory(logFolderPath);
            }

            //构造文件名
            string date = DateTime.Now.ToString("yyyyMMdd");
            string folderPath = "./log/";
            string fileName = $"SoftwareMessage_v{appVersion}_{date}.json";

            //如果文件已存在，则在后面加上数字
            int count = 1;
            while (File.Exists(Path.Combine(folderPath, fileName)))
            {
                fileName = $"SoftwareMessage_v{appVersion}_{date}_{count}.json";
                count++;
            }

            //写入到日志文件中
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log", fileName);
            File.WriteAllText(path, jsonString);

            ContentTextBox.Text = contentText;

            mediaElement.Source = playmusicpath;
        }

        private void listBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = true;
            }
        }

        
    }
}
