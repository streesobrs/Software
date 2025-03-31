using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using Serilog;
using Software.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Software.其他窗口
{
    // 用于存储配置信息的类
    public class Config
    {
        public string Version { get; set; }
        public string DownloadSize { get; set; }
        public string CurrentVersion { get; set; }
        public string LastUpdate { get; set; }
        public string ReleaseNotes { get; set; }
        public List<UpdateDetail> Details { get; set; }
    }

    // 用于存储更新详细信息的类
    public class UpdateDetail
    {
        public string UpdateSource { get; set; }
        public string UpdateMode { get; set; }
        public string UpdateURL { get; set; }
        public string FileSize { get; set; }
        public List<Checksum> Checksum { get; set; }
        public bool MandatoryUpdate { get; set; }
        // 新增属性，用于存储更新源和更新模式组成的组合字符串
        public string UpdateSourceAndMode { get; set; }
    }

    // 用于存储文件校验和信息的类
    public class Checksum
    {
        public string MD5 { get; set; }
    }

    // 用于从指定URL加载配置信息的类
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

    // 用于存储更新日志信息的类
    public class UpdateLog
    {
        public string JsonVersion { get; set; }
        public string Version { get; set; }
        public List<UpdateInfo> Updates { get; set; }
    }

    // 用于存储每次更新的具体信息的类
    public class UpdateInfo
    {
        public string Version { get; set; }
        public string UpdateTime { get; set; }
        public List<string> UpdateContent { get; set; }
    }

    // 用于从指定URL加载更新日志的类
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

        // 用于获取日志记录器实例的属性
        public ILogger MyLoger
        {
            get
            {
                if (logger == null)
                {
                    logger = Log.ForContext<WindowUpdate>();
                }
                return logger;
            }
        }

        string databasePath = DatabaseHelper.GetDatabasePath();

        public WindowUpdate()
        {
            InitializeComponent();

            // 从数据库读取配置文件的相关路径和URL
            newUpdatePath = GetConfigValueFromDatabase(databasePath, "NewUpdatePath");
            JsonUrl = GetConfigValueFromDatabase(databasePath, "UpdateLogUrl");

            LoadUpdateInfo();
        }

        // 从数据库中根据键获取对应的值
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

        // 加载更新信息并展示在界面上
        private async void LoadUpdateInfo()
        {
            try
            {
                UpdateModeComboBox.IsEnabled = false;
                string configUrl = newUpdatePath;
                Uri updateUri = new Uri(configUrl);
                config = await ConfigManager.LoadConfigFromUrlAsync(updateUri.ToString());
                if (config != null)
                {
                    VersionInfo.Text = config.Version;
                    DownloadSize.Text = config.DownloadSize;
                    CurrentVersion.Text = config.CurrentVersion;
                    LastUpdate.Text = config.LastUpdate;
                    ReleaseNotes.Text = config.ReleaseNotes;

                    // 遍历更新详细信息列表，添加组合字符串到UpdateModeComboBox并存储到UpdateDetail对象中
                    foreach (UpdateDetail detail in config.Details)
                    {
                        string item = $"{detail.UpdateSource} - {detail.UpdateMode}";
                        detail.UpdateSourceAndMode = item;
                        UpdateModeComboBox.Items.Add(item);
                    }

                    UpdateModeComboBox.IsEnabled = true;

                    // 获取当前程序集的版本信息并展示在界面上
                    System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    System.Version version = assembly.GetName().Version;
                    string versionString = version.ToString();
                    CurrentVersion.Text = versionString;

                    // 从数据库读取并展示上次更新时间
                    LastUpdate.Text = LastUpdateTime();

                    // 保存当前时间为上次更新时间到数据库
                    SaveLastUpdateTime(databasePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    // 加载更新日志
                    await LoadUpdateLog();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("发生错误:{error}", ex.ToString());
                MessageBox.Show("发生错误: " + ex.Message);
            }
        }

        // 保存上次更新时间到数据库
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

        // 从数据库读取上次更新时间
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

        // 加载更新日志并展示相关更新内容在界面上
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

                // 筛选出需要展示的更新内容并展示在界面上
                var allUpdates = updateLog.Updates;
                if (allUpdates != null)
                {
                    var updatesToShow = allUpdates
                       .TakeWhile(update => Version.TryParse(update.Version, out var updateVersion) && updateVersion >= currentVersion)
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
                UpdateStatus($"加载更新日志时出错: " + ex.Message);
            }
        }

        // 处理更新方式选择更改事件，更新相关信息展示
        private void UpdateModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UpdateModeComboBox.SelectedItem != null && config != null)
            {
                string selectedItemString = UpdateModeComboBox.SelectedItem.ToString();

                // 根据组合字符串查找对应的UpdateDetail对象，并更新相关展示信息
                var selectedDetail = config.Details.FirstOrDefault(detail => detail.UpdateSourceAndMode == selectedItemString);
                if (selectedDetail != null)
                {
                    DownloadSize.Text = selectedDetail.FileSize;
                }
            }
        }

        // 点击开始更新按钮触发的操作
        private async void StartUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (config != null && UpdateModeComboBox.SelectedItem != null)
            {
                string selectedItemString = UpdateModeComboBox.SelectedItem.ToString();

                // 根据组合字符串查找对应的UpdateDetail对象
                var selectedDetail = config.Details.FirstOrDefault(detail => detail.UpdateSourceAndMode == selectedItemString);
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
                            InstallUpdate(filePath, selectedDetail.UpdateMode);
                        }
                        else
                        {
                            UpdateStatus("文件校验失败，重新下载...");
                            filePath = await DownloadUpdate(updateUrl);
                            if (ValidateFileChecksum(filePath, selectedDetail.Checksum))
                            {
                                UpdateStatus("文件校验成功，开始更新...");
                                InstallUpdate(filePath, selectedDetail.UpdateMode);
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
                            InstallUpdate(filePath, selectedDetail.UpdateMode);
                        }
                        else
                        {
                            UpdateStatus("文件校验失败，请检查网络连接或联系支持。");
                        }
                    }
                }
            }
        }
        // 启动更新程序的相关操作，包括解压、启动辅助程序等
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

        // 从数据库读取临时更新路径
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

        // 更新界面上的状态信息，如进度条、状态文本等
        private void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value += 10; // 根据需要调整进度条的增量
                StatusTextBlock.Text = status;
            });
        }

        // 下载更新文件的操作
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

        // 验证文件校验和的操作
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

        // 解压并打开文件的操作（部分功能可能与StartUpdater有重叠，可根据实际需求优化）
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

        // 保存临时解压路径到数据库的操作
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

        // 安装软件包的操作（假设这里是直接启动安装文件进行安装）
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

        // 根据更新模式进行实际更新操作的方法
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

        // 删除更新文件的操作
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

        // 更新进度条及相关下载信息展示的操作
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

        // 关闭窗口的操作
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
