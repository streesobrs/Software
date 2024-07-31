using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Wpf.Ui.Controls;

namespace Software.ViewModels
{
    public class MusicPlayer
    {
        private MediaElement mediaElement;
        private string[] musicFiles;
        private bool isPlaying = false;
        private System.Windows.Controls.TextBox musicNameTextBox;
        private System.Windows.Controls.Button playPauseButton;
        private System.Windows.Controls.Button playModeButton;
        private ToggleSwitch playModeToggleSwitch;
        private ToggleSwitch loopModeToggleSwitch;
        private int currentMusicIndex = 0; // 当前音乐的索引
        private bool isRandomPlay = true; // 初始为随机播放
        private bool isLoopMode = false; // 初始为非单曲循环

        // 保存和读取上次播放的音乐路径的文件路径
        private string lastPlayedMusicFilePath = AppDomain.CurrentDomain.BaseDirectory + "resources\\LastPlayedMusic.ini";
        private System.Windows.Controls.TextBox music_name;

        // 完整的构造函数
        public MusicPlayer(MediaElement mediaElement, System.Windows.Controls.TextBox musicNameTextBox, System.Windows.Controls.Button playPauseButton, System.Windows.Controls.Button playModeButton, ToggleSwitch playModeToggleSwitch, ToggleSwitch loopModeToggleSwitch)
        {
            this.mediaElement = mediaElement;
            this.musicNameTextBox = musicNameTextBox;
            this.playPauseButton = playPauseButton;
            this.playModeButton = playModeButton;
            this.playModeToggleSwitch = playModeToggleSwitch;
            this.loopModeToggleSwitch = loopModeToggleSwitch;

            Initialize();
        }

        // 简化的构造函数
        public MusicPlayer(MediaElement mediaElement, System.Windows.Controls.TextBox musicNameTextBox, System.Windows.Controls.Button playPauseButton)
        {
            this.mediaElement = mediaElement;
            this.musicNameTextBox = musicNameTextBox;
            this.playPauseButton = playPauseButton;

            Initialize();
        }

        private void Initialize()
        {
            RefreshMusicFiles();
            RestoreLastPlayedMusic();
            mediaElement.MediaEnded += Media_Ended;
        }

        public MusicPlayer(MediaElement mediaElement, System.Windows.Controls.TextBox music_name, Wpf.Ui.Controls.Button playPauseButton, ToggleSwitch playModeToggleSwitch, ToggleSwitch loopModeToggleSwitch)
        {
            this.mediaElement = mediaElement;
            this.music_name = music_name;
            this.playPauseButton = playPauseButton;
            this.playModeToggleSwitch = playModeToggleSwitch;
            this.loopModeToggleSwitch = loopModeToggleSwitch;
        }

        // 刷新音乐文件列表
        public void RefreshMusicFiles()
        {
            musicFiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "resources\\sound\\music", "*.*", SearchOption.AllDirectories);
        }

        // 播放或暂停音乐
        public void PlayPause()
        {
            if (mediaElement.Source == null)
            {
                PlayRandomMusic();
            }
            else if (isPlaying)
            {
                mediaElement.Pause();
                playPauseButton.Content = "播放";
                isPlaying = false;
            }
            else
            {
                mediaElement.Play();
                playPauseButton.Content = "暂停";
                isPlaying = true;
            }
        }

        // 停止播放音乐
        public void Stop()
        {
            mediaElement.Stop();
            playPauseButton.Content = "播放";
            isPlaying = false;
        }

        // 随机播放一首音乐
        public void PlayRandomMusic()
        {
            Random random = new Random();
            PlayMusicAtIndex(random.Next(musicFiles.Length));
        }

        // 顺序播放下一首音乐
        public void PlayNextMusic()
        {
            currentMusicIndex++;
            if (currentMusicIndex >= musicFiles.Length)
            {
                currentMusicIndex = 0; // 回到第一首音乐
            }
            PlayMusicAtIndex(currentMusicIndex);
        }

        // 播放指定索引的音乐
        private void PlayMusicAtIndex(int index, bool autoPlay = true)
        {
            string selectedMusicFile = musicFiles[index];
            mediaElement.Source = new Uri(selectedMusicFile);

            if (autoPlay)
            {
                mediaElement.Play();
                isPlaying = true;
                playPauseButton.Content = "暂停";
            }
            else
            {
                isPlaying = false;
                playPauseButton.Content = "播放";
            }

            // 更新音乐名称文本框
            var musicName = System.IO.Path.GetFileName(mediaElement.Source.LocalPath);
            this.musicNameTextBox.Text = musicName;

            // 更新音乐名称文本框的工具提示
            this.musicNameTextBox.ToolTip = selectedMusicFile; // 显示音乐文件的完整路径

            // 在播放音乐时保存当前播放的音乐路径
            SaveLastPlayedMusic(selectedMusicFile);
        }

        // MediaEnded 事件处理程序：单曲循环
        private void Media_Ended(object sender, EventArgs e)
        {
            // 重置音乐的播放位置到起始位置
            mediaElement.Position = TimeSpan.Zero;
            mediaElement.Play();
        }

        // 保存上次播放的音乐路径
        private void SaveLastPlayedMusic(string musicPath)
        {
            File.WriteAllText(lastPlayedMusicFilePath, musicPath);
        }

        // 恢复上次播放的音乐
        private void RestoreLastPlayedMusic()
        {
            if (File.Exists(lastPlayedMusicFilePath))
            {
                string lastPlayedMusicPath = File.ReadAllText(lastPlayedMusicFilePath);
                if (File.Exists(lastPlayedMusicPath))
                {
                    PlayMusicAtIndex(Array.IndexOf(musicFiles, lastPlayedMusicPath), false);
                }
            }
        }

        // 切换播放模式
        public void ToggleRandomOrSequential()
        {
            isRandomPlay = !isRandomPlay;
            if (isRandomPlay)
            {
                playModeButton.Content = "随机";
            }
            else
            {
                playModeButton.Content = "顺序";
            }
        }

        // 切换单曲循环模式
        public void ToggleLoopMode()
        {
            isLoopMode = !isLoopMode;
            if (isLoopMode)
            {
                mediaElement.MediaEnded += Media_Ended_Loop;
                Debug.Write("Loop mode enabled");
            }
            else
            {
                mediaElement.MediaEnded -= Media_Ended_Loop;
                Debug.Write("Loop mode disabled");
            }
        }

        // MediaEnded 事件处理程序：单曲循环
        private void Media_Ended_Loop(object sender, EventArgs e)
        {
            mediaElement.Position = TimeSpan.Zero;
            mediaElement.Play();
        }

        // 获取当前播放的音乐名称
        public string GetCurrentMusicName()
        {
            return System.IO.Path.GetFileName(mediaElement.Source.LocalPath);
        }

        // 获取当前播放的音乐路径
        public string GetCurrentMusicPath()
        {
            return mediaElement.Source.LocalPath;
        }

    }
}
