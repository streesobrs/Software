using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Software.ViewModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace Software.其他界面
{
    /// <summary>
    /// PageHome.xaml 的交互逻辑
    /// </summary>
    public partial class PageHome : Page
    {
        private Window dialog;
        private TextBox txtGamePath;
        private Weather weather;

        public PageHome()
        {
            InitializeComponent();

            txtGamePath = new TextBox();
            weather = new Weather();
            this.DataContext = weather;

            this.AllowDrop = true;
            this.Drop += Page_Drop;

            Loaded += async (_, __) =>
            {
                await weather.LoadAsync();
                await weather.RefreshAsync();
            };
        }

        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
        // 获取Music文件夹中的所有音乐文件路径
        string[] musicFiles;

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            musicFiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "resources\\sound\\music");

            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            // 获取TextContent的值
            string contentTextBox = ConfigurationManager.AppSettings["TextContent"];
            // 设置TextBox的文本
            ContentTextBox.Text = contentTextBox;
        }

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
                Background = (Brush)new BrushConverter().ConvertFromString("#CADFF6"),
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
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            logButton.Click += (sender, e) =>
            {
                Process.Start("explorer.exe", logFolder);
                dialog.Close();
            };

            string resourcesFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources");
            var resourcesButton = new Button
            {
                Content = "打开resources文件夹",
                Padding = new Thickness(5),
                Margin = new Thickness(5),
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            resourcesButton.Click += (sender, e) =>
            {
                Process.Start("explorer.exe", resourcesFolder);
                dialog.Close();
            };

            string musicFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources\\sound\\music");
            var musicButton = new Button
            {
                Content = "打开music文件夹",
                Padding = new Thickness(5),
                Margin = new Thickness(5),
                Foreground = Brushes.Black,
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
                Foreground = Brushes.Black,
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
                Foreground = Brushes.Black,
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
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            selectFileButton.Click += SelectFileButton_Click;

            // 创建一个新的按钮用于保存路径
            var savePathButton = new Button
            {
                Content = "保存路径",
                Padding = new Thickness(5),
                Margin = new Thickness(5),
                Foreground = Brushes.Black,
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

        private void UpdateGamePath(string newPath)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["GamePath"].Value = newPath;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
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



        private void EnableCountingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateEnableCounting(true);
            mainWindow.LaunchCount.Visibility = Visibility.Visible;
        }

        private void EnableCountingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateEnableCounting(false);
            mainWindow.LaunchCount.Visibility = Visibility.Hidden;
        }

        private void UpdateEnableCounting(bool enableCounting)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["EnableCounting"].Value = enableCounting.ToString();
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }
        
        // 标记当前媒体文件是否正在播放
        bool isPlaying = false; 

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
            playPauseButton.Content = "播放";
            playPauseButton.ToolTip = "字面意思，播放";
            isPlaying = false;
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
            playPauseButton.Content = "暂停";
            playPauseButton.ToolTip = "字面意思，暂停";
            isPlaying = true;
        }

        private void Button_Click_RefreshMusicPath(object sender, RoutedEventArgs e)
        {
            musicFiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "resources\\sound\\music");
        }

        private void volumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mediaElement.Volume = volumeSlider.Value;
        }

        private void Page_Drop(object sender, DragEventArgs e)
        {
            Debug.WriteLine("Drop event triggered.");

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    string md5 = ComputeMD5(file);
                    string sha1 = ComputeSHA1(file);
                    string sha256 = ComputeSHA256(file);
                    string sha384 = ComputeSHA384(file);
                    string sha512 = ComputeSHA512(file);
                    //Debug.WriteLine($"File: {file}, MD5: {md5}, SHA1: {sha1}");

                    // 创建并显示自定义对话框
                    var dialog = new Window
                    {
                        Width = 400,
                        Height = 300,
                        Title = "MD5/SHA1/SHA256/SHA384/SHA512值",
                        Background = (Brush)new BrushConverter().ConvertFromString("#CADFF6"),
                        ShowInTaskbar = false,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ResizeMode = ResizeMode.NoResize,
                        WindowStyle = WindowStyle.SingleBorderWindow,
                        Owner = Application.Current.MainWindow,
                        Content = new PageHashDialog(file, md5, sha1,sha256,sha384,sha512)
                    };
                    Debug.WriteLine("Showing dialog.");
                    bool? result = dialog.ShowDialog();
                    Debug.WriteLine("Dialog closed.");
                }
            }
        }

        private string ComputeMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private string ComputeSHA1(string filename)
        {
            using (var sha1 = SHA1.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = sha1.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private string ComputeSHA256(string filename)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private string ComputeSHA384(string filename)
        {
            using (var sha384 = SHA384.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = sha384.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private string ComputeSHA512(string filename)
        {
            using (var sha512 = SHA512.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = sha512.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

    }
}
