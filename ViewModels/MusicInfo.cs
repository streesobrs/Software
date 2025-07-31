using System;
using System.Windows.Media.Imaging;

namespace Software.ViewModels
{
    public class MusicInfo
    {
        public int Index { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string DisplayName { get; set; }
        public string Folder { get; set; }
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;
        public string Artist { get; set; } = "未知艺术家";
        public string Album { get; set; } = "未知专辑";
        public BitmapImage AlbumCover { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}