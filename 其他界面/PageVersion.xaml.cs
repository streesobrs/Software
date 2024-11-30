using AutoUpdaterDotNET;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Serilog;
using Software.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

namespace Software.其他界面
{
    public class Update
    {
        public string version { get; set; }
        public string updateTime { get; set; }
        public List<string> updateContent { get; set; }
    }

    public class Root
    {
        public string newVersion { get; set; }
        public string version { get; set; }
        public string jsonVersion { get; set; }
        public List<Update> updates { get; set; }
    }

    /// <summary>
    /// PageVersion.xaml 的交互逻辑
    /// </summary>
    public partial class PageVersion : Page
    {
        string databasePath = DatabaseHelper.GetDatabasePath();

        private int refreshCount = 0;
        private const int RefreshThreshold = 5;
        private string version;
        private const string DefaultJsonFilePath = "resources\\update_log.json";
        private string jsonFilePath = DefaultJsonFilePath;
        private string JsonUrl;
        private string versionColor;
        private string updateTimeColor;

        private ILogger logger;

        public ILogger MyLoger
        {
            get
            {
                if (logger == null)
                {
                    logger = Log.ForContext<PageVersion>();
                }
                return logger;
            }
        }

        public PageVersion()
        {
            InitializeComponent();

            // 获取当前正在执行的程序集
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            // 获取版本信息
            System.Version version = assembly.GetName().Version;
            // 将版本信息转换为字符串
            string versionString = version.ToString();

            // 在文本块中显示版本信息
            VersionTextBlock.Text = "当前版本：" + versionString;

            JsonUrl = GetConfigValueFromDatabase(databasePath, "UpdateLogUrl");
            versionColor = GetConfigValueFromDatabase(databasePath, "VersionColor");
            updateTimeColor = GetConfigValueFromDatabase(databasePath, "UpdateTimeColor");

            // 先检查更新日志文件是否存在，不存在则下载
            if (!File.Exists(jsonFilePath))
            {
                _ = DownloadUpdates(true); // 传入参数表示强制下载
            }
            else
            {
                _ = DownloadUpdates(false); // 传入参数表示只读取本地文件
            }
        }

        /// <summary>
        /// 从数据库中读取配置值
        /// </summary>
        /// <param name="dbPath">数据库路径</param>
        /// <param name="key">配置键</param>
        /// <returns>配置值</returns>
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

        // 读取本地文件的方法
        private string ReadLocalFile(string filePath)
        {
            string json = "";
            try
            {
                // 读取文件内容
                json = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                // 如果读取失败，显示错误信息
                MessageBox.Show("本地读取更新失败，错误信息：\n" + ex.Message);
            }
            return json;
        }

        // 从网络下载文件的方法
        private async Task<string> DownloadFileFromNetwork(string url, string tempFilePath)
        {
            string json = "";
            WebClient client = new WebClient();
            try
            {
                // 异步下载文件
                await client.DownloadFileTaskAsync(new Uri(url), tempFilePath);
                // 读取下载的文件内容
                json = File.ReadAllText(tempFilePath);
            }
            catch (Exception ex)
            {
                // 如果下载失败，显示错误信息
                MessageBox.Show("下载更新失败，错误信息：\n" + ex.Message);
            }
            finally
            {
                // 释放WebClient资源
                ((IDisposable)client)?.Dispose();
                // 删除临时文件
                File.Delete(tempFilePath);
            }
            return json;
        }

        private async Task DownloadUpdates(bool forceDownload)
        {
            refreshCount++;
            if (refreshCount >= RefreshThreshold)
            {
                // 清空历史文档
                historyDocument.Blocks.Clear();
            }

            string json = "";
            if (forceDownload || !File.Exists(jsonFilePath))
            {
                string tempFilePath = "resources\\temp_update_log.json";
                try
                {
                    // 从网络下载json文件
                    json = await DownloadFileFromNetwork(JsonUrl, tempFilePath);
                    // 如果下载成功，用临时文件覆盖原文件
                    File.Copy(tempFilePath, jsonFilePath, true);
                    // 读取json文件内容
                    json = File.ReadAllText(jsonFilePath);
                }
                catch (Exception ex)
                {
                    // 如果下载失败，显示错误信息
                    MessageBox.Show("下载更新失败，错误信息：\n" + ex.Message);
                }
                finally
                {
                    // 删除临时文件
                    File.Delete(tempFilePath);
                }
            }
            else
            {
                // 读取本地json文件
                json = ReadLocalFile(jsonFilePath);
            }

            if (!string.IsNullOrEmpty(json))
            {
                // 将json字符串反序列化为对象
                Root root = JsonConvert.DeserializeObject<Root>(json);
                version = root.version;
                // 显示更新信息
                DisplayUpdates();
            }
        }

        private void DisplayUpdates()
        {
            // 清空历史文档
            historyDocument.Blocks.Clear();
            try
            {
                // 检查文件是否存在
                if (!File.Exists(jsonFilePath))
                {
                    MessageBox.Show("文件不存在：" + jsonFilePath);
                    return;
                }

                // 读取文件内容
                string json = File.ReadAllText(jsonFilePath);

                // 检查文件内容是否为空
                if (string.IsNullOrEmpty(json))
                {
                    MessageBox.Show("文件内容为空：" + jsonFilePath);
                    return;
                }

                // 尝试解析 JSON
                Root root = JsonConvert.DeserializeObject<Root>(json);

                // 检查解析结果是否为 null
                if (root == null)
                {
                    MessageBox.Show("无法解析 JSON：" + json);
                    return;
                }

                // 检查 JsonVersion 是否为 null
                if (root.jsonVersion == null)
                {
                    MessageBox.Show("JsonVersion 为 null");
                    return;
                }

                // 检查 updates 是否为 null
                if (root.updates == null)
                {
                    MessageBox.Show("updates 为 null");
                    return;
                }

                // 默认处理最新版本的 updates
                foreach (Update update in root.updates)
                {
                    // 检查 update 和 update.updateContent 是否为 null
                    if (update == null || update.updateContent == null)
                    {
                        continue;
                    }

                    Paragraph paragraph = new Paragraph();

                    // 创建并添加版本信息
                    Run versionRun = new Run($"版本：{update.version}\n");
                    versionRun.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(versionColor);
                    paragraph.Inlines.Add(versionRun);

                    // 创建并添加更新时间信息
                    Run updateTimeRun = new Run($"·更新时间：{update.updateTime}\n");
                    updateTimeRun.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(updateTimeColor);
                    paragraph.Inlines.Add(updateTimeRun);

                    // 创建并添加更新内容标签
                    Run updateContentLabel = new Run("更新内容:\n");
                    paragraph.Inlines.Add(updateContentLabel);

                    // 添加更新内容
                    foreach (string content in update.updateContent)
                    {
                        paragraph.Inlines.Add(new Run("- " + content + "\n"));
                    }
                    // 将段落添加到历史文档
                    historyDocument.Blocks.Add(paragraph);
                }

                // 特定版本的处理逻辑
                if (root.jsonVersion == "1.0.0")
                {
                    // 处理版本为 "1.0.0" 的特定逻辑
                }
                else if (root.jsonVersion == "1.0.1")
                {
                    // 处理版本为 "1.0.1" 的特定逻辑
                }
                // 可以继续添加其他已知版本的处理逻辑

            }
            catch (Exception ex)
            {
                // 显示异常信息
                MessageBox.Show("在解析 JSON 时出现异常：\n" + ex.ToString());
            }
        }

        private async void VersionTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 省略此方法中的部分代码，与上述修改思路类似，可根据需要进一步优化此方法中
                // 涉及文件获取的逻辑，这里暂不详细展开修改，可参考前面的修改思路
                // 比如先判断文件是否存在，不存在则下载等操作

                // 获取当前正在执行的程序集
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                // 获取当前的软件版本
                System.Version currentVersion = assembly.GetName().Version;

                // 下载文件
                string jsonFilePath = "resources\\update_log.json";
                string tempFilePath = "resources\\temp_update_log.json";

                string jsonUrl = GetConfigValueFromDatabase(databasePath, "UpdateLogUrl");

                WebClient client = new WebClient();
                try
                {
                    // 异步下载文件
                    await client.DownloadFileTaskAsync(new Uri(jsonUrl), tempFilePath);
                    // 如果下载成功，用临时文件覆盖原文件
                    File.Copy(tempFilePath, jsonFilePath, true);
                }
                catch (Exception ex)
                {
                    // 如果下载失败，显示错误信息
                    MessageBox.Show("下载更新失败，错误信息：\n" + ex.Message);
                }
                finally
                {
                    // 释放WebClient资源
                    ((IDisposable)client)?.Dispose();
                    // 删除临时文件
                    File.Delete(tempFilePath);
                }

                // 读取文件
                string json = "";
                try
                {
                    // 读取文件内容
                    json = File.ReadAllText(jsonFilePath);
                }
                catch (Exception ex)
                {
                    // 如果读取失败，显示错误信息
                    MessageBox.Show("本地读取更新失败，错误信息：\n" + ex.Message);
                }

                if (!string.IsNullOrEmpty(json))
                {
                    // 将json字符串反序列化为对象
                    Root root = JsonConvert.DeserializeObject<Root>(json);
                    version = root.version;
                    // 显示更新信息
                    DisplayUpdates();
                }

                // 将获取的最新版本转换为System.Version
                System.Version latestVersion = new System.Version(version);
                // 比较当前的软件版本和获取的最新版本
                int comparison = currentVersion.CompareTo(latestVersion);
                if (comparison < 0)
                {
                    // 如果当前的软件版本小于获取的最新版本，那么有更新可用
                    MessageBoxResult result = MessageBox.Show("刷新成功!\n获取的最新版本为：" + version + "\n软件当前的版本为：" + currentVersion + "\n发现新版本，建议您更新以获取最新功能和改进。\n你想现在下载更新吗？", "有更新可用", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        // 初始化一个空的字符串变量
                        string versionString = string.Empty;

                        try
                        {
                            // 获取更新服务器的IP地址
                            var UpdateIP = ConfigurationManager.AppSettings["UpdatePath"];
                            // 获取版本信息
                            System.Version softwareversion = assembly.GetName().Version;
                            // 将版本信息转换为字符串
                            versionString = softwareversion.ToString();
                            // 创建一个新的HttpClient实例
                            var httpClient = new HttpClient();
                            // 从更新服务器获取XML字符串
                            var xmlString = await httpClient.GetStringAsync($"{UpdateIP}");
                            // 解析XML字符串
                            var xdoc = XDocument.Parse(xmlString);
                            // 获取XML中的"category"元素
                            var versionElement = xdoc.Descendants("version").FirstOrDefault();
                            if (versionElement != null)
                            {
                                // 解析"category"元素的值为Version对象
                                var version = Version.Parse(versionElement.Value);

                                // 如果服务器的版本高于当前版本，则启动自动更新
                                if (version > Assembly.GetExecutingAssembly().GetName().Version)
                                {
                                    AutoUpdater.Start($"{UpdateIP}");
                                }
                                else
                                {
                                    // 如果当前版本已经是最新的，则显示消息框
                                    MessageBox.Show($"当前{versionString}已是最新版本");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // 在这里处理异常，例如显示错误消息
                            MessageBox.Show($"更新检查失败：{ex.Message}");
                        }
                    }
                }
                else if (comparison > 0)
                {
                    // 如果当前的软件版本大于获取的最新版本，那么当前的软件是测试版
                    MessageBox.Show("刷新成功!\n获取的最新版本为：" + version + "\n软件当前的版本为：" + currentVersion + "\n您正在使用的是预览版，可能包含尚未发布的新功能。");
                }
                else
                {
                    // 如果当前的软件版本等于获取的最新版本，那么当前的软件是最新版本
                    MessageBox.Show("刷新成功!\n获取的最新版本为：" + version + "\n软件当前的版本为：" + currentVersion + "\n您的软件已是最新版本，无需更新。");
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 显示更新信息
            DisplayUpdates();
            MessageBox.Show("刷新成功");
        }

    }
}