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

    // 转换器实现
    public class ProgressBarWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && parameter is string ratioString &&
                double.TryParse(ratioString, out double ratio))
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

        // 定义更新阶段
        public enum UpdateStage
        {
            CheckingUpdates,      // 检查更新
            PreparingUpdate,      // 准备更新
            Downloading,          // 下载文件
            Validating,           // 校验文件
            Installing            // 安装更新
        }

        // 各阶段权重（总和为100）
        private readonly Dictionary<UpdateStage, int> stageWeights = new Dictionary<UpdateStage, int>
        {
            { UpdateStage.CheckingUpdates, 5 },   // 检查更新占5%
            { UpdateStage.PreparingUpdate, 5 },  // 准备更新占5%
            { UpdateStage.Downloading, 80 },      // 下载占80%（最耗时）
            { UpdateStage.Validating, 5 },       // 校验占5%
            { UpdateStage.Installing, 5 }        // 安装占5%
        };

        // 当前阶段
        private UpdateStage currentStage = UpdateStage.CheckingUpdates;
        // 当前阶段的完成度（0-100）
        private double currentStageProgress = 0;

        public WindowUpdate()
        {
            InitializeComponent();

            // 从数据库读取配置文件的相关路径和URL
            newUpdatePath = GetConfigValueFromDatabase(databasePath, "NewUpdatePath");
            JsonUrl = GetConfigValueFromDatabase(databasePath, "UpdateLogUrl");

            // 初始化进度条
            InitializeProgressBars();

            LoadUpdateInfo();
        }

        // 初始化进度条
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

        // 计算主进度条的总百分比
        private double CalculateTotalProgress()
        {
            double totalProgress = 0;

            // 累加前面所有阶段的权重
            foreach (var stage in Enum.GetValues(typeof(UpdateStage)).Cast<UpdateStage>())
            {
                if (stage < currentStage)
                {
                    totalProgress += stageWeights[stage];
                }
                else if (stage == currentStage)
                {
                    // 加上当前阶段的完成度
                    totalProgress += stageWeights[stage] * (currentStageProgress / 100);
                    break;
                }
            }

            return totalProgress;
        }

        // 更新主进度条
        private void UpdateMainProgress(double stageProgress, string status)
        {
            currentStageProgress = stageProgress;
            double totalProgress = CalculateTotalProgress();

            Dispatcher.Invoke(() =>
            {
                MainProgressBar.Value = totalProgress;
                MainProgressPercentage.Text = $"{totalProgress:F0}%";
                StatusTextBlock.Text = status;

                // 更新阶段描述文本
                string stageDescription = GetStageDescription(currentStage);
                StageDescriptionText.Text = $"当前阶段: {stageDescription}";
            });
        }

        // 获取阶段描述文本
        private string GetStageDescription(UpdateStage stage)
        {
            switch (stage)
            {
                case UpdateStage.CheckingUpdates:
                    return "检查更新";
                case UpdateStage.PreparingUpdate:
                    return "准备更新";
                case UpdateStage.Downloading:
                    return "下载文件";
                case UpdateStage.Validating:
                    return "校验文件";
                case UpdateStage.Installing:
                    return "安装更新";
                default:
                    return "未知阶段";
            }
        }

        // 更新子进度条（当前阶段的详细进度）
        private void UpdateSubProgress(double value, string status)
        {
            Dispatcher.Invoke(() =>
            {
                SubProgressBar.Value = value;
                SubProgressPercentage.Text = $"{value:F0}%";
                StatusTextBlock.Text = status;
            });
        }

        // 加载更新信息（检查更新阶段）
        private async void LoadUpdateInfo()
        {
            try
            {
                currentStage = UpdateStage.CheckingUpdates;
                UpdateMainProgress(0, "正在获取更新信息...");

                // 从网络获取更新配置
                UpdateMainProgress(10, "正在连接更新服务器...");
                config = await ConfigManager.LoadConfigFromUrlAsync(newUpdatePath);
                UpdateMainProgress(30, "更新信息获取完成");

                if (config != null)
                {
                    // 解析更新内容
                    UpdateMainProgress(40, "正在解析更新内容...");

                    // 填充更新模式下拉框
                    foreach (UpdateDetail detail in config.Details)
                    {
                        string item = $"{detail.UpdateSource} - {detail.UpdateMode}";
                        detail.UpdateSourceAndMode = item;
                        UpdateModeComboBox.Items.Add(item);
                    }

                    UpdateModeComboBox.IsEnabled = true;

                    // 获取当前版本
                    System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    System.Version version = assembly.GetName().Version;
                    CurrentVersion.Text = version.ToString();

                    // 显示更新版本信息
                    VersionInfo.Text = config.Version;

                    // 从数据库读取并展示上次更新时间
                    LastUpdate.Text = LastUpdateTime();

                    // 加载更新日志
                    UpdateMainProgress(60, "正在加载更新日志...");
                    await LoadUpdateLog();

                    // 完成检查更新阶段
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
                }
                else
                {
                    ReleaseNotes.Text = "没有可用的更新日志。";
                }
            }
            catch (Exception ex)
            {
                ReleaseNotes.Text = "加载更新日志时出错，请检查网络连接或联系支持。";
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

        private async void StartUpdate_Click(object sender, RoutedEventArgs e)
        {
            bool isValid = false; // 在方法合适的外层作用域先定义
            if (config != null && UpdateModeComboBox.SelectedItem != null)
            {
                try
                {
                    // 准备更新阶段
                    currentStage = UpdateStage.PreparingUpdate;
                    UpdateMainProgress(0, "正在准备更新...");

                    string selectedItemString = UpdateModeComboBox.SelectedItem.ToString();
                    var selectedDetail = config.Details.FirstOrDefault(d => d.UpdateSourceAndMode == selectedItemString);
                    if (selectedDetail == null)
                    {
                        UpdateMainProgress(100, "未找到匹配的更新方式");
                        return;
                    }

                    string updateUrl = selectedDetail.UpdateURL;
                    string fileName = Path.GetFileName(updateUrl);
                    string updateFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update");
                    string filePath = Path.Combine(updateFolderPath, fileName);

                    if (!Directory.Exists(updateFolderPath))
                    {
                        Directory.CreateDirectory(updateFolderPath);
                    }

                    UpdateMainProgress(30, "检查文件是否存在...");
                    bool fileExists = File.Exists(filePath);

                    // 下载阶段
                    currentStage = UpdateStage.Downloading;
                    if (!fileExists)
                    {
                        UpdateMainProgress(0, "文件不存在，开始下载...");
                        filePath = await DownloadUpdate(updateUrl, selectedDetail.FileSize);
                    }
                    else
                    {
                        UpdateMainProgress(0, "文件已存在，验证完整性...");
                        isValid = ValidateFileChecksum(filePath, selectedDetail.Checksum); // 复用之前定义的变量

                        if (isValid)
                        {
                            UpdateMainProgress(100, "文件校验通过，跳过下载");
                        }
                        else
                        {
                            UpdateMainProgress(0, "文件已损坏，重新下载...");
                            filePath = await DownloadUpdate(updateUrl, selectedDetail.FileSize);
                        }
                    }

                    // 校验阶段
                    currentStage = UpdateStage.Validating;
                    UpdateMainProgress(0, "正在验证文件完整性...");
                    isValid = ValidateFileChecksum(filePath, selectedDetail.Checksum); // 继续复用
                    UpdateMainProgress(isValid ? 100 : 0, isValid ? "文件校验成功" : "文件校验失败");

                    if (!isValid)
                    {
                        UpdateMainProgress(0, "文件校验失败，重新下载...");
                        filePath = await DownloadUpdate(updateUrl, selectedDetail.FileSize);

                        UpdateMainProgress(0, "重新验证文件完整性...");
                        isValid = ValidateFileChecksum(filePath, selectedDetail.Checksum); // 还是复用
                        UpdateMainProgress(isValid ? 100 : 0, isValid ? "文件校验成功" : "文件校验失败");

                        if (!isValid)
                        {
                            UpdateMainProgress(100, "文件校验失败，请检查网络连接或联系支持。");
                            return;
                        }
                    }

                    // 安装阶段
                    currentStage = UpdateStage.Installing;
                    UpdateMainProgress(0, "开始安装更新...");
                    InstallUpdate(filePath, selectedDetail.UpdateMode);
                    UpdateMainProgress(100, "更新完成，请重启应用");
                }
                catch (Exception ex)
                {
                    MyLoger.Error("更新过程中发生错误:{error}", ex.ToString());
                    UpdateMainProgress(100, "更新过程中发生错误: " + ex.Message);
                }
            }
        }

        // 根据更新模式进行实际更新操作的方法
        private void InstallUpdate(string filePath, string updateMode)
        {
            UpdateMainProgress(0, "准备启动更新程序...");
            try
            {
                // 1. 配置更新参数（保持不变）
                string mainAppExe = "Software.exe";
                string packagePath = filePath;
                string targetDir = AppDomain.CurrentDomain.BaseDirectory;
                bool deleteAfterUpdate = DeletePackageAfterUpdate.IsChecked ?? true;
                string updateType = updateMode.Contains("zip") ? "zip" : "installer";

                // 2. 定位Updater.exe（保持不变）
                string updaterPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "StreeDB", "update", "Updater.exe"
                );

                // 3. 验证必要文件（保持不变）
                if (!File.Exists(updaterPath))
                {
                    throw new FileNotFoundException("更新程序（Updater.exe）不存在", updaterPath);
                }
                if (!File.Exists(packagePath))
                {
                    throw new FileNotFoundException("安装包文件不存在", packagePath);
                }

                // 4. 关键修改：使用ArgumentList传递参数（替代字符串拼接）
                var startInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(updaterPath)
                };
                // 按顺序添加参数（自动处理空格和引号）
                startInfo.ArgumentList.Add(mainAppExe);       // 1. 主程序文件名
                startInfo.ArgumentList.Add(packagePath);      // 2. 安装包路径
                startInfo.ArgumentList.Add(targetDir);        // 3. 目标目录
                startInfo.ArgumentList.Add(deleteAfterUpdate.ToString()); // 4. 是否删除
                startInfo.ArgumentList.Add(updateType);       // 5. 更新类型

                // 5. 记录调试信息（优化参数日志格式）
                MyLoger.Information($"启动更新器：{updaterPath}");
                MyLoger.Information($"传递的参数列表：");
                MyLoger.Information($"  1. 主程序文件名：{mainAppExe}");
                MyLoger.Information($"  2. 安装包路径：{packagePath}");
                MyLoger.Information($"  3. 目标目录：{targetDir}");
                MyLoger.Information($"  4. 是否删除：{deleteAfterUpdate}");
                MyLoger.Information($"  5. 更新类型：{updateType}");

                // 6. 启动更新器
                Process.Start(startInfo);

                // 后续代码保持不变...
                UpdateMainProgress(0, $"更新程序已启动，正在处理{updateType}更新...");

                Task.Delay(1000).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Application.Current.Shutdown();
                    });
                });
            }
            catch (Exception ex)
            {
                // 异常处理保持不变...
                string errorMsg = $"启动更新程序失败：{ex.Message}";
                UpdateMainProgress(0, errorMsg);
                MyLoger.Error(errorMsg + "\n" + ex.StackTrace);
                MessageBox.Show(
                    errorMsg + "\n请检查更新程序是否存在或权限是否足够",
                    "更新错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
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

        // 下载更新文件（带详细进度）
        private async Task<string> DownloadUpdate(string url, string fileSizeText)
        {
            try
            {
                // 明确标记当前阶段为下载
                currentStage = UpdateStage.Downloading;
                UpdateMainProgress(0, "初始化下载...");

                string updateFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update");
                string filePath = Path.Combine(updateFolderPath, Path.GetFileName(url));

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(30);
                    HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    long totalBytes = response.Content.Headers.ContentLength ?? 0;
                    double totalMB = totalBytes / (1024.0 * 1024.0);

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        long totalReadBytes = 0;
                        int readBytes;
                        int lastProgress = -1;
                        stopwatch.Restart();

                        while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            totalReadBytes += readBytes;
                            await fs.WriteAsync(buffer, 0, readBytes);

                            // 计算当前阶段的进度（下载阶段）
                            double stageProgress = totalBytes > 0 ? (double)totalReadBytes / totalBytes * 100 : 0;
                            int progressInt = (int)stageProgress;

                            // 每1%更新一次进度，减少UI更新频率
                            if (progressInt != lastProgress)
                            {
                                lastProgress = progressInt;

                                // 更新主进度条（基于阶段权重）
                                UpdateMainProgress(stageProgress, "正在下载更新文件...");

                                // 更新子进度条（详细下载进度）
                                double downloadedMB = totalReadBytes / (1024.0 * 1024.0);
                                string progressText = $"正在下载: {downloadedMB:F2} MB / {totalMB:F2} MB";
                                UpdateSubProgress(stageProgress, progressText);

                                // 更新下载速度
                                Dispatcher.Invoke(() =>
                                {
                                    double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                                    double downloadSpeed = elapsedSeconds > 0 ? downloadedMB / elapsedSeconds : 0;
                                    DownloadSpeed.Text = $"下载速度: {downloadSpeed:F2} MB/s";
                                    DownloadProgress.Text = $"{downloadedMB:F2} MB / {totalMB:F2} MB";
                                });
                            }
                        }
                        stopwatch.Stop();
                    }
                }

                return filePath;
            }
            catch (Exception ex)
            {
                MyLoger.Error("下载时出错:{error}", ex.ToString());
                throw;
            }
        }

        // 验证文件校验和的操作
        private bool ValidateFileChecksum(string filePath, List<Checksum> checksums)
        {
            try
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        var hash = md5.ComputeHash(stream);
                        var fileChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                        bool isValid = checksums.Any(c => c.MD5.ToLowerInvariant() == fileChecksum);
                        return isValid;
                    }
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("验证文件完整性时出错:{error}", ex.ToString());
                return false;
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

                // 标记需要在下次启动时进行文件替换
                SaveTempExtractPath(databasePath, tempExtractPath);

                return true; // 解压成功
            }
            catch (Exception ex)
            {
                MyLoger.Error("解压时出错:{error}", ex.ToString());
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
                MyLoger.Error("安装时出错:{error}", ex.ToString());
                return false; // 安装失败
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
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("删除更新文件时出错:{error}", ex.ToString());
            }
        }

        // 关闭窗口的操作
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}