using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace Software.其他窗口
{
    public class Config
    {
        public string Version { get; set; }
        public string DownloadSize { get; set; }
        public string CurrentVersion { get; set; }
        public string LastUpdate { get; set; }
        public string ReleaseNotes { get; set; }
        public List<UpdateDetail> Details { get; set; }
    }

    public class UpdateDetail
    {
        public string UpdateMode { get; set; }
        public string UpdateURL { get; set; }
        public string FileSize { get; set; }
        public List<Checksum> Checksum { get; set; }
        public bool MandatoryUpdate { get; set; }
    }

    public class Checksum
    {
        public string MD5 { get; set; }
    }

    public class ConfigManager
    {
        public static async Task<Config> LoadConfigFromUrlAsync(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                string json = await client.GetStringAsync(url);
                JObject jObject = JObject.Parse(json);
                return jObject.ToObject<Config>();
            }
        }
    }

    public class UpdateLog
    {
        public string JsonVersion { get; set; }
        public string Version { get; set; }
        public List<UpdateInfo> Updates { get; set; }
    }

    public class UpdateInfo
    {
        public string Version { get; set; }
        public string UpdateTime { get; set; }
        public List<string> UpdateContent { get; set; }
    }

    public class UpdateLogManager
    {
        public static async Task<UpdateLog> LoadUpdateLogFromUrlAsync(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                string json = await client.GetStringAsync(url);
                JObject jObject = JObject.Parse(json);
                return jObject.ToObject<UpdateLog>();
            }
        }
    }

    public partial class WindowUpdate : Window
    {
        private Stopwatch stopwatch = new Stopwatch();
        private Config config;

        public WindowUpdate()
        {
            InitializeComponent();
            LoadUpdateInfo();
            LoadUpdateLog();
        }

        private async void LoadUpdateInfo()
        {
            UpdateModeComboBox.IsEnabled = false;
            string configUrl = "https://gitee.com/nibadianbanxiaban/software/releases/download/resources/new_update.json";
            config = await ConfigManager.LoadConfigFromUrlAsync(configUrl);
            if (config != null)
            {
                VersionInfo.Text = config.Version;
                DownloadSize.Text = config.DownloadSize;
                CurrentVersion.Text = config.CurrentVersion;
                LastUpdate.Text = config.LastUpdate;
                ReleaseNotes.Text = config.ReleaseNotes;
                foreach (UpdateDetail detail in config.Details)
                {
                    UpdateModeComboBox.Items.Add(detail.UpdateMode);
                }
                UpdateModeComboBox.IsEnabled = true;
            }
        }

        private async void LoadUpdateLog()
        {
            string updateLogUrl = "https://gitee.com/nibadianbanxiaban/software/releases/download/resources/update_log.json"; // 替换为你的 update_log.json 文件 URL
            UpdateLog updateLog = await UpdateLogManager.LoadUpdateLogFromUrlAsync(updateLogUrl);

            // 显示所有版本的更新内容
            var allUpdates = updateLog.Updates;
            if (allUpdates != null)
            {
                ReleaseNotes.Text = string.Join("\n\n", allUpdates.Select(update => $"版本 {update.Version} ({update.UpdateTime}):\n{string.Join("\n", update.UpdateContent)}"));
            }
        }

        private void UpdateModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 处理更新方式选择更改事件
            if (UpdateModeComboBox.SelectedItem != null && config != null)
            {
                string selectedUpdateMode = UpdateModeComboBox.SelectedItem.ToString();
                var selectedDetail = config.Details.FirstOrDefault(detail => detail.UpdateMode == selectedUpdateMode);
                if (selectedDetail != null)
                {
                    DownloadSize.Text = selectedDetail.FileSize;
                }
            }
        }

        private async void StartUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (config != null && UpdateModeComboBox.SelectedItem != null)
            {
                string selectedUpdateMode = UpdateModeComboBox.SelectedItem.ToString();
                var selectedDetail = config.Details.FirstOrDefault(detail => detail.UpdateMode == selectedUpdateMode);
                if (selectedDetail != null)
                {
                    // 关闭软件
                    CloseSoftware();

                    string updateUrl = selectedDetail.UpdateURL;
                    string filePath = await DownloadUpdate(updateUrl);

                    // 下载完成后，保存文件路径以便安装按钮使用
                    Application.Current.Properties["DownloadedFilePath"] = filePath;

                    MessageBox.Show("下载完成，请点击安装按钮进行安装。");
                }
            }
        }

        private async Task<string> DownloadUpdate(string url)
        {
            string rootPath = AppDomain.CurrentDomain.BaseDirectory;
            string updateFolderPath = Path.Combine(rootPath, "update");

            if (!Directory.Exists(updateFolderPath))
            {
                Directory.CreateDirectory(updateFolderPath);
            }

            string filePath = Path.Combine(updateFolderPath, Path.GetFileName(url));
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(30);
                HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? 0;
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[8192];
                    long totalReadBytes = 0;
                    int readBytes;

                    stopwatch.Start();
                    while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        totalReadBytes += readBytes;
                        await fs.WriteAsync(buffer, 0, readBytes);
                        double progress = (double)totalReadBytes / totalBytes * 100;
                        UpdateProgressBar((int)progress, totalReadBytes, totalBytes);
                    }
                    stopwatch.Stop();
                }
            }

            MessageBox.Show($"下载完成！文件路径: {filePath}");
            await Task.Delay(1000); // 添加延迟
            return filePath;
        }

        private void CloseSoftware()
        {
            // 获取当前进程的名称
            string processName = Process.GetCurrentProcess().ProcessName;

            // 查找所有与当前进程名称相同的进程
            foreach (var process in Process.GetProcessesByName(processName))
            {
                if (process.Id != Process.GetCurrentProcess().Id)
                {
                    process.Kill(); // 关闭其他进程
                }
            }
        }

        private void ExtractAndOpen(string filePath)
        {
            string extractPath = AppDomain.CurrentDomain.BaseDirectory;
            using (ZipArchive archive = ZipFile.OpenRead(filePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.Combine(extractPath, entry.FullName);
                    if (File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath); // 删除已存在的文件
                    }
                    entry.ExtractToFile(destinationPath, true); // 解压并覆盖现有文件
                }
            }
            MessageBox.Show($"解压完成！解压路径: {extractPath}");
            Process.Start(new ProcessStartInfo
            {
                FileName = extractPath,
                UseShellExecute = true
            });
        }

        private void InstallPackage(string filePath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }

        private void InstallUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.Properties["DownloadedFilePath"] != null)
            {
                string filePath = Application.Current.Properties["DownloadedFilePath"].ToString();
                string selectedUpdateMode = UpdateModeComboBox.SelectedItem.ToString();

                MessageBox.Show($"安装模式: {selectedUpdateMode}\n文件路径: {filePath}");

                if (selectedUpdateMode.Contains("zip"))
                {
                    MessageBox.Show("开始解压...");
                    ExtractAndOpen(filePath);
                }
                else if (selectedUpdateMode.Contains("installer"))
                {
                    MessageBox.Show("开始安装...");
                    InstallPackage(filePath);
                }
                else
                {
                    MessageBox.Show("未知的更新模式。");
                }
            }
            else
            {
                MessageBox.Show("请先下载更新。");
            }
        }

        private void UpdateProgressBar(int value, long downloadedBytes, long totalBytes)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = value;
                ProgressPercentage.Text = $"{value}%";
                double downloadedMB = downloadedBytes / (1024.0 * 1024.0);
                double totalMB = totalBytes / (1024.0 * 1024.0);
                DownloadProgress.Text = $"{downloadedMB:F2} MB / {totalMB:F2} MB";

                // 计算下载速度
                double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                double downloadSpeed = downloadedMB / elapsedSeconds;
                DownloadSpeed.Text = $"下载速度: {downloadSpeed:F2} MB/s";
            });
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}