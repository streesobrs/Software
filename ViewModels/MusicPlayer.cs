using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TagLib;
using Serilog;
using Microsoft.Data.Sqlite;
using Software.Models;
using System.Threading.Tasks;

namespace Software.ViewModels
{
    public class MusicPlayer : INotifyPropertyChanged, IDisposable
    {
        private ILogger _logger;

        public ILogger Logger
        {
            get
            {
                if (_logger == null)
                {
                    _logger = Log.ForContext<MusicPlayer>();
                }
                return _logger;
            }
        }

        #region 私有字段
        private readonly MediaElement _mediaElement;
        private readonly TextBlock _musicNameTextBox;
        private readonly Button _playPauseButton;
        private readonly Button _playModeButton;
        private readonly Button _loopModeButton;
        private int _currentMusicIndex = -1;
        private bool _isDisposed = false;
        private readonly Random _random = new Random();
        private DispatcherTimer _progressTimer;
        private LyricManager _lyricManager;

        // 循环模式枚举
        public enum LoopMode
        {
            None,       // 无循环
            Single,     // 单曲循环
            List        // 列表循环
        }

        // 循环模式
        private LoopMode _currentLoopMode = LoopMode.None;

        // 播放状态跟踪
        private bool _isPlaying = false;
        private bool _isRandomPlay = true;

        // 新增字段：播放历史记录
        private Stack<MusicInfo> _playbackHistory = new Stack<MusicInfo>();
        private Stack<MusicInfo> _playbackFuture = new Stack<MusicInfo>();

        // 音乐文件相关
        private readonly string _lastPlayedMusicFilePath;
        private readonly string _defaultMusicFolder;

        // 播放统计相关
        private DateTime _lastRecordedTime = DateTime.MinValue;
        private string _lastRecordedSong = string.Empty;
        private bool _isRecordingPlay = false;
        #endregion

        #region 公共属性
        // 可绑定的音乐列表
        public ObservableCollection<MusicInfo> MusicFiles { get; private set; } = new ObservableCollection<MusicInfo>();

        // 当前循环模式
        public LoopMode CurrentLoopMode => _currentLoopMode;

        // 使用公共属性访问歌词
        public List<LyricManager.LyricLine> CurrentLyrics => _lyricManager?.CurrentLyrics;
        public int? CurrentLyricIndex => _lyricManager?.CurrentLyricIndex;

        // 播放进度
        private double _progress;
        public double Progress
        {
            get => _progress;
            private set
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        // 当前播放时间
        public TimeSpan CurrentTime => _mediaElement?.Position ?? TimeSpan.Zero;

        // 总播放时间
        public TimeSpan TotalTime
        {
            get
            {
                if (_mediaElement != null && _mediaElement.NaturalDuration.HasTimeSpan)
                    return _mediaElement.NaturalDuration.TimeSpan;
                return TimeSpan.Zero;
            }
        }

        // 当前音量
        public double Volume
        {
            get => _mediaElement?.Volume ?? 0.5; // 默认音量0.5
            set
            {
                if (_mediaElement != null)
                {
                    _mediaElement.Volume = Math.Clamp(value, 0, 1);
                    OnPropertyChanged(nameof(Volume));
                }
            }
        }

        // 是否静音
        public bool IsMuted
        {
            get => _mediaElement?.IsMuted ?? false;
            set
            {
                if (_mediaElement != null)
                {
                    _mediaElement.IsMuted = value;
                    OnPropertyChanged(nameof(IsMuted));
                }
            }
        }

        // 当前播放的音乐信息
        public MusicInfo CurrentMusic => _currentMusicIndex >= 0 && _currentMusicIndex < MusicFiles.Count
            ? MusicFiles[_currentMusicIndex]
            : null;

        // 只读属性，用于外部获取状态
        public bool IsListLoop => _currentLoopMode == LoopMode.List;
        public bool IsLoopMode => _currentLoopMode == LoopMode.Single;
        public bool IsRandomPlay => _isRandomPlay;
        public bool IsPlaying => _isPlaying;
        #endregion

        #region 构造函数
        public MusicPlayer(MediaElement mediaElement, TextBlock musicNameTextBox, Button playPauseButton,
                          Button playModeButton, Button loopModeButton)
        {
            Logger.Information("MusicPlayer 初始化开始");

            _mediaElement = mediaElement ?? throw new ArgumentNullException(nameof(mediaElement));
            _musicNameTextBox = musicNameTextBox;
            _playPauseButton = playPauseButton ?? throw new ArgumentNullException(nameof(playPauseButton));
            _playModeButton = playModeButton;
            _loopModeButton = loopModeButton;

            _defaultMusicFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "sound", "music");
            _lastPlayedMusicFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "LastPlayedMusic.ini");

            // 初始化歌词管理器
            _lyricManager = new LyricManager(Application.Current.Dispatcher);
            _lyricManager.OnLyricsLoaded += LyricManager_OnLyricsLoaded;

            Initialize();

            Logger.Information("MusicPlayer 初始化完成");
        }

        public MusicPlayer(MediaElement mediaElement, TextBlock musicNameTextBox, Button playPauseButton)
            : this(mediaElement, musicNameTextBox, playPauseButton, null, null)
        { }
        #endregion

        #region 初始化
        private void Initialize()
        {
            Logger.Debug("开始初始化 MusicPlayer");

            if (!Directory.Exists(_defaultMusicFolder))
            {
                Logger.Information("创建默认音乐文件夹: {MusicFolder}", _defaultMusicFolder);
                Directory.CreateDirectory(_defaultMusicFolder);
            }

            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _progressTimer.Tick += ProgressTimer_Tick;

            _mediaElement.MediaOpened += MediaElement_MediaOpened;
            _mediaElement.MediaEnded += MediaElement_MediaEnded;
            _mediaElement.MediaFailed += MediaElement_MediaFailed;

            // 确保媒体元素设置为手动模式
            _mediaElement.LoadedBehavior = MediaState.Manual;
            _mediaElement.UnloadedBehavior = MediaState.Manual;

            RefreshMusicFiles();
            RestoreLastPlayedMusic();
            UpdatePlayPauseButtonText();

            // 初始化循环模式按钮文本
            UpdateLoopModeButtonText();

            Logger.Debug("MusicPlayer 初始化完成");
        }
        #endregion

        #region 事件处理
        private void MediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            Logger.Debug("媒体文件已打开: {MediaSource}", _mediaElement.Source);

            _progressTimer.Start();
            OnPropertyChanged(nameof(TotalTime));

            // 更新当前播放音乐的时长
            if (CurrentMusic != null && _mediaElement.NaturalDuration.HasTimeSpan)
            {
                TimeSpan duration = _mediaElement.NaturalDuration.TimeSpan;
                CurrentMusic.Duration = duration;
                OnPropertyChanged(nameof(CurrentMusic)); // 通知UI更新

                Logger.Debug("媒体时长: {Duration}", duration);

                // 更新列表中的时长显示
                var index = MusicFiles.IndexOf(CurrentMusic);
                if (index >= 0)
                {
                    // 创建新的实例以触发UI更新
                    MusicFiles[index] = new MusicInfo
                    {
                        Index = CurrentMusic.Index,
                        FilePath = CurrentMusic.FilePath,
                        FileName = CurrentMusic.FileName,
                        DisplayName = CurrentMusic.DisplayName,
                        Folder = CurrentMusic.Folder,
                        Duration = duration,
                        Artist = CurrentMusic.Artist,
                        Album = CurrentMusic.Album,
                        AlbumCover = CurrentMusic.AlbumCover
                    };
                }
            }
        }

        // 添加歌词加载事件处理
        private void LyricManager_OnLyricsLoaded(object sender, List<LyricManager.LyricLine> e)
        {
            Logger.Debug("歌词加载完成事件处理，歌词行数: {LyricCount}", e?.Count ?? 0);
            OnLyricsLoaded?.Invoke(this, e);
        }

        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            Logger.Debug("媒体播放结束");
            HandleMediaEnded();
        }

        private void MediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            string errorMessage = $"播放失败: {e.ErrorException.Message}";
            Logger.Error(e.ErrorException, "媒体播放失败: {ErrorMessage}", e.ErrorException.Message);

            // 添加额外错误诊断信息
            if (e.ErrorException is InvalidOperationException)
            {
                errorMessage += "\n可能原因：";
                errorMessage += "\n- 文件格式不受支持";
                errorMessage += "\n- 文件已损坏";
                errorMessage += "\n- 缺少必要的解码器";

                Logger.Warning("媒体播放失败可能原因: 文件格式不受支持/文件已损坏/缺少必要的解码器");
            }

            // 清除歌词
            _lyricManager?.ClearLyrics();

            Debug.WriteLine(errorMessage);
            OnPlaybackError?.Invoke(this, errorMessage);

            if (MusicFiles.Count > 0)
            {
                Logger.Debug("尝试播放下一首音乐");
                PlayNextMusic();
            }
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (_mediaElement != null && _mediaElement.NaturalDuration.HasTimeSpan)
            {
                double newProgress = (_mediaElement.Position.TotalSeconds /
                                     _mediaElement.NaturalDuration.TimeSpan.TotalSeconds) * 100;

                if (Math.Abs(Progress - newProgress) > 0.1)
                {
                    _progress = newProgress;
                    OnPropertyChanged(nameof(Progress));
                    OnPropertyChanged(nameof(CurrentTime));
                }
            }

            // 更新歌词位置
            if (_mediaElement != null && _mediaElement.NaturalDuration.HasTimeSpan)
            {
                _lyricManager.UpdateLyricsPosition(_mediaElement.Position);
            }
        }
        #endregion

        #region 公共方法
        public string GetCurrentMusicName()
        {
            return CurrentMusic?.DisplayName ?? "无当前播放音乐";
        }

        public string GetCurrentMusicPath()
        {
            return CurrentMusic?.FilePath ?? string.Empty;
        }

        public void RefreshMusicFiles()
        {
            Logger.Debug("开始刷新音乐文件列表");

            try
            {
                MusicFiles.Clear();
                _playbackHistory.Clear();
                _playbackFuture.Clear();

                var supportedExtensions = new[] { ".mp3", ".wav", ".wma", ".flac", ".ogg", ".m4a" };
                var files = Directory.GetFiles(_defaultMusicFolder, "*.*", SearchOption.AllDirectories)
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLower()));

                Logger.Debug("找到 {FileCount} 个支持的音乐文件", files.Count());

                int index = 1;
                foreach (var file in files)
                {
                    var musicInfo = new MusicInfo
                    {
                        Index = index++,
                        FilePath = file,
                        FileName = Path.GetFileName(file),
                        DisplayName = Path.GetFileNameWithoutExtension(file),
                        Folder = Path.GetDirectoryName(file)
                    };

                    // 读取音频文件的元数据
                    LoadMusicMetadata(musicInfo);

                    MusicFiles.Add(musicInfo);
                }

                Logger.Information("刷新音乐文件完成，共 {MusicCount} 首音乐", MusicFiles.Count);
                OnMusicListUpdated?.Invoke(this, MusicFiles.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "刷新音乐文件失败: {ErrorMessage}", ex.Message);
                Debug.WriteLine($"刷新音乐文件失败: {ex.Message}");
                OnPlaybackError?.Invoke(this, $"刷新音乐列表失败: {ex.Message}");
            }
        }

        // 加载音乐元数据（艺术家、专辑、封面、时长等）
        private void LoadMusicMetadata(MusicInfo musicInfo)
        {
            try
            {
                Logger.Debug("开始加载音乐元数据: {FilePath}", musicInfo.FilePath);

                using (var file = TagLib.File.Create(musicInfo.FilePath))
                {
                    // 读取艺术家信息
                    if (!string.IsNullOrWhiteSpace(file.Tag.FirstPerformer))
                    {
                        musicInfo.Artist = file.Tag.FirstPerformer;
                        Logger.Debug("读取艺术家信息: {Artist}", musicInfo.Artist);
                    }

                    // 读取专辑信息
                    if (!string.IsNullOrWhiteSpace(file.Tag.Album))
                    {
                        musicInfo.Album = file.Tag.Album;
                        Logger.Debug("读取专辑信息: {Album}", musicInfo.Album);
                    }

                    // 读取时长
                    if (file.Properties != null && file.Properties.Duration.TotalSeconds > 0)
                    {
                        musicInfo.Duration = file.Properties.Duration;
                        Logger.Debug("读取时长信息: {Duration}", musicInfo.Duration);
                    }
                    else
                    {
                        // 尝试通过文件大小估算时长
                        var fileInfo = new FileInfo(musicInfo.FilePath);
                        double sizeMB = fileInfo.Length / (1024.0 * 1024.0);
                        musicInfo.Duration = TimeSpan.FromMinutes(sizeMB * 0.1); // 估算：1MB ≈ 0.1分钟
                        Logger.Debug("估算时长: {Duration}", musicInfo.Duration);
                    }

                    // 读取专辑封面
                    var pictures = file.Tag.Pictures;
                    if (pictures != null && pictures.Length > 0)
                    {
                        var picture = pictures[0];
                        using (var ms = new MemoryStream(picture.Data.Data))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = ms;
                            bitmap.EndInit();
                            bitmap.Freeze();  // 使其可在其他线程使用
                            musicInfo.AlbumCover = bitmap;
                        }
                        Logger.Debug("成功加载专辑封面");
                    }
                    else
                    {
                        Logger.Debug("未找到专辑封面");
                    }
                }

                Logger.Debug("音乐元数据加载完成: {FilePath}", musicInfo.FilePath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "读取音乐元数据失败: {FilePath} - {ErrorMessage}", musicInfo.FilePath, ex.Message);
                Debug.WriteLine($"读取音乐元数据失败: {musicInfo.FilePath} - {ex.Message}");

                // 使用默认封面
                musicInfo.AlbumCover = LoadDefaultAlbumCover();

                // 设置默认时长
                musicInfo.Duration = TimeSpan.FromMinutes(3);
            }

            // 如果没有封面，使用默认封面
            if (musicInfo.AlbumCover == null)
            {
                musicInfo.AlbumCover = LoadDefaultAlbumCover();
                Logger.Debug("使用默认专辑封面");
            }
        }

        // 加载默认专辑封面
        public BitmapImage LoadDefaultAlbumCover()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/resources/image/default_album.png");
                return new BitmapImage(uri);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "加载默认专辑封面失败: {ErrorMessage}", ex.Message);
                return null;
            }
        }

        public void PlayPause()
        {
            Logger.Debug("播放/暂停操作");

            if (MusicFiles.Count == 0)
            {
                Logger.Warning("尝试播放但未找到音乐文件");
                OnPlaybackError?.Invoke(this, "没有找到音乐文件");
                return;
            }

            if (_mediaElement.Source == null)
            {
                Logger.Debug("媒体源为空，开始播放第一首音乐");
                PlayMusicAtIndex(0);
                return;
            }

            if (_isPlaying)
            {
                Logger.Debug("暂停播放");
                _mediaElement.Pause();
            }
            else
            {
                Logger.Debug("继续播放");
                _mediaElement.Play();
            }

            _isPlaying = !_isPlaying;
            UpdatePlayPauseButtonText();
        }

        public void Stop()
        {
            Logger.Debug("停止播放");

            _mediaElement.Stop();
            _isPlaying = false;
            UpdatePlayPauseButtonText();
            _progressTimer.Stop();
            _progress = 0;
            OnPropertyChanged(nameof(Progress));
        }

        public void PlayRandomMusic()
        {
            Logger.Debug("随机播放音乐");

            if (MusicFiles.Count == 0)
            {
                Logger.Warning("尝试随机播放但未找到音乐文件");
                OnPlaybackError?.Invoke(this, "没有找到音乐文件");
                return;
            }

            if (MusicFiles.Count == 1)
            {
                Logger.Debug("只有一首音乐，直接播放");
                PlayMusicAtIndex(0);
                return;
            }

            int newIndex;
            do
            {
                newIndex = _random.Next(MusicFiles.Count);
            } while (newIndex == _currentMusicIndex && MusicFiles.Count > 1);

            Logger.Debug("随机选择音乐索引: {Index}", newIndex);
            PlayMusicAtIndex(newIndex);
        }

        public void PlayNextMusic()
        {
            Logger.Debug("播放下一首音乐");

            if (MusicFiles.Count == 0)
            {
                Logger.Warning("尝试播放下一首但未找到音乐文件");
                OnPlaybackError?.Invoke(this, "没有找到音乐文件");
                return;
            }

            // 随机模式且存在"未来"记录
            if (_isRandomPlay && _playbackFuture.Count > 0)
            {
                Logger.Debug("从未来记录中获取下一首音乐");

                // 当前歌曲存入历史
                if (CurrentMusic != null)
                {
                    _playbackHistory.Push(CurrentMusic);
                }

                // 从"未来"栈获取下一首
                var nextMusic = _playbackFuture.Pop();
                int index = MusicFiles.IndexOf(nextMusic);
                if (index != -1)
                {
                    PlayMusicAtIndex(index, true);
                }
            }
            // 其他情况保持原逻辑
            else
            {
                if (_isRandomPlay)
                {
                    Logger.Debug("随机模式下播放下一首");
                    PlayRandomMusic();
                }
                else
                {
                    int nextIndex = _currentMusicIndex + 1;
                    if (nextIndex >= MusicFiles.Count)
                    {
                        nextIndex = _currentLoopMode == LoopMode.List ? 0 : MusicFiles.Count - 1;
                    }
                    Logger.Debug("顺序模式下播放下一首，索引: {Index}", nextIndex);
                    PlayMusicAtIndex(nextIndex);
                }
            }
        }

        public void PlayPreviousMusic()
        {
            Logger.Debug("播放上一首音乐");

            if (MusicFiles.Count == 0)
            {
                Logger.Warning("尝试播放上一首但未找到音乐文件");
                OnPlaybackError?.Invoke(this, "没有找到音乐文件");
                return;
            }

            // 随机模式：使用历史记录回溯
            if (_isRandomPlay && _playbackHistory.Count > 0)
            {
                Logger.Debug("从历史记录中获取上一首音乐");

                // 当前歌曲存入"未来"栈
                if (CurrentMusic != null)
                {
                    _playbackFuture.Push(CurrentMusic);
                }

                // 从历史栈获取上一首
                var previousMusic = _playbackHistory.Pop();
                int index = MusicFiles.IndexOf(previousMusic);
                if (index != -1)
                {
                    PlayMusicAtIndex(index, true);
                }
            }
            // 顺序模式：原逻辑
            else
            {
                int prevIndex;
                if (_currentMusicIndex <= 0)
                {
                    prevIndex = _currentLoopMode == LoopMode.List ? MusicFiles.Count - 1 : 0;
                }
                else
                {
                    prevIndex = _currentMusicIndex - 1;
                }

                Logger.Debug("顺序模式下播放上一首，索引: {Index}", prevIndex);
                PlayMusicAtIndex(prevIndex);
            }
        }

        public void PlayMusicAtIndex(int index, bool isFromHistoryOrFuture = false)
        {
            Logger.Debug("开始播放音乐，索引: {Index}, 来自历史/未来: {IsFromHistory}", index, isFromHistoryOrFuture);

            if (index < 0 || index >= MusicFiles.Count)
            {
                Logger.Warning("无效的音乐索引: {Index}", index);
                return;
            }

            try
            {
                // 记录上一首歌的播放时长（如果播放时间超过10秒）
                if (CurrentMusic != null && _mediaElement.NaturalDuration.HasTimeSpan)
                {
                    TimeSpan currentPosition = _mediaElement.Position;

                    // 如果播放时间超过10秒，记录为一次播放
                    if (currentPosition.TotalSeconds >= 10)
                    {
                        RecordPlay(currentPosition);
                    }
                }

                // 停止当前播放
                _mediaElement.Stop();

                // 记录当前歌曲到历史（非历史/未来触发的播放）
                if (!isFromHistoryOrFuture && CurrentMusic != null)
                {
                    // 随机模式下记录完整历史
                    if (_isRandomPlay)
                    {
                        _playbackHistory.Push(CurrentMusic);
                        Logger.Debug("将当前音乐添加到历史记录");
                    }
                    // 顺序模式只记录当前歌曲
                    else
                    {
                        // 清空历史只保留最新记录
                        _playbackHistory.Clear();
                        _playbackHistory.Push(CurrentMusic);
                        Logger.Debug("清空历史记录并添加当前音乐");
                    }
                    _playbackFuture.Clear();
                }

                _currentMusicIndex = index;
                var selectedMusic = MusicFiles[index];

                Logger.Information("播放音乐: {MusicName} - {Artist}", selectedMusic.DisplayName, selectedMusic.Artist);

                // 加载歌词
                if (selectedMusic != null)
                {
                    Logger.Debug("开始加载歌词");
                    _ = _lyricManager.LoadLyricsAsync(
                        selectedMusic.FilePath,
                        selectedMusic.DisplayName,
                        selectedMusic.Artist
                    );
                }

                // 设置源并播放
                _mediaElement.Source = new Uri(selectedMusic.FilePath);
                _mediaElement.Play();

                _isPlaying = true;
                UpdatePlayPauseButtonText();
                UpdateMusicInfoDisplay();

                SaveLastPlayedMusic(selectedMusic.FilePath);
                OnMusicChanged?.Invoke(this, selectedMusic);

                Logger.Debug("音乐播放开始");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "播放音乐失败: {ErrorMessage}", ex.Message);
                Debug.WriteLine($"播放音乐失败: {ex.Message}");
                OnPlaybackError?.Invoke(this, $"播放失败: {ex.Message}");
            }
        }

        public void PlayMusic(MusicInfo music)
        {
            if (music == null)
                return;

            int index = MusicFiles.IndexOf(music);
            if (index != -1)
            {
                PlayMusicAtIndex(index);
            }
        }

        public void ToggleRandomOrSequential()
        {
            _isRandomPlay = !_isRandomPlay;
            Logger.Information("切换播放模式: {Mode}", _isRandomPlay ? "随机模式" : "顺序模式");

            // 切换模式时清空历史记录
            _playbackHistory.Clear();
            _playbackFuture.Clear();
            Logger.Debug("清空播放历史记录");

            if (_playModeButton != null)
            {
                _playModeButton.Content = _isRandomPlay ? "随机模式" : "顺序模式";
            }

            OnPlayModeChanged?.Invoke(this, _isRandomPlay);
            OnPropertyChanged(nameof(IsRandomPlay));
        }

        public void ToggleLoopMode()
        {
            // 循环切换三种模式：无循环 -> 单曲循环 -> 列表循环
            _currentLoopMode = _currentLoopMode switch
            {
                LoopMode.None => LoopMode.Single,
                LoopMode.Single => LoopMode.List,
                _ => LoopMode.None
            };

            Logger.Information("切换循环模式: {LoopMode}", _currentLoopMode);

            UpdateLoopModeButtonText();

            OnPropertyChanged(nameof(CurrentLoopMode));
            OnPropertyChanged(nameof(IsLoopMode));
            OnPropertyChanged(nameof(IsListLoop));
        }

        private void UpdateLoopModeButtonText()
        {
            if (_loopModeButton != null)
            {
                _loopModeButton.Content = _currentLoopMode switch
                {
                    LoopMode.Single => "循环模式: 单曲",
                    LoopMode.List => "循环模式: 列表",
                    _ => "循环模式: 无"
                };
            }
        }

        public void SetProgress(double value)
        {
            Logger.Debug("设置播放进度: {Progress}%", value);

            if (_mediaElement == null || !_mediaElement.NaturalDuration.HasTimeSpan)
            {
                Logger.Warning("无法设置进度，媒体未加载");
                return;
            }

            // 确保媒体已加载
            if (_mediaElement.NaturalDuration.HasTimeSpan)
            {
                value = Math.Clamp(value, 0, 100);
                double totalSeconds = _mediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                double newPositionSeconds = totalSeconds * (value / 100.0);

                // 设置新位置
                _mediaElement.Position = TimeSpan.FromSeconds(newPositionSeconds);

                // 更新进度属性
                _progress = value;
                OnPropertyChanged(nameof(Progress));
                OnPropertyChanged(nameof(CurrentTime));

                Logger.Debug("播放进度已更新: {NewPosition}", TimeSpan.FromSeconds(newPositionSeconds));
            }
        }

        public void AddMusicFiles(string[] filePaths)
        {
            Logger.Debug("开始添加音乐文件，数量: {FileCount}", filePaths?.Length ?? 0);

            if (filePaths == null || filePaths.Length == 0)
            {
                Logger.Warning("未提供有效的文件路径");
                return;
            }

            int addedCount = 0;
            var supportedExtensions = new[] { ".mp3", ".wav", ".wma", ".flac", ".ogg", ".m4a" };

            foreach (var filePath in filePaths)
            {
                try
                {
                    var extension = Path.GetExtension(filePath).ToLower();
                    if (!supportedExtensions.Contains(extension))
                    {
                        Logger.Debug("跳过不支持的文件格式: {FilePath}", filePath);
                        Debug.WriteLine($"跳过不支持的文件格式: {filePath}");
                        continue;
                    }

                    if (!System.IO.File.Exists(filePath))
                    {
                        Logger.Warning("文件不存在: {FilePath}", filePath);
                        Debug.WriteLine($"文件不存在: {filePath}");
                        continue;
                    }

                    if (MusicFiles.Any(m => m.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        Logger.Debug("文件已存在，跳过: {FilePath}", filePath);
                        continue;
                    }

                    var destPath = Path.Combine(_defaultMusicFolder, Path.GetFileName(filePath));
                    int copyIndex = 1;

                    while (System.IO.File.Exists(destPath))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(filePath);
                        var ext = Path.GetExtension(filePath);
                        destPath = Path.Combine(_defaultMusicFolder, $"{fileName}({copyIndex++}){ext}");
                    }

                    System.IO.File.Copy(filePath, destPath);
                    addedCount++;
                    Logger.Debug("文件已复制: {Source} -> {Destination}", filePath, destPath);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "添加文件失败: {FilePath} - {ErrorMessage}", filePath, ex.Message);
                    Debug.WriteLine($"添加文件失败: {filePath} - {ex.Message}");
                }
            }

            if (addedCount > 0)
            {
                Logger.Information("成功添加 {AddedCount} 首音乐", addedCount);
                RefreshMusicFiles();
                OnPlaybackMessage?.Invoke(this, $"已添加 {addedCount} 首音乐");
            }
            else
            {
                Logger.Warning("未添加任何音乐文件");
            }
        }
        #endregion

        #region 私有方法
        private void HandleMediaEnded()
        {
            Logger.Debug("处理媒体结束事件，当前循环模式: {LoopMode}", _currentLoopMode);

            // 随机模式结束时清除未来记录
            if (_isRandomPlay)
            {
                _playbackFuture.Clear();
                Logger.Debug("随机模式下清空未来记录");
            }

            // 只有在歌曲完整播放时才记录
            // 避免与PlayMusicAtIndex中的记录冲突
            if (CurrentMusic != null && _mediaElement.NaturalDuration.HasTimeSpan)
            {
                TimeSpan totalDuration = _mediaElement.NaturalDuration.TimeSpan;

                // 记录完整播放
                RecordPlay(totalDuration);
            }

            switch (_currentLoopMode)
            {
                case LoopMode.Single:
                    Logger.Debug("单曲循环模式，重新播放当前歌曲");
                    // 单曲循环：重新播放当前歌曲
                    _mediaElement.Position = TimeSpan.Zero;
                    _mediaElement.Play();
                    break;

                case LoopMode.List:
                    Logger.Debug("列表循环模式，播放下一首");
                    // 列表循环：播放下一首
                    PlayNextMusic();
                    break;

                default:
                    Logger.Debug("无循环模式，播放下一首或停止");
                    // 无循环：播放下一首或停止
                    if (_currentMusicIndex < MusicFiles.Count - 1)
                    {
                        PlayNextMusic();
                    }
                    else
                    {
                        Stop();
                        OnPlaybackMessage?.Invoke(this, "播放已结束");
                    }
                    break;
            }
        }

        // 记录播放统计信息
        private async void RecordPlay(TimeSpan duration)
        {
            // 如果正在记录中，则跳过
            if (_isRecordingPlay)
            {
                Logger.Debug("跳过重复记录: 正在记录中");
                return;
            }

            try
            {
                _isRecordingPlay = true; // 设置记录标志

                // 防止短时间内重复记录同一首歌（30秒内不重复记录）
                if (CurrentMusic != null &&
                    CurrentMusic.FilePath == _lastRecordedSong &&
                    (DateTime.Now - _lastRecordedTime).TotalSeconds < 30)
                {
                    Logger.Debug("跳过重复记录: {SongName}", CurrentMusic.DisplayName);
                    return;
                }

                string databasePath = DatabaseHelper.GetDatabasePath();

                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    await connection.OpenAsync();

                    // 更新歌曲的播放统计
                    string updateSongStats = @"
                        INSERT INTO SongPlayStats (FilePath, PlayCount, TotalPlayDuration)
                        VALUES (@FilePath, 1, @Duration)
                        ON CONFLICT(FilePath) DO UPDATE SET
                            PlayCount = PlayCount + 1,
                            TotalPlayDuration = TotalPlayDuration + @Duration;
                    ";

                    using (var command = new SqliteCommand(updateSongStats, connection))
                    {
                        command.Parameters.AddWithValue("@FilePath", CurrentMusic.FilePath);
                        command.Parameters.AddWithValue("@Duration", duration.TotalSeconds);
                        await command.ExecuteNonQueryAsync();
                    }

                    // 更新总播放统计
                    string updateTotalStats = @"
                        UPDATE TotalPlayStats 
                        SET TotalPlayCount = TotalPlayCount + 1,
                            TotalPlayDuration = TotalPlayDuration + @Duration
                        WHERE Id = 1;
                    ";

                    using (var command = new SqliteCommand(updateTotalStats, connection))
                    {
                        command.Parameters.AddWithValue("@Duration", duration.TotalSeconds);
                        await command.ExecuteNonQueryAsync();
                    }
                }

                // 更新最后记录的时间和歌曲
                _lastRecordedTime = DateTime.Now;
                _lastRecordedSong = CurrentMusic.FilePath;

                Logger.Information($"记录播放统计: {CurrentMusic.DisplayName}, 时长: {duration.TotalSeconds}秒");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "记录播放统计失败");
            }
            finally
            {
                _isRecordingPlay = false; // 释放记录标志
            }
        }

        private void SaveLastPlayedMusic(string musicPath)
        {
            try
            {
                Logger.Debug("保存最后播放的音乐: {MusicPath}", musicPath);
                System.IO.File.WriteAllText(_lastPlayedMusicFilePath, musicPath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "保存播放记录失败: {ErrorMessage}", ex.Message);
                Debug.WriteLine($"保存播放记录失败: {ex.Message}");
            }
        }

        private void RestoreLastPlayedMusic()
        {
            try
            {
                Logger.Debug("尝试恢复最后播放的音乐");

                if (System.IO.File.Exists(_lastPlayedMusicFilePath))
                {
                    string lastPlayedMusicPath = System.IO.File.ReadAllText(_lastPlayedMusicFilePath);
                    Logger.Debug("最后播放的音乐路径: {MusicPath}", lastPlayedMusicPath);

                    if (System.IO.File.Exists(lastPlayedMusicPath))
                    {
                        var music = MusicFiles.FirstOrDefault(m =>
                            m.FilePath.Equals(lastPlayedMusicPath, StringComparison.OrdinalIgnoreCase));

                        if (music != null)
                        {
                            int index = MusicFiles.IndexOf(music);
                            _currentMusicIndex = index;
                            _mediaElement.Source = new Uri(lastPlayedMusicPath);

                            Logger.Debug("成功恢复最后播放的音乐: {MusicName}", music.DisplayName);

                            // 通知UI更新
                            UpdateMusicInfoDisplay();
                            OnMusicChanged?.Invoke(this, music);
                        }
                        else
                        {
                            Logger.Warning("最后播放的音乐不在当前列表中");
                        }
                    }
                    else
                    {
                        Logger.Warning("最后播放的音乐文件不存在");
                    }
                }
                else
                {
                    Logger.Debug("未找到最后播放的音乐记录文件");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "恢复播放记录失败: {ErrorMessage}", ex.Message);
                Debug.WriteLine($"恢复播放记录失败: {ex.Message}");
            }
        }

        private void UpdatePlayPauseButtonText()
        {
            if (_playPauseButton != null)
            {
                _playPauseButton.Content = _isPlaying ? "暂停" : "播放";
            }
        }

        private void UpdateMusicInfoDisplay()
        {
            if (CurrentMusic != null && _musicNameTextBox != null)
            {
                _musicNameTextBox.Text = CurrentMusic.DisplayName;
            }
        }
        #endregion

        #region 事件和通知
        public event EventHandler<int> OnMusicListUpdated;
        public event EventHandler<bool> OnPlayModeChanged;
        public event EventHandler<LoopMode> OnLoopModeChanged;
        public event EventHandler<MusicInfo> OnMusicChanged;
        public event EventHandler<string> OnPlaybackError;
        public event EventHandler<string> OnPlaybackMessage;
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<List<LyricManager.LyricLine>> OnLyricsLoaded;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region 资源释放
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                Logger.Information("MusicPlayer 正在释放资源");

                if (_progressTimer != null)
                {
                    _progressTimer.Stop();
                    _progressTimer.Tick -= ProgressTimer_Tick;
                    _progressTimer = null;
                    Logger.Debug("进度定时器已停止");
                }

                if (_mediaElement != null)
                {
                    _mediaElement.MediaOpened -= MediaElement_MediaOpened;
                    _mediaElement.MediaEnded -= MediaElement_MediaEnded;
                    _mediaElement.MediaFailed -= MediaElement_MediaFailed;
                    _mediaElement.Stop();
                    Logger.Debug("媒体元素已清理");
                }

                if (_lyricManager != null)
                {
                    _lyricManager.OnLyricsLoaded -= LyricManager_OnLyricsLoaded;
                    Logger.Debug("歌词管理器已清理");
                }
            }

            _isDisposed = true;
            Logger.Information("MusicPlayer 资源释放完成");
        }

        ~MusicPlayer()
        {
            Dispose(false);
        }
        #endregion

        #region 播放统计方法
        // 获取歌曲播放统计
        public static async Task<Dictionary<string, object>> GetSongPlayStatsAsync(string filePath)
        {
            try
            {
                string databasePath = DatabaseHelper.GetDatabasePath();

                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    await connection.OpenAsync();

                    string query = "SELECT PlayCount, TotalPlayDuration FROM SongPlayStats WHERE FilePath = @FilePath;";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@FilePath", filePath);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new Dictionary<string, object>
                                {
                                    { "PlayCount", reader.GetInt32(0) },
                                    { "TotalPlayDuration", TimeSpan.FromSeconds(reader.GetDouble(1)) }
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "获取歌曲播放统计失败");
            }

            return new Dictionary<string, object>
            {
                { "PlayCount", 0 },
                { "TotalPlayDuration", TimeSpan.Zero }
            };
        }

        // 获取总播放统计
        public static async Task<Dictionary<string, object>> GetTotalPlayStatsAsync()
        {
            try
            {
                string databasePath = DatabaseHelper.GetDatabasePath();

                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    await connection.OpenAsync();

                    string query = "SELECT TotalPlayCount, TotalPlayDuration FROM TotalPlayStats WHERE Id = 1;";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new Dictionary<string, object>
                                {
                                    { "TotalPlayCount", reader.GetInt32(0) },
                                    { "TotalPlayDuration", TimeSpan.FromSeconds(reader.GetDouble(1)) }
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "获取总播放统计失败");
            }

            return new Dictionary<string, object>
            {
                { "TotalPlayCount", 0 },
                { "TotalPlayDuration", TimeSpan.Zero }
            };
        }
        #endregion
    }
}