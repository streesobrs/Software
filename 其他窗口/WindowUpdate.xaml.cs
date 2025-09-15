using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using Serilog;
using Software.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Software.其他窗口
{
    public partial class WindowUpdate : Window
    {
        private Stopwatch stopwatch = new Stopwatch();
        private Config config;
        private string newUpdatePath;
        private string JsonUrl;
        private ILogger logger;
        string databasePath = DatabaseHelper.GetDatabasePath();

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

        public enum UpdateStage
        {
            CheckingUpdates,
            PreparingUpdate,
            Downloading,
            Validating,
            Installing
        }

        private readonly Dictionary<UpdateStage, int> stageWeights = new Dictionary<UpdateStage, int>
        {
            { UpdateStage.CheckingUpdates, 5 },
            { UpdateStage.PreparingUpdate, 5 },
            { UpdateStage.Downloading, 80 },
            { UpdateStage.Validating, 5 },
            { UpdateStage.Installing, 5 }
        };

        private UpdateStage currentStage = UpdateStage.CheckingUpdates;
        private double currentStageProgress = 0;

        public WindowUpdate()
        {
            InitializeComponent();
            newUpdatePath = GetConfigValueFromDatabase(databasePath, "NewUpdatePath");
            JsonUrl = GetConfigValueFromDatabase(databasePath, "UpdateLogUrl");
            InitializeProgressBars();
            LoadUpdateInfo();
        }

        private void InitializeProgressBars()
        {
            MainProgressBar.Minimum = 0;
            MainProgressBar.Maximum = 100;
            MainProgressBar.Value = 0;

            SubProgressBar.Minimum = 0;
            SubProgressBar.Maximum = 100;
            SubProgressBar.Value = 0;

            MainProgressPercentage.Text = "0%";
            SubProgressPercentage.Text = "0%";
            DownloadProgress.Text = "0 MB / 0 MB";
            DownloadSpeed.Text = "下载速度: 0 MB/s";
            StatusTextBlock.Text = "状态: 等待中...";
            StageDescriptionText.Text = "当前阶段: 检查更新";
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

        private double CalculateTotalProgress()
        {
            double totalProgress = 0;
            foreach (var stage in Enum.GetValues(typeof(UpdateStage)).Cast<UpdateStage>())
            {
                if (stage < currentStage)
                {
                    totalProgress += stageWeights[stage];
                }
                else if (stage == currentStage)
                {
                    totalProgress += stageWeights[stage] * (currentStageProgress / 100);
                    break;
                }
            }
            return totalProgress;
        }

        private void UpdateMainProgress(double stageProgress, string status)
        {
            currentStageProgress = stageProgress;
            double totalProgress = CalculateTotalProgress();

            Dispatcher.Invoke(() =>
            {
                MainProgressBar.Value = totalProgress;
                MainProgressPercentage.Text = $"{totalProgress:F0}%";
                StatusTextBlock.Text = status;
                StageDescriptionText.Text = $"当前阶段: {GetStageDescription(currentStage)}";
            });
        }

        private string GetStageDescription(UpdateStage stage)
        {
            switch (stage)
            {
                case UpdateStage.CheckingUpdates: return "检查更新";
                case UpdateStage.PreparingUpdate: return "准备更新";
                case UpdateStage.Downloading: return "下载文件";
                case UpdateStage.Validating: return "校验文件";
                case UpdateStage.Installing: return "安装更新";
                default: return "未知阶段";
            }
        }

        private void UpdateSubProgress(double value, string status)
        {
            Dispatcher.Invoke(() =>
            {
                SubProgressBar.Value = value;
                SubProgressPercentage.Text = $"{value:F0}%";
                StatusTextBlock.Text = status;
            });
        }

        private async void LoadUpdateInfo()
        {
            try
            {
                currentStage = UpdateStage.CheckingUpdates;
                UpdateMainProgress(0, "正在获取更新信息...");

                UpdateMainProgress(10, "正在连接更新服务器...");
                config = await ConfigManager.LoadConfigFromUrlAsync(newUpdatePath);
                UpdateMainProgress(30, "更新信息获取完成");

                if (config != null)
                {
                    UpdateMainProgress(40, "正在解析更新内容...");

                    // 合并完整包和增量包到下拉框
                    foreach (var detail in config.Details)
                    {
                        detail.UpdateSourceAndMode = $"{detail.UpdateSource} - {detail.UpdateMode}";
                        UpdateModeComboBox.Items.Add(detail.UpdateSourceAndMode);
                    }

                    // 处理增量包
                    if (config.IncrementalPackages?.VersionPackages != null)
                    {
                        foreach (var versionPair in config.IncrementalPackages.VersionPackages)
                        {
                            var baseVersion = versionPair.Key.Split("→")[0];
                            var targetVersion = versionPair.Key.Split("→")[1];

                            foreach (var pkg in versionPair.Value.Packages)
                            {
                                var incrementalDetail = new UpdateDetail
                                {
                                    UpdateSource = pkg.UpdateSource,
                                    UpdateMode = "incremental",
                                    UpdateURL = pkg.UpdateURL,
                                    FileSize = pkg.FileSize,
                                    Checksum = new List<Checksum> { pkg.Checksum },
                                    MandatoryUpdate = pkg.MandatoryUpdate,
                                    BaseVersion = baseVersion,
                                    UpdateSourceAndMode = $"{pkg.UpdateSource} - 增量更新 ({baseVersion} → {targetVersion})"
                                };
                                config.Details.Add(incrementalDetail);
                                UpdateModeComboBox.Items.Add(incrementalDetail.UpdateSourceAndMode);
                            }
                        }
                    }

                    UpdateModeComboBox.IsEnabled = true;

                    // 显示版本信息
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    CurrentVersion.Text = assembly.GetName().Version.ToString();
                    VersionInfo.Text = config.Version;
                    LastUpdate.Text = config.Date; // 显示JSON中的更新发布时间

                    UpdateMainProgress(60, "正在加载更新日志...");
                    await LoadUpdateLog();

                    UpdateMainProgress(100, "更新准备就绪");
                    currentStage = UpdateStage.PreparingUpdate;
                    UpdateMainProgress(0, "请选择更新方式");
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("发生错误:{error}", ex.ToString());
                MessageBox.Show("发生错误: " + ex.Message);
                UpdateMainProgress(100, "更新检查失败");
            }
        }

        private async Task LoadUpdateLog()
        {
            try
            {
                string updateLogUrl = JsonUrl;
                UpdateLog updateLog = await UpdateLogManager.LoadUpdateLogFromUrlAsync(updateLogUrl);

                var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                var allUpdates = updateLog.Updates;

                if (allUpdates != null)
                {
                    var updatesToShow = allUpdates
                        .TakeWhile(update => Version.TryParse(update.Version, out var v) && v >= currentVersion)
                        .ToList();

                    ReleaseNotes.Text = string.Join("\n\n", updatesToShow.Select(update =>
                        $"版本 {update.Version} ({update.UpdateTime}):\n{string.Join("\n", update.UpdateContent)}"));
                }
                else
                {
                    ReleaseNotes.Text = "没有可用的更新日志。";
                }
            }
            catch (Exception ex)
            {
                ReleaseNotes.Text = "加载更新日志时出错，请检查网络连接。";
            }
        }

        private void UpdateModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IncrementalVersionHint.Visibility = Visibility.Collapsed;

            if (UpdateModeComboBox.SelectedItem != null && config != null)
            {
                string selectedItem = UpdateModeComboBox.SelectedItem.ToString();
                var detail = config.Details.FirstOrDefault(d => d.UpdateSourceAndMode == selectedItem);

                if (detail != null)
                {
                    DownloadSize.Text = detail.FileSize;
                    if (detail.UpdateMode == "incremental")
                    {
                        IncrementalVersionHint.Text = $"⚠️ 增量包适用版本: {detail.BaseVersion} → {config.Version}";
                        IncrementalVersionHint.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private async void StartUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (config != null && UpdateModeComboBox.SelectedItem != null)
            {
                try
                {
                    currentStage = UpdateStage.PreparingUpdate;
                    UpdateMainProgress(0, "正在准备更新...");

                    string selectedItem = UpdateModeComboBox.SelectedItem.ToString();
                    var selectedDetail = config.Details.FirstOrDefault(d => d.UpdateSourceAndMode == selectedItem);
                    if (selectedDetail == null)
                    {
                        UpdateMainProgress(100, "未找到匹配的更新方式");
                        return;
                    }

                    // 增量包版本校验
                    if (selectedDetail.UpdateMode == "incremental")
                    {
                        var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                        if (!Version.TryParse(selectedDetail.BaseVersion, out var baseVersion) || currentVersion != baseVersion)
                        {
                            UpdateMainProgress(100, "版本不匹配");
                            MessageBox.Show(
                                $"增量更新仅支持从 {selectedDetail.BaseVersion} 升级，当前版本为 {currentVersion}",
                                "版本错误",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error
                            );
                            return;
                        }
                    }

                    string url = selectedDetail.UpdateURL;
                    string fileName = Path.GetFileName(url);
                    string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update");
                    string filePath = Path.Combine(folder, fileName);

                    Directory.CreateDirectory(folder);
                    bool fileExists = File.Exists(filePath);

                    currentStage = UpdateStage.Downloading;
                    if (!fileExists || !ValidateFileChecksum(filePath, selectedDetail.Checksum))
                    {
                        UpdateMainProgress(0, "开始下载...");
                        filePath = await DownloadUpdate(url, selectedDetail.FileSize);
                    }
                    else
                    {
                        UpdateMainProgress(100, "文件已存在且有效");
                    }

                    currentStage = UpdateStage.Validating;
                    UpdateMainProgress(0, "验证文件...");
                    bool isValid = ValidateFileChecksum(filePath, selectedDetail.Checksum);
                    UpdateMainProgress(isValid ? 100 : 0, isValid ? "验证成功" : "验证失败");

                    if (!isValid)
                    {
                        MessageBox.Show("文件校验失败，请重新尝试", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    currentStage = UpdateStage.Installing;
                    UpdateMainProgress(0, "安装更新...");
                    InstallUpdate(filePath, selectedDetail.UpdateMode);
                    UpdateMainProgress(100, "更新完成，请重启应用");
                }
                catch (Exception ex)
                {
                    MyLoger.Error("更新失败:{error}", ex.ToString());
                    UpdateMainProgress(100, "更新失败: " + ex.Message);
                }
            }
        }

        private void InstallUpdate(string filePath, string updateMode)
        {
            try
            {
                string updaterPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "StreeDB", "update", "Updater.exe"
                );

                if (!File.Exists(updaterPath))
                    throw new FileNotFoundException("更新程序不存在", updaterPath);

                var startInfo = new ProcessStartInfo(updaterPath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(updaterPath)
                };

                string updateType = updateMode == "incremental" ? "incremental"
                    : updateMode == "zip" ? "zip" : "installer";

                startInfo.ArgumentList.Add("Software.exe");
                startInfo.ArgumentList.Add(filePath);
                startInfo.ArgumentList.Add(AppDomain.CurrentDomain.BaseDirectory);
                startInfo.ArgumentList.Add((DeletePackageAfterUpdate.IsChecked ?? true).ToString());
                startInfo.ArgumentList.Add(updateType);

                Process.Start(startInfo);
                Task.Delay(1000).ContinueWith(_ => Application.Current.Dispatcher.Invoke(Application.Current.Shutdown));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新程序启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<string> DownloadUpdate(string url, string fileSizeText)
        {
            try
            {
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update");
                string filePath = Path.Combine(folder, Path.GetFileName(url));

                using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
                {
                    var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    long totalBytes = response.Content.Headers.ContentLength ?? 0;
                    double totalMB = totalBytes / (1024.0 * 1024.0);

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fs = new FileStream(filePath, FileMode.Create))
                    {
                        byte[] buffer = new byte[8192];
                        long totalRead = 0;
                        int read;
                        int lastProgress = -1;
                        stopwatch.Restart();

                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            totalRead += read;
                            await fs.WriteAsync(buffer, 0, read);

                            double progress = totalBytes > 0 ? (double)totalRead / totalBytes * 100 : 0;
                            int progressInt = (int)progress;

                            if (progressInt != lastProgress)
                            {
                                lastProgress = progressInt;
                                UpdateMainProgress(progress, "下载中...");

                                double downloadedMB = totalRead / (1024.0 * 1024.0);
                                UpdateSubProgress(progress, $"下载: {downloadedMB:F2} MB / {totalMB:F2} MB");

                                Dispatcher.Invoke(() =>
                                {
                                    double speed = stopwatch.Elapsed.TotalSeconds > 0 ? downloadedMB / stopwatch.Elapsed.TotalSeconds : 0;
                                    DownloadSpeed.Text = $"下载速度: {speed:F2} MB/s";
                                    DownloadProgress.Text = $"{downloadedMB:F2} MB / {totalMB:F2} MB";
                                });
                            }
                        }
                    }
                }
                return filePath;
            }
            catch (Exception ex)
            {
                MyLoger.Error("下载失败:{error}", ex.ToString());
                throw;
            }
        }

        private bool ValidateFileChecksum(string filePath, List<Checksum> checksums)
        {
            try
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    string fileHash = BitConverter.ToString(hash).Replace("-", "").ToLower();
                    return checksums.Any(c => c.MD5.ToLower() == fileHash);
                }
            }
            catch
            {
                return false;
            }
        }

        private bool ExtractAndOpen(string filePath)
        {
            try
            {
                string tempPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_update");
                Directory.CreateDirectory(tempPath);

                using (var archive = ZipFile.OpenRead(filePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        string dest = Path.Combine(tempPath, entry.FullName);
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        entry.ExtractToFile(dest, true);
                    }
                }

                SaveTempExtractPath(databasePath, tempPath);
                return true;
            }
            catch (Exception ex)
            {
                MyLoger.Error("解压失败:{error}", ex.ToString());
                return false;
            }
        }

        private void SaveTempExtractPath(string dbPath, string path)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    var cmd = new SqliteCommand(
                        "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('PendingUpdatePath', @Path)",
                        connection
                    );
                    cmd.Parameters.AddWithValue("@Path", path);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("保存路径失败:{error}", ex.ToString());
            }
        }

        private bool InstallPackage(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                MyLoger.Error("安装失败:{error}", ex.ToString());
                return false;
            }
        }

        private void DeleteUpdateFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            catch (Exception ex)
            {
                MyLoger.Error("删除文件失败:{error}", ex.ToString());
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }

    #region 数据模型类（完全匹配new_update.json结构）
    public class Config
    {
        public string Version { get; set; }
        public string Date { get; set; }
        public List<UpdateDetail> Details { get; set; } = new List<UpdateDetail>();
        public IncrementalPackages IncrementalPackages { get; set; }
    }

    public class UpdateDetail
    {
        public string UpdateSource { get; set; }
        public string UpdateMode { get; set; }
        public string UpdateURL { get; set; }
        public string FileSize { get; set; }
        public List<Checksum> Checksum { get; set; } = new List<Checksum>();
        public bool MandatoryUpdate { get; set; }
        public string UpdateSourceAndMode { get; set; }
        public string BaseVersion { get; set; }
    }

    public class IncrementalPackages
    {
        public string MinRequiredVersion { get; set; }
        public Dictionary<string, IncrementalPackage> VersionPackages { get; set; } = new Dictionary<string, IncrementalPackage>();
    }

    public class IncrementalPackage
    {
        public List<IncrementalPackageDetail> Packages { get; set; } = new List<IncrementalPackageDetail>();
    }

    public class IncrementalPackageDetail
    {
        public string UpdateSource { get; set; }
        public string UpdateURL { get; set; }
        public string FileSize { get; set; }
        public Checksum Checksum { get; set; } = new Checksum();
        public bool MandatoryUpdate { get; set; }
    }

    public class Checksum
    {
        public string MD5 { get; set; }
    }

    public class UpdateLog
    {
        public string JsonVersion { get; set; }
        public string Version { get; set; }
        public List<UpdateInfo> Updates { get; set; } = new List<UpdateInfo>();
    }

    public class UpdateInfo
    {
        public string Version { get; set; }
        public string UpdateTime { get; set; }
        public List<string> UpdateContent { get; set; } = new List<string>();
    }
    #endregion

    #region 工具类
    public class ConfigManager
    {
        public static async Task<Config> LoadConfigFromUrlAsync(string url)
        {
            using (var client = new HttpClient())
            {
                string json = await client.GetStringAsync(url);
                var jObj = JObject.Parse(json);

                // 手动解析增量包（处理动态键）
                var config = jObj.ToObject<Config>();
                if (jObj["incrementalPackages"] != null)
                {
                    config.IncrementalPackages = new IncrementalPackages
                    {
                        MinRequiredVersion = jObj["incrementalPackages"]["minRequiredVersion"]?.ToString()
                    };

                    foreach (var prop in jObj["incrementalPackages"].Children<JProperty>())
                    {
                        if (prop.Name != "minRequiredVersion")
                        {
                            config.IncrementalPackages.VersionPackages[prop.Name] = prop.Value.ToObject<IncrementalPackage>();
                        }
                    }
                }
                return config;
            }
        }
    }

    public class UpdateLogManager
    {
        public static async Task<UpdateLog> LoadUpdateLogFromUrlAsync(string url)
        {
            using (var client = new HttpClient())
            {
                string json = await client.GetStringAsync(url);
                return JObject.Parse(json).ToObject<UpdateLog>();
            }
        }
    }

    public class ProgressBarWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && parameter is string ratioStr && double.TryParse(ratioStr, out double ratio))
            {
                return width * ratio;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    #endregion
}