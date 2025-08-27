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

namespace Software.ViewModels
{
    public class MusicPlayer : INotifyPropertyChanged, IDisposable
    {
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
        }

        public MusicPlayer(MediaElement mediaElement, TextBlock musicNameTextBox, Button playPauseButton)
            : this(mediaElement, musicNameTextBox, playPauseButton, null, null)
        { }
        #endregion

        #region 初始化
        private void Initialize()
        {
            if (!Directory.Exists(_defaultMusicFolder))
            {
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
        }
        #endregion

        #region 事件处理
        private void MediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            _progressTimer.Start();
            OnPropertyChanged(nameof(TotalTime));

            // 更新当前播放音乐的时长
            if (CurrentMusic != null && _mediaElement.NaturalDuration.HasTimeSpan)
            {
                TimeSpan duration = _mediaElement.NaturalDuration.TimeSpan;
                CurrentMusic.Duration = duration;
                OnPropertyChanged(nameof(CurrentMusic)); // 通知UI更新

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
            OnLyricsLoaded?.Invoke(this, e);
        }

        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            HandleMediaEnded();
        }

        private void MediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            string errorMessage = $"播放失败: {e.ErrorException.Message}";

            // 添加额外错误诊断信息
            if (e.ErrorException is InvalidOperationException)
            {
                errorMessage += "\n可能原因：";
                errorMessage += "\n- 文件格式不受支持";
                errorMessage += "\n- 文件已损坏";
                errorMessage += "\n- 缺少必要的解码器";
            }

            // 清除歌词
            _lyricManager?.ClearLyrics();

            Debug.WriteLine(errorMessage);
            OnPlaybackError?.Invoke(this, errorMessage);

            if (MusicFiles.Count > 0)
            {
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
            try
            {
                MusicFiles.Clear();
                _playbackHistory.Clear();
                _playbackFuture.Clear();

                var supportedExtensions = new[] { ".mp3", ".wav", ".wma", ".flac", ".ogg", ".m4a" };
                var files = Directory.GetFiles(_defaultMusicFolder, "*.*", SearchOption.AllDirectories)
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLower()));

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

                OnMusicListUpdated?.Invoke(this, MusicFiles.Count);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刷新音乐文件失败: {ex.Message}");
                OnPlaybackError?.Invoke(this, $"刷新音乐列表失败: {ex.Message}");
            }
        }

        // 加载音乐元数据（艺术家、专辑、封面、时长等）
        private void LoadMusicMetadata(MusicInfo musicInfo)
        {
            try
            {
                using (var file = TagLib.File.Create(musicInfo.FilePath))
                {
                    // 读取艺术家信息
                    if (!string.IsNullOrWhiteSpace(file.Tag.FirstPerformer))
                    {
                        musicInfo.Artist = file.Tag.FirstPerformer;
                    }

                    // 读取专辑信息
                    if (!string.IsNullOrWhiteSpace(file.Tag.Album))
                    {
                        musicInfo.Album = file.Tag.Album;
                    }

                    // 读取时长
                    if (file.Properties != null && file.Properties.Duration.TotalSeconds > 0)
                    {
                        musicInfo.Duration = file.Properties.Duration;
                    }
                    else
                    {
                        // 尝试通过文件大小估算时长
                        var fileInfo = new FileInfo(musicInfo.FilePath);
                        double sizeMB = fileInfo.Length / (1024.0 * 1024.0);
                        musicInfo.Duration = TimeSpan.FromMinutes(sizeMB * 0.1); // 估算：1MB ≈ 0.1分钟
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
                    }
                }
            }
            catch (Exception ex)
            {
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
            }
        }

        // 加载默认专辑封面
        public BitmapImage LoadDefaultAlbumCover()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Resources/default_album.png");
                return new BitmapImage(uri);
            }
            catch
            {
                return null;
            }
        }

        public void PlayPause()
        {
            if (MusicFiles.Count == 0)
            {
                OnPlaybackError?.Invoke(this, "没有找到音乐文件");
                return;
            }

            if (_mediaElement.Source == null)
            {
                PlayMusicAtIndex(0);
                return;
            }

            if (_isPlaying)
            {
                _mediaElement.Pause();
            }
            else
            {
                _mediaElement.Play();
            }

            _isPlaying = !_isPlaying;
            UpdatePlayPauseButtonText();
        }

        public void Stop()
        {
            _mediaElement.Stop();
            _isPlaying = false;
            UpdatePlayPauseButtonText();
            _progressTimer.Stop();
            _progress = 0;
            OnPropertyChanged(nameof(Progress));
        }

        public void PlayRandomMusic()
        {
            if (MusicFiles.Count == 0)
            {
                OnPlaybackError?.Invoke(this, "没有找到音乐文件");
                return;
            }

            if (MusicFiles.Count == 1)
            {
                PlayMusicAtIndex(0);
                return;
            }

            int newIndex;
            do
            {
                newIndex = _random.Next(MusicFiles.Count);
            } while (newIndex == _currentMusicIndex && MusicFiles.Count > 1);

            PlayMusicAtIndex(newIndex);
        }

        public void PlayNextMusic()
        {
            if (MusicFiles.Count == 0)
            {
                OnPlaybackError?.Invoke(this, "没有找到音乐文件");
                return;
            }

            // 随机模式且存在"未来"记录
            if (_isRandomPlay && _playbackFuture.Count > 0)
            {
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
                    PlayRandomMusic();
                }
                else
                {
                    int nextIndex = _currentMusicIndex + 1;
                    if (nextIndex >= MusicFiles.Count)
                    {
                        nextIndex = _currentLoopMode == LoopMode.List ? 0 : MusicFiles.Count - 1;
                    }
                    PlayMusicAtIndex(nextIndex);
                }
            }
        }

        public void PlayPreviousMusic()
        {
            if (MusicFiles.Count == 0)
            {
                OnPlaybackError?.Invoke(this, "没有找到音乐文件");
                return;
            }

            // 随机模式：使用历史记录回溯
            if (_isRandomPlay && _playbackHistory.Count > 0)
            {
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

                PlayMusicAtIndex(prevIndex);
            }
        }

        public void PlayMusicAtIndex(int index, bool isFromHistoryOrFuture = false)
        {
            if (index < 0 || index >= MusicFiles.Count)
                return;

            try
            {
                // 停止当前播放
                _mediaElement.Stop();

                // 记录当前歌曲到历史（非历史/未来触发的播放）
                if (!isFromHistoryOrFuture && CurrentMusic != null)
                {
                    // 随机模式下记录完整历史
                    if (_isRandomPlay)
                    {
                        _playbackHistory.Push(CurrentMusic);
                    }
                    // 顺序模式只记录当前歌曲
                    else
                    {
                        // 清空历史只保留最新记录
                        _playbackHistory.Clear();
                        _playbackHistory.Push(CurrentMusic);
                    }
                    _playbackFuture.Clear();
                }

                _currentMusicIndex = index;
                var selectedMusic = MusicFiles[index];

                // 加载歌词
                if (selectedMusic != null)
                {
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
            }
            catch (Exception ex)
            {
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

            // 切换模式时清空历史记录
            _playbackHistory.Clear();
            _playbackFuture.Clear();

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
            if (_mediaElement == null || !_mediaElement.NaturalDuration.HasTimeSpan)
                return;

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
            }
        }

        public void AddMusicFiles(string[] filePaths)
        {
            if (filePaths == null || filePaths.Length == 0)
                return;

            int addedCount = 0;
            var supportedExtensions = new[] { ".mp3", ".wav", ".wma", ".flac", ".ogg", ".m4a" };

            foreach (var filePath in filePaths)
            {
                try
                {
                    var extension = Path.GetExtension(filePath).ToLower();
                    if (!supportedExtensions.Contains(extension))
                    {
                        Debug.WriteLine($"跳过不支持的文件格式: {filePath}");
                        continue;
                    }

                    if (!System.IO.File.Exists(filePath))
                    {
                        Debug.WriteLine($"文件不存在: {filePath}");
                        continue;
                    }

                    if (MusicFiles.Any(m => m.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                    {
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
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"添加文件失败: {filePath} - {ex.Message}");
                }
            }

            if (addedCount > 0)
            {
                RefreshMusicFiles();
                OnPlaybackMessage?.Invoke(this, $"已添加 {addedCount} 首音乐");
            }
        }
        #endregion

        #region 私有方法
        private void HandleMediaEnded()
        {
            // 随机模式结束时清除未来记录
            if (_isRandomPlay)
            {
                _playbackFuture.Clear();
            }

            switch (_currentLoopMode)
            {
                case LoopMode.Single:
                    // 单曲循环：重新播放当前歌曲
                    _mediaElement.Position = TimeSpan.Zero;
                    _mediaElement.Play();
                    break;

                case LoopMode.List:
                    // 列表循环：播放下一首
                    PlayNextMusic();
                    break;

                default:
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

        private void SaveLastPlayedMusic(string musicPath)
        {
            try
            {
                System.IO.File.WriteAllText(_lastPlayedMusicFilePath, musicPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存播放记录失败: {ex.Message}");
            }
        }

        private void RestoreLastPlayedMusic()
        {
            try
            {
                if (System.IO.File.Exists(_lastPlayedMusicFilePath))
                {
                    string lastPlayedMusicPath = System.IO.File.ReadAllText(_lastPlayedMusicFilePath);
                    if (System.IO.File.Exists(lastPlayedMusicPath))
                    {
                        var music = MusicFiles.FirstOrDefault(m =>
                            m.FilePath.Equals(lastPlayedMusicPath, StringComparison.OrdinalIgnoreCase));

                        if (music != null)
                        {
                            int index = MusicFiles.IndexOf(music);
                            _currentMusicIndex = index;
                            _mediaElement.Source = new Uri(lastPlayedMusicPath);

                            // 通知UI更新
                            UpdateMusicInfoDisplay();
                            OnMusicChanged?.Invoke(this, music);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
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
                if (_progressTimer != null)
                {
                    _progressTimer.Stop();
                    _progressTimer.Tick -= ProgressTimer_Tick;
                    _progressTimer = null;
                }

                if (_mediaElement != null)
                {
                    _mediaElement.MediaOpened -= MediaElement_MediaOpened;
                    _mediaElement.MediaEnded -= MediaElement_MediaEnded;
                    _mediaElement.MediaFailed -= MediaElement_MediaFailed;
                    _mediaElement.Stop();
                }
            }

            _isDisposed = true;
        }

        ~MusicPlayer()
        {
            Dispose(false);
        }
        #endregion
    }
}