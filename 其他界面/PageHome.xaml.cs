using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Serilog;
using Software.Models;
using Software.ViewModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using TextBox = System.Windows.Controls.TextBox;

namespace Software.其他界面
{
    /// <summary>
    /// PageHome.xaml 的交互逻辑
    /// </summary>
    public partial class PageHome : Page
    {
        private TextBox txtGamePath;
        private Weather weather;
        private ToggleSwitch playModeToggleSwitch;

        private MusicPlayer musicPlayer;

        public static PageHome Instance { get; private set; }

        string databasePath = DatabaseHelper.GetDatabasePath();

        private ILogger logger;

        public ILogger MyLoger
        {
            get
            {
                if (logger == null)
                {
                    logger = Log.ForContext<PageHome>();
                }
                return logger;
            }
        }

        public PageHome()
        {
            InitializeComponent();
            Instance = this;

            // 创建 MusicPlayer 实例，并将控件的引用传递给它
            musicPlayer = new MusicPlayer(mediaElement, music_name, playPauseButton, playModeButton, playModeToggleSwitch, loopModeToggleSwitch);

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

            MyLoger.Information("PageHome初始化完成");
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();

                    // 获取TextContent的值
                    string contentTextBoxQuery = "SELECT Value FROM Settings WHERE Key = 'TextContent';";
                    var contentTextBoxCommand = new SqliteCommand(contentTextBoxQuery, connection);
                    string contentTextBox = contentTextBoxCommand.ExecuteScalar()?.ToString();

                    // 设置TextBox的文本
                    ContentTextBox.Text = contentTextBox;
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("读取TextContent时发生错误:{error}", ex.ToString());
                MessageBox.Show("读取TextContent时发生错误: " + ex.Message);
            }
        }

        private DispatcherTimer timer;
        private bool isHandlingSliderValueChanged = false;

        private void MediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            // 定时器将每隔 100 毫秒更新媒体播放位置
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += (s, args) =>
            {
                if (!isHandlingSliderValueChanged && mediaElement.Source != null && mediaElement.NaturalDuration.HasTimeSpan)
                {
                    // 计算总时间和当前时间的比率，并更新 mediaPositionSlider 的值。
                    double ratio = mediaElement.Position.TotalSeconds / mediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                    mediaPositionSlider.Value = ratio * mediaPositionSlider.Maximum;

                    // 更新当前播放时长和总时长的显示。
                    music_TextBlock.Width = new GridLength(80);
                    currentPositionText.Visibility = Visibility.Visible;
                    fenjieText.Visibility = Visibility.Visible;
                    totalDurationText.Visibility = Visibility.Visible;
                    currentPositionText.Text = mediaElement.Position.ToString(@"mm\:ss");
                    fenjieText.Text = "/";
                    totalDurationText.Text = mediaElement.NaturalDuration.TimeSpan.ToString(@"mm\:ss");
                }
            };
            timer.Start();
        }

        private void MediaPositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (mediaElement.IsLoaded && e.NewValue != e.OldValue)
            {
                timer.Stop();  // 暂停定时器

                isHandlingSliderValueChanged = true;  // 设置标志

                // 计算当前滑块位置的时间。
                double ratio = e.NewValue / mediaPositionSlider.Maximum;
                TimeSpan position = TimeSpan.FromSeconds(mediaElement.NaturalDuration.TimeSpan.TotalSeconds * ratio);

                // 跳转媒体播放位置到所选位置。
                mediaElement.Position = position;

                isHandlingSliderValueChanged = false;  // 清除标志

                timer.Start();  // 重新启动定时器
            }
        }

        private void ContentTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                // 获取TextBox的文本
                string text = ContentTextBox.Text;

                // 保存到数据库
                string databasePath = DatabaseHelper.GetDatabasePath();

                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();

                    string updateTextContentQuery = "UPDATE Settings SET Value = @value WHERE Key = 'TextContent';";
                    var updateCommand = new SqliteCommand(updateTextContentQuery, connection);
                    updateCommand.Parameters.AddWithValue("@value", text);
                    updateCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("保存TextContent时发生错误:{error}", ex.ToString());
                MessageBox.Show("保存TextContent时发生错误: " + ex.Message);
            }
        }

        private async void Button_Click_RefreshWeather(object sender, RoutedEventArgs e)
        {
            await weather.RefreshAsync();
            MessageBox.Show("刷新成功");
        }

        private void Button_Click_MusicPlay_MusicPause(object sender, RoutedEventArgs e)
        {
            musicPlayer.PlayPause();
        }

        private void Button_Click_MusicStop(object sender, RoutedEventArgs e)
        {
            musicPlayer.Stop();
        }

        private void Button_Click_MusicHandOff(object sender, RoutedEventArgs e)
        {

            musicPlayer.PlayRandomMusic();
        }

        private void Button_Click_RefreshMusicPath(object sender, RoutedEventArgs e)
        {
            musicPlayer.RefreshMusicFiles();
        }

        private void Button_Click_RandomOrSequential(object sender, RoutedEventArgs e)
        {
            musicPlayer.ToggleRandomOrSequential();
        }

        private void ToggleSwitch_Checked_Loop(object sender, RoutedEventArgs e)
        {
            musicPlayer.ToggleLoopMode();
        }

        private void ToggleSwitch_Unchecked_Loop(object sender, RoutedEventArgs e)
        {
            musicPlayer.ToggleLoopMode();
        }

        private void volumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mediaElement.Volume = volumeSlider.Value;
        }

        private async Task<string> ComputeHashAsync(string filename, Func<HashAlgorithm> createHashAlgorithm)
        {
            using (var stream = File.OpenRead(filename))
            {
                var hash = await Task.Run(() =>
                {
                    using (var hashAlgorithm = createHashAlgorithm())
                    {
                        return hashAlgorithm.ComputeHash(stream);
                    }
                });

                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private async Task<string> ComputeMD5Async(string filename)
        {
            return await ComputeHashAsync(filename, () => MD5.Create());
        }

        private async Task<string> ComputeSHA1Async(string filename)
        {
            return await ComputeHashAsync(filename, () => SHA1.Create());
        }

        private async Task<string> ComputeSHA256Async(string filename)
        {
            return await ComputeHashAsync(filename, () => SHA256.Create());
        }

        private async Task<string> ComputeSHA384Async(string filename)
        {
            return await ComputeHashAsync(filename, () => SHA384.Create());
        }

        private async Task<string> ComputeSHA512Async(string filename)
        {
            return await ComputeHashAsync(filename, () => SHA512.Create());
        }

        private async void Page_Drop(object sender, DragEventArgs e)
        {
            Debug.WriteLine("Drop event triggered.");

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                var existingDialog = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.Title == "MD5/SHA1/SHA256/SHA384/SHA512值");

                if (existingDialog == null)
                {
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
                        Topmost = false
                    };

                    foreach (var file in files)
                    {
                        string md5 = await ComputeMD5Async(file);
                        string sha1 = await ComputeSHA1Async(file);
                        string sha256 = await ComputeSHA256Async(file);
                        string sha384 = await ComputeSHA384Async(file);
                        string sha512 = await ComputeSHA512Async(file);

                        dialog.Content = new PageHashDialog(file, md5, sha1, sha256, sha384, sha512);
                        Debug.WriteLine("Showing dialog.");
                        dialog.Show();
                    }
                }
                else
                {
                    Debug.WriteLine("Dialog already open.");
                }
            }
        }

    }
}
