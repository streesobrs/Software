using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Configuration;
using System.Net;
using Newtonsoft.Json;
using System.Net.NetworkInformation;

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
        public string jsonVersion {  get; set; }
        public List<Update> updates { get; set; }
    }
    /// <summary>
    /// PageVersion.xaml 的交互逻辑
    /// </summary>
    public partial class PageVersion : Page
    {
        private int refreshCount = 0;
        private const int RefreshThreshold = 5;
        private string version;
        private const string DefaultJsonFilePath = "resources\\update_log.json";
        private string jsonFilePath = DefaultJsonFilePath;
        private string JsonUrl = ConfigurationManager.AppSettings["UpdateLogUrl"];  // 从app.config文件中读取UpdateLogUrl

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

            DownloadUpdates();
        }

        private async void DownloadUpdates()
        {
            refreshCount++;
            if (refreshCount >= 5)
            {
                historyDocument.Blocks.Clear();
            }
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                MessageBox.Show("网络连接不可用，将尝试从本地读取更新。");
                jsonFilePath = "resources\\update_log.json";
            }
            else
            {
                string tempFilePath = "resources\\temp_update_log.json";
                try
                {
                    WebClient client = new WebClient();
                    try
                    {
                        await client.DownloadFileTaskAsync(new Uri(ConfigurationManager.AppSettings["UpdateLogUrl"]), tempFilePath);
                        File.Copy(tempFilePath, jsonFilePath, true);  // 如果下载成功，用临时文件覆盖原文件
                    }
                    finally
                    {
                        ((IDisposable)client)?.Dispose();
                        File.Delete(tempFilePath);  // 无论下载是否成功，都删除临时文件
                    }
                }
                catch (Exception ex2)
                {
                    Exception ex = ex2;
                    MessageBox.Show("下载更新失败，将尝试从本地读取更新。错误信息：\n" + ex.Message);
                    // 如果下载失败，就不覆盖原文件
                }
            }
            string json = File.ReadAllText(jsonFilePath);
            Root root = JsonConvert.DeserializeObject<Root>(json);
            version = root.version;
            DisplayUpdates();
        }

        private void DisplayUpdates()
        {
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
                //获取颜色
                string versionColor = ConfigurationManager.AppSettings["VersionColor"];
                string updateTimeColor = ConfigurationManager.AppSettings["UpdateTimeColor"];

                // 检查文件是否为空
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

                // 根据 JsonVersion 的值来处理 updates
                if (root.jsonVersion == "1.0.0")
                {
                    // 处理版本为 "1.0.0" 的 updates
                    foreach (Update update in root.updates)
                    {
                        // 检查 update 和 update.updateContent 是否为 null
                        if (update == null || update.updateContent == null)
                        {
                            continue;
                        }

                        Paragraph paragraph = new Paragraph();

                        Run versionRun = new Run($"版本：{update.version}\n");
                        versionRun.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(versionColor);  // 设置版本颜色为versionColor里的颜色
                        paragraph.Inlines.Add(versionRun);

                        Run updateTimeRun = new Run($"·更新时间：{update.updateTime}\n");
                        updateTimeRun.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(updateTimeColor);  // 设置版本颜色为updateTimeColor里的颜色
                        paragraph.Inlines.Add(updateTimeRun);

                        Run updateContentLabel = new Run("更新内容:\n");
                        paragraph.Inlines.Add(updateContentLabel);

                        foreach (string content in update.updateContent)
                        {
                            paragraph.Inlines.Add(new Run("- " + content + "\n"));
                        }
                        historyDocument.Blocks.Add(paragraph);
                    }
                }
                else
                {
                    // 处理其他版本的 updates
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("在解析 JSON 时出现异常：\n" + ex.ToString());
            }
        }

        private void VersionTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DownloadUpdates();

            // 获取当前正在执行的程序集
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            // 获取当前的软件版本
            System.Version currentVersion = assembly.GetName().Version;

            // 将获取的最新版本转换为System.Version
            System.Version latestVersion = new System.Version(version);

            // 比较当前的软件版本和获取的最新版本
            int comparison = currentVersion.CompareTo(latestVersion);
            if (comparison < 0)
            {
                // 如果当前的软件版本小于获取的最新版本，那么有更新可用
                MessageBox.Show("刷新成功!\n获取的最新版本为：" + version + "\n软件当前的版本为：" + currentVersion + "\n发现新版本，建议您更新以获取最新功能和改进。");
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
    }
}