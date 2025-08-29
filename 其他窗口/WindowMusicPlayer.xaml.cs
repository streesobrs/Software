using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media; // 添加 Brushes 支持
using System.Windows.Threading;
using Microsoft.Win32;
using Software.ViewModels; // 添加 LyricManager 支持
using Serilog; // 添加日志命名空间

namespace Software.其他窗口
{
    public partial class WindowMusicPlayer : Window
    {
        private ILogger _logger;

        public ILogger Logger
        {
            get
            {
                if (_logger == null)
                {
                    _logger = Log.ForContext<WindowMusicPlayer>();
                }
                return _logger;
            }
        }

        public MusicPlayer MusicPlayer { get; private set; }
        private DispatcherTimer _updateTimer;

        private int _lastLyricIndex = -1;

        // 添加标志位，用于区分是用户拖动还是程序更新
        private bool _isUserDragging = false;

        public WindowMusicPlayer()
        {
            Logger.Information("音乐播放器窗口初始化");
            InitializeComponent();
            Loaded += WindowMusicPlayer_Loaded;
        }

        private void WindowMusicPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Debug("音乐播放器窗口加载完成");

            try
            {
                // 初始化 MusicPlayer 实例
                MusicPlayer = new MusicPlayer(
                    mediaElement,
                    musicNameTextBlock,
                    playPauseButton,
                    playModeButton,
                    loopModeButton
                );

                // 设置音量初始值
                MusicPlayer.Volume = 0.5;

                // 加载音乐列表
                MusicPlayer.RefreshMusicFiles();
                MusicList.ItemsSource = MusicPlayer.MusicFiles;

                // 注册事件
                MusicPlayer.OnMusicChanged += MusicPlayer_OnMusicChanged;
                MusicPlayer.OnPlaybackError += MusicPlayer_OnPlaybackError;
                MusicPlayer.OnPlaybackMessage += MusicPlayer_OnPlaybackMessage;
                MusicPlayer.OnLyricsLoaded += MusicPlayer_OnLyricsLoaded;

                // 启动定时器更新进度
                _updateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(10)
                };
                _updateTimer.Tick += UpdateTimer_Tick;
                _updateTimer.Start();

                // 设置数据上下文以便绑定
                DataContext = this;

                Logger.Information("音乐播放器初始化完成");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "音乐播放器初始化失败: {ErrorMessage}", ex.Message);
                MessageBox.Show($"音乐播放器初始化失败: {ex.Message}",
                                "错误",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                Close();
            }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (MusicPlayer != null && !_isUserDragging)
            {
                // 更新进度条
                MusicProgress.Value = MusicPlayer.Progress;
                ProgressSlider.Value = MusicPlayer.Progress;

                // 更新歌词位置
                if (MusicPlayer != null && !_isUserDragging)
                {
                    // 获取当前歌词索引
                    int? lyricIndex = MusicPlayer.CurrentLyricIndex;

                    if (lyricIndex.HasValue && lyricIndex != _lastLyricIndex)
                    {
                        _lastLyricIndex = lyricIndex.Value;
                        UpdateLyricsDisplay(MusicPlayer.CurrentLyrics, lyricIndex);
                    }
                }

                // 更新时间显示
                CurrentTimeText.Text = MusicPlayer.CurrentTime.ToString(@"mm\:ss");
                TotalTimeText.Text = MusicPlayer.TotalTime.ToString(@"mm\:ss");
            }
        }

        // 添加新方法：更新歌词显示
        private void UpdateLyricsDisplay(List<LyricManager.LyricLine> lyrics, int? currentIndex = null)
        {
            Logger.Debug("更新歌词显示，当前索引: {LyricIndex}", currentIndex);

            // 创建歌词项集合
            var lyricItems = new List<dynamic>();

            for (int i = 0; i < lyrics.Count; i++)
            {
                bool isCurrent = (currentIndex.HasValue && i == currentIndex.Value);

                lyricItems.Add(new
                {
                    Text = lyrics[i].Text,
                    Color = isCurrent ? Brushes.White : Brushes.Gray
                });
            }

            // 设置数据源
            LyricsPanel.ItemsSource = lyricItems;

            // 滚动到当前歌词
            if (currentIndex.HasValue && currentIndex.Value >= 0)
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    if (LyricsPanel.ItemContainerGenerator.ContainerFromIndex(currentIndex.Value) is FrameworkElement container)
                    {
                        container.BringIntoView();
                    }
                }), DispatcherPriority.Render);
            }
        }

        // 歌词加载完成事件处理
        private void MusicPlayer_OnLyricsLoaded(object sender, List<LyricManager.LyricLine> e)
        {
            Logger.Debug("歌词加载完成事件处理，歌词行数: {LyricCount}", e?.Count ?? 0);

            Dispatcher.Invoke(() => {
                if (e == null || e.Count == 0)
                {
                    // 没有歌词时显示提示
                    LyricsPanel.ItemsSource = new[] { new { Text = "未找到歌词", Color = Brushes.Gray } };
                    Logger.Debug("未找到歌词，显示提示信息");
                }
                else
                {
                    UpdateLyricsDisplay(e);
                }
            });
        }

        private void MusicPlayer_OnMusicChanged(object sender, MusicInfo e)
        {
            Logger.Information("音乐已更改: {MusicName} - {Artist}", e.DisplayName, e.Artist);

            // 更新窗口标题和歌曲名称
            Title = $"音乐播放器 - {e.DisplayName}";
            musicNameTextBlock.Text = e.DisplayName;

            // 更新专辑封面
            try
            {
                AlbumCover.Source = e.AlbumCover ?? MusicPlayer.LoadDefaultAlbumCover();
                Logger.Debug("专辑封面已更新");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "更新专辑封面失败: {ErrorMessage}", ex.Message);
                // 忽略封面加载错误
            }
        }

        private void MusicPlayer_OnPlaybackError(object sender, string e)
        {
            Logger.Error("播放错误: {ErrorMessage}", e);
            MessageBox.Show(e, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void MusicPlayer_OnPlaybackMessage(object sender, string e)
        {
            Logger.Information("播放消息: {Message}", e);
            MessageBox.Show(e, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #region 按钮事件处理
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("播放/暂停按钮点击");
            MusicPlayer?.PlayPause();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("上一首按钮点击");
            MusicPlayer?.PlayPreviousMusic();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("下一首按钮点击");
            MusicPlayer?.PlayNextMusic();
        }

        private void PlayModeButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("播放模式按钮点击");
            MusicPlayer?.ToggleRandomOrSequential();
        }

        private void LoopModeButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("循环模式按钮点击");
            MusicPlayer?.ToggleLoopMode();
        }

        private void MusicList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MusicList.SelectedItem is MusicInfo selectedMusic)
            {
                Logger.Debug("音乐列表选择更改: {MusicName}", selectedMusic.DisplayName);
                MusicPlayer?.PlayMusic(selectedMusic);
            }
        }

        private void AddMusicButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("添加音乐按钮点击");

            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "音频文件|*.mp3;*.wav;*.wma;*.flac;*.ogg;*.m4a|所有文件|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                Logger.Information("用户选择了 {FileCount} 个文件", openFileDialog.FileNames.Length);
                MusicPlayer?.AddMusicFiles(openFileDialog.FileNames);
                // 刷新列表显示
                MusicList.Items.Refresh();
            }
            else
            {
                Logger.Debug("用户取消了文件选择");
            }
        }

        // 手动刷新按钮点击事件
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("刷新按钮点击");
            MusicPlayer?.RefreshMusicFiles();
            MusicList.ItemsSource = MusicPlayer.MusicFiles;
        }

        // 进度条拖动开始事件
        private void ProgressSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            Logger.Debug("进度条拖动开始");
            _isUserDragging = true;
        }

        // 进度条拖动结束事件
        private void ProgressSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            Logger.Debug("进度条拖动结束，值: {Value}", ProgressSlider.Value);
            _isUserDragging = false;
            if (MusicPlayer != null)
            {
                // 只在拖动结束时更新播放位置
                MusicPlayer.SetProgress(ProgressSlider.Value);
            }
        }
        #endregion

        protected override void OnClosed(EventArgs e)
        {
            Logger.Information("音乐播放器窗口关闭");

            base.OnClosed(e);

            // 停止定时器
            _updateTimer?.Stop();
            _updateTimer = null;

            // 释放资源
            MusicPlayer?.Dispose();
        }
    }
}