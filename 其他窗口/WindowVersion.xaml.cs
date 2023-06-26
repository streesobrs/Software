using System;
using System.IO;
using System.Windows;
using System.Windows.Navigation;

namespace Software.其他窗口
{
    /// <summary>
    /// WindowEdition.xaml 的交互逻辑
    /// </summary>
    public partial class WindowVersion : Window
    {
        public WindowVersion()
        {
            InitializeComponent();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            NavigationWindow window = new()
            {
                Source = new Uri("其他界面/PageDehURL.xaml", UriKind.Relative)
            };
            window.Show();
        }

        private void Button_Click_delete_file(object sender, RoutedEventArgs e)
        {
            string filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Delete_file.bat");
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                MessageBox.Show("删除成功", "删除文件");
            }
            else
            {
                MessageBox.Show("根目录下没有名为“Delete_file.bat”的文件/你删过了", "删除文件");
            }
        }

        private void Button_Click_win_x64(object sender, RoutedEventArgs e)
        {
            string folderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "win-x64");
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
                MessageBox.Show("删除成功", "删除文件");
            }
            else
            {
                MessageBox.Show("根目录下没有名为“win-x64”的文件夹/你删过了", "删除文件");
            }
        }
    }
}
