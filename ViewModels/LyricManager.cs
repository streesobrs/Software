using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using Serilog; // 添加日志命名空间

namespace Software.ViewModels
{
    public class LyricManager
    {
        private ILogger _logger;

        public ILogger Logger
        {
            get
            {
                if (_logger == null)
                {
                    _logger = Log.ForContext<LyricManager>();
                }
                return _logger;
            }
        }

        public class LyricLine
        {
            public TimeSpan Time { get; set; }
            public string Text { get; set; }
            public bool IsCurrent { get; set; }
        }

        private List<LyricLine> _currentLyrics = new List<LyricLine>();
        private int _currentLyricIndex = -1;
        private readonly Dispatcher _dispatcher;

        // 添加公共属性访问私有字段
        public List<LyricLine> CurrentLyrics => _currentLyrics;
        public int CurrentLyricIndex => _currentLyricIndex;

        public LyricManager(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            Logger.Information("LyricManager 初始化完成");
        }

        public void ClearLyrics()
        {
            _currentLyrics = new List<LyricLine>();
            _currentLyricIndex = -1;
            Logger.Debug("歌词已清空");
        }

        // 解析LRC歌词
        public List<LyricLine> ParseLyrics(string lrcContent)
        {
            Logger.Debug("开始解析歌词内容");
            var lyrics = new List<LyricLine>();
            var regex = new Regex(@"\[(\d+):(\d+)\.(\d+)\](.*)");

            foreach (var line in lrcContent.Split('\n'))
            {
                var match = regex.Match(line);
                if (match.Success)
                {
                    var min = int.Parse(match.Groups[1].Value);
                    var sec = int.Parse(match.Groups[2].Value);
                    var ms = int.Parse(match.Groups[3].Value.PadRight(3, '0').Substring(0, 3));
                    var time = new TimeSpan(0, 0, min, sec, ms);
                    var text = match.Groups[4].Value.Trim();

                    lyrics.Add(new LyricLine { Time = time, Text = text });
                }
            }

            Logger.Information("歌词解析完成，共解析 {LyricCount} 行歌词", lyrics.Count);
            return lyrics.OrderBy(l => l.Time).ToList();
        }

        // 加载歌词（本地+在线）
        public async Task LoadLyricsAsync(string musicFilePath, string title, string artist)
        {
            Logger.Information("开始加载歌词，歌曲: {Title} - {Artist}", title, artist);

            try
            {
                // 1. 尝试本地歌词文件
                string lyrics = GetLocalLyrics(musicFilePath);
                if (!string.IsNullOrEmpty(lyrics))
                {
                    Logger.Information("找到本地歌词文件");
                    _currentLyrics = ParseLyrics(lyrics);
                    OnLyricsLoaded?.Invoke(this, _currentLyrics);
                    return;
                }

                Logger.Debug("未找到本地歌词文件，尝试在线获取");

                // 2. 尝试在线获取
                lyrics = await GetOnlineLyrics(title, artist);
                if (!string.IsNullOrEmpty(lyrics))
                {
                    Logger.Information("成功获取在线歌词");
                    _currentLyrics = ParseLyrics(lyrics);
                    OnLyricsLoaded?.Invoke(this, _currentLyrics);

                    // 保存到本地
                    SaveLyricsLocally(musicFilePath, lyrics);
                    Logger.Debug("歌词已保存到本地");
                }
                else
                {
                    Logger.Warning("在线获取歌词失败");
                    _currentLyrics = new List<LyricLine>();
                    OnLyricsLoaded?.Invoke(this, _currentLyrics);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "歌词加载失败: {ErrorMessage}", ex.Message);
                Debug.WriteLine($"歌词加载失败: {ex.Message}");
                _currentLyrics = new List<LyricLine>();
                OnLyricsLoaded?.Invoke(this, _currentLyrics);
            }
        }

        // 获取本地歌词
        private string GetLocalLyrics(string musicFilePath)
        {
            try
            {
                string lrcPath = Path.ChangeExtension(musicFilePath, ".lrc");
                if (File.Exists(lrcPath))
                {
                    Logger.Debug("找到本地歌词文件: {LrcPath}", lrcPath);
                    return File.ReadAllText(lrcPath, Encoding.UTF8);
                }

                Logger.Debug("未找到本地歌词文件: {LrcPath}", lrcPath);
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "读取本地歌词文件失败: {ErrorMessage}", ex.Message);
                return null;
            }
        }

        // 保存歌词到本地
        private void SaveLyricsLocally(string musicFilePath, string lyrics)
        {
            try
            {
                string lrcPath = Path.ChangeExtension(musicFilePath, ".lrc");
                File.WriteAllText(lrcPath, lyrics, Encoding.UTF8);
                Logger.Information("歌词已保存到本地: {LrcPath}", lrcPath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "保存歌词到本地失败: {ErrorMessage}", ex.Message);
                /* 忽略保存错误 */
            }
        }

        // 在线获取歌词（网易云音乐API）
        private async Task<string> GetOnlineLyrics(string title, string artist)
        {
            Logger.Debug("开始在线获取歌词: {Title} - {Artist}", title, artist);

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // 设置超时
                    client.Timeout = TimeSpan.FromSeconds(5);

                    // 搜索歌曲
                    string searchUrl = $"https://music.163.com/api/search/get?s={Uri.EscapeDataString(title + " " + artist)}&type=1&limit=1";
                    Logger.Debug("发送搜索请求: {SearchUrl}", searchUrl);

                    string searchResult = await client.GetStringAsync(searchUrl);

                    dynamic searchData = Newtonsoft.Json.JsonConvert.DeserializeObject(searchResult);
                    long songId = searchData?.result?.songs?[0]?.id ?? 0;

                    if (songId == 0)
                    {
                        Logger.Warning("未找到匹配的歌曲: {Title} - {Artist}", title, artist);
                        return null;
                    }

                    Logger.Debug("找到歌曲ID: {SongId}", songId);

                    // 获取歌词
                    string lyricUrl = $"http://music.163.com/api/song/lyric?id={songId}&lv=1";
                    Logger.Debug("发送歌词请求: {LyricUrl}", lyricUrl);

                    string lyricResult = await client.GetStringAsync(lyricUrl);

                    dynamic lyricData = Newtonsoft.Json.JsonConvert.DeserializeObject(lyricResult);
                    return lyricData?.lrc?.lyric;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "在线获取歌词失败: {ErrorMessage}", ex.Message);
                return null;
            }
        }

        // 更新歌词位置
        public void UpdateLyricsPosition(TimeSpan position)
        {
            if (_currentLyrics == null || _currentLyrics.Count == 0) return;

            int newIndex = -1;
            for (int i = 0; i < _currentLyrics.Count; i++)
            {
                if (_currentLyrics[i].Time <= position)
                    newIndex = i;
                else
                    break;
            }

            if (newIndex != _currentLyricIndex && newIndex >= 0)
            {
                _currentLyricIndex = newIndex;
                OnCurrentLyricChanged?.Invoke(this, newIndex);
                Logger.Debug("歌词位置更新: 索引 {LyricIndex}, 时间 {Position}", newIndex, position);
            }
        }

        // 事件
        public event EventHandler<List<LyricLine>> OnLyricsLoaded;
        public event EventHandler<int> OnCurrentLyricChanged;
    }
}