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
using System.Configuration;
using Software.Models;
using Microsoft.Data.Sqlite;
using Serilog;

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

        private string newUpdatePath;
        private string JsonUrl;

        private ILogger logger;

        public ILogger MyLoger
        {
            get
            {
                if (logger == null)
                {
                    logger = Log.ForContext<MainWindow>();
                }
                return logger;
            }
        }

        string databasePath = DatabaseHelper.GetDatabasePath();

        public WindowUpdate()
        {
            InitializeComponent();

            // 从数据库读取配置文件
            newUpdatePath = GetConfigValueFromDatabase(databasePath, "newUpdatePath");
            JsonUrl = GetConfigValueFromDatabase(databasePath, "UpdateLogUrl");

            LoadUpdateInfo();
        }

        private string GetConfigValueFromDatabase(string dbPath, string key)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    string query = "SELECT Value FROM Settings WHERE Key = @Key;";
                    var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@Key", key);
                    return command.ExecuteScalar()?.ToString();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("读取配置值时发生错误:{error}", ex.ToString());
                MessageBox.Show("读取配置值时发生错误: " + ex.Message);
                return null;
            }
        }

        private async void LoadUpdateInfo()
        {
            UpdateModeComboBox.IsEnabled = false;
            string configUrl = newUpdatePath;
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

                // 获取当前正在执行的程序集
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                // 获取版本信息
                System.Version version = assembly.GetName().Version;
                // 将版本信息转换为字符串
                string versionString = version.ToString();
                CurrentVersion.Text = versionString;

                // 从数据库读取并显示上次更新时间
                LastUpdate.Text = LastUpdateTime();

                // 保存当前时间为上次更新时间
                SaveLastUpdateTime(databasePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                // 加载更新日志
                await LoadUpdateLog();
            }
        }

        private void SaveLastUpdateTime(string dbPath, string lastUpdateTime)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    string query = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('LastUpdateTime', @LastUpdateTime);";
                    var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@LastUpdateTime", lastUpdateTime);
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("保存LastUpdateTime时发生错误:{error}", ex.ToString());
                MessageBox.Show("保存LastUpdateTime时发生错误: " + ex.Message);
            }
        }

        private string LastUpdateTime()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();
                    string query = "SELECT Value FROM Settings WHERE Key = 'LastUpdateTime';";
                    var command = new SqliteCommand(query, connection);
                    return command.ExecuteScalar()?.ToString();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("读取LastUpdateTime时发生错误:{error}", ex.ToString());
                MessageBox.Show("读取LastUpdateTime时发生错误: " + ex.Message);
                return null;
            }
        }


        private async Task LoadUpdateLog()
        {
            try
            {
                UpdateStatus("加载更新日志...");
                string updateLogUrl = JsonUrl;
                UpdateLog updateLog = await UpdateLogManager.LoadUpdateLogFromUrlAsync(updateLogUrl);

                // 获取当前版本号
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                System.Version currentVersion = assembly.GetName().Version;
                string currentVersionString = currentVersion.ToString();

                // 显示从最新版本到当前版本之间的更新内容
                var allUpdates = updateLog.Updates;
                if (allUpdates != null)
                {
                    var updatesToShow = allUpdates
                        .TakeWhile(update => string.Compare(update.Version, currentVersionString) >= 0)
                        .ToList();

                    ReleaseNotes.Text = string.Join("\n\n", updatesToShow.Select(update => $"版本 {update.Version} ({update.UpdateTime}):\n{string.Join("\n", update.UpdateContent)}"));
                    UpdateStatus("更新日志加载完成。");
                }
                else
                {
                    ReleaseNotes.Text = "没有可用的更新日志。";
                    UpdateStatus("没有可用的更新日志。");
                }
            }
            catch (Exception ex)
            {
                ReleaseNotes.Text = "加载更新日志时出错，请检查网络连接或联系支持。";
                UpdateStatus($"加载更新日志时出错: {ex.Message}");
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
                    string updateUrl = selectedDetail.UpdateURL;
                    string fileName = Path.GetFileName(updateUrl);
                    string rootPath = AppDomain.CurrentDomain.BaseDirectory;
                    string updateFolderPath = Path.Combine(rootPath, "update");
                    string filePath = Path.Combine(updateFolderPath, fileName);

                    UpdateStatus("检查文件是否存在...");
                    if (File.Exists(filePath))
                    {
                        if (ValidateFileChecksum(filePath, selectedDetail.Checksum))
                        {
                            UpdateStatus("文件校验成功，开始更新...");
                            InstallUpdate(filePath, selectedUpdateMode);
                        }
                        else
                        {
                            UpdateStatus("文件校验失败，重新下载...");
                            filePath = await DownloadUpdate(updateUrl);
                            if (ValidateFileChecksum(filePath, selectedDetail.Checksum))
                            {
                                UpdateStatus("文件校验成功，开始更新...");
                                InstallUpdate(filePath, selectedUpdateMode);
                            }
                            else
                            {
                                UpdateStatus("文件校验失败，请检查网络连接或联系支持。");
                            }
                        }
                    }
                    else
                    {
                        UpdateStatus("文件不存在，开始下载...");
                        filePath = await DownloadUpdate(updateUrl);
                        if (ValidateFileChecksum(filePath, selectedDetail.Checksum))
                        {
                            UpdateStatus("文件校验成功，开始更新...");
                            InstallUpdate(filePath, selectedUpdateMode);
                        }
                        else
                        {
                            UpdateStatus("文件校验失败，请检查网络连接或联系支持。");
                        }
                    }
                }
            }
        }

        private Task StartUpdater(string filePath, string updateMode)
        {
            string tempExtractPath = GetPendingUpdatePath(databasePath);
            if (string.IsNullOrEmpty(tempExtractPath))
            {
                tempExtractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_update");
                Directory.CreateDirectory(tempExtractPath);
            }

            using (ZipArchive archive = ZipFile.OpenRead(filePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.Combine(tempExtractPath, entry.FullName);

                    // 确保目标目录存在
                    string destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    // 删除已存在的文件
                    if (File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath);
                    }

                    // 解压并覆盖现有文件
                    entry.ExtractToFile(destinationPath, true);
                }
            }

            UpdateStatus($"解压完成！临时解压路径: {tempExtractPath}");

            // 启动辅助程序进行更新
            string updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater.exe");
            Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = $"\"{tempExtractPath}\" \"{AppDomain.CurrentDomain.BaseDirectory}\"",
                UseShellExecute = true
            });

            // 关闭主程序
            Application.Current.Shutdown();

            // 返回已完成的任务
            return Task.CompletedTask;
        }

        private string GetPendingUpdatePath(string dbPath)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    string query = "SELECT Value FROM Settings WHERE Key = 'PendingUpdatePath';";
                    var command = new SqliteCommand(query, connection);
                    return command.ExecuteScalar()?.ToString();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("读取PendingUpdatePath时发生错误:{error}", ex.ToString());
                MessageBox.Show("读取PendingUpdatePath时发生错误: " + ex.Message);
                return null;
            }
        }

        private void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value += 10; // 根据需要调整进度条的增量
                StatusTextBlock.Text = status;
            });
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
            await Task.Delay(1000); // 添加延迟
            return filePath;
        }

        private bool ValidateFileChecksum(string filePath, List<Checksum> checksums)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    var fileChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    return checksums.Any(c => c.MD5.ToLowerInvariant() == fileChecksum);
                }
            }
        }

        private bool ExtractAndOpen(string filePath)
        {
            try
            {
                string tempExtractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_update");
                Directory.CreateDirectory(tempExtractPath);

                using (ZipArchive archive = ZipFile.OpenRead(filePath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destinationPath = Path.Combine(tempExtractPath, entry.FullName);

                        // 确保目标目录存在
                        string destinationDir = Path.GetDirectoryName(destinationPath);
                        if (!Directory.Exists(destinationDir))
                        {
                            Directory.CreateDirectory(destinationDir);
                        }

                        // 删除已存在的文件
                        if (File.Exists(destinationPath))
                        {
                            File.Delete(destinationPath);
                        }

                        // 解压并覆盖现有文件
                        entry.ExtractToFile(destinationPath, true);
                    }
                }
                UpdateStatus($"解压完成！临时解压路径: {tempExtractPath}");

                // 标记需要在下次启动时进行文件替换
                SaveTempExtractPath(databasePath, tempExtractPath);

                return true; // 解压成功
            }
            catch (Exception ex)
            {
                UpdateStatus($"解压时出错: {ex.Message}");
                return false; // 解压失败
            }
        }

        private void SaveTempExtractPath(string dbPath, string pendingUpdatePath)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    string query = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('PendingUpdatePath', @PendingUpdatePath);";
                    var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@PendingUpdatePath", pendingUpdatePath);
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("保存PendingUpdatePath时发生错误:{error}", ex.ToString());
                MessageBox.Show("保存PendingUpdatePath时发生错误: " + ex.Message);
            }
        }

        private bool InstallPackage(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });

                return true; // 安装成功
            }
            catch (Exception ex)
            {
                UpdateStatus($"安装时出错: {ex.Message}");
                return false; // 安装失败
            }
        }

        private void InstallUpdate(string filePath, string updateMode)
        {
            bool updateSuccessful = false;

            if (updateMode.Contains("zip"))
            {
                UpdateStatus("开始解压...");
                // 获取用户文档文件夹路径
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                // 构建Updater.exe的相对路径
                string updaterPath = Path.Combine(documentsPath, "StreeDB", "update", "Updater.exe");
                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = $"\"{filePath}\" \"{AppDomain.CurrentDomain.BaseDirectory}\"",
                    UseShellExecute = true
                });

                // 关闭主程序
                Application.Current.Shutdown();
            }
            else if (updateMode.Contains("installer"))
            {
                UpdateStatus("开始安装...");
                updateSuccessful = InstallPackage(filePath);
            }
            else
            {
                UpdateStatus("未知的更新模式。");
            }

            // 只有在更新成功完成时才删除更新文件
            if (updateSuccessful)
            {
                DeleteUpdateFile(filePath);
            }
        }

        private void DeleteUpdateFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    UpdateStatus("更新文件已删除。");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"删除更新文件时出错: {ex.Message}");
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