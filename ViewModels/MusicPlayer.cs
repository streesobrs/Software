using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Software.ViewModels
{
    public class MusicPlayer
    {
        private MediaElement mediaElement;
        private string[] musicFiles;
        private bool isPlaying = false;
        private TextBox musicNameTextBox;
        private Button playPauseButton;

        // 保存和读取上次播放的音乐路径的文件路径
        private string lastPlayedMusicFilePath = AppDomain.CurrentDomain.BaseDirectory + "resources\\LastPlayedMusic.ini";

        public MusicPlayer(MediaElement mediaElement, TextBox musicNameTextBox, Button playPauseButton, Slider volumeSlider, ToggleButton bgm)
        {
            this.mediaElement = mediaElement;
            this.musicNameTextBox = musicNameTextBox;
            this.playPauseButton = playPauseButton;

            RefreshMusicFiles();

            // 在游戏启动时尝试恢复上次播放的音乐
            RestoreLastPlayedMusic();
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
            string selectedMusicFile = musicFiles[random.Next(musicFiles.Length)];
            mediaElement.Source = new Uri(selectedMusicFile);
            mediaElement.Play();
            isPlaying = true;

            // 更新音乐名称文本框
            var musicName = System.IO.Path.GetFileName(mediaElement.Source.LocalPath);
            this.musicNameTextBox.Text = musicName;

            // 更新播放暂停按钮的文本
            playPauseButton.Content = "暂停";

            // 更新音乐名称文本框的工具提示
            this.musicNameTextBox.ToolTip = selectedMusicFile; // 显示音乐文件的完整路径

            // 在播放音乐时保存当前播放的音乐路径
            SaveLastPlayedMusic(selectedMusicFile);
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
                    mediaElement.Source = new Uri(lastPlayedMusicPath);

                    // 更新音乐名称文本框
                    var musicName = System.IO.Path.GetFileName(mediaElement.Source.LocalPath);
                    this.musicNameTextBox.Text = musicName;

                    // 更新播放暂停按钮的文本
                    playPauseButton.Content = "播放";

                    // 更新音乐名称文本框的工具提示
                    this.musicNameTextBox.ToolTip = lastPlayedMusicPath; // 显示音乐文件的完整路径
                }
            }
        }
    }
}
