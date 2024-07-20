using Software.其他界面;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace Software
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);

                SettingsMigrator migrator = new SettingsMigrator();
                migrator.MigrateSettings();

                // 检查配置文件是否存在
                if (!File.Exists("Software.dll.config"))
                {
                    // 如果不存在，创建一个新的配置文件
                    using (var stream = File.Create("Software.dll.config"))
                    {
                        // 写入默认的设置
                        string defaultSettings = @"
<configuration>
    <appSettings>
        <add key=""GamePath"" value=""""/>
        <add key=""TextContent"" value=""""/>
        <add key=""UpdatePath"" value=""https://gitee.com/nibadianbanxiaban/software/releases/download/resources/update.xml""/>
        <add key=""LaunchCount"" value=""0""/>
        <add key=""EnableCounting"" value=""false""/>
        <add key=""RetryCount"" value=""5""/>
        <add key=""RetryDelay"" value=""10""/>
        <add key=""adcode"" value=""""/>
        <add key=""UpdateLogUrl"" value=""https://gitee.com/nibadianbanxiaban/software/releases/download/resources/update_log.json"" />
        <add key=""VersionColor"" value=""Red""/>
		<add key=""UpdateTimeColor"" value=""Blue""/>
        <add key=""Culture"" value=""zh-CN""/>
        <add key=""EnableAutoUpdate"" value=""true""/>
    </appSettings>
</configuration>";
                        byte[] data = Encoding.UTF8.GetBytes(defaultSettings);
                        stream.Write(data, 0, data.Length);
                    }
                }
                else
                {
                    // 如果存在，检查是否缺少某些设置，并添加缺少的设置
                    XDocument doc = XDocument.Load("Software.dll.config");
                    XElement appSettings = doc.Root.Element("appSettings");

                    // 检查每个需要的设置
                    CheckAndAddSetting(appSettings, "GamePath", "");
                    CheckAndAddSetting(appSettings, "TextContent", "");
                    CheckAndAddSetting(appSettings, "UpdatePath", "https://gitee.com/nibadianbanxiaban/software/releases/download/resources/update.xml");
                    CheckAndAddSetting(appSettings, "LaunchCount", "0");
                    CheckAndAddSetting(appSettings, "EnableCounting", "false");
                    CheckAndAddSetting(appSettings, "RetryCount", "5");
                    CheckAndAddSetting(appSettings, "RetryDelay", "10");
                    CheckAndAddSetting(appSettings, "adcode", "");
                    CheckAndAddSetting(appSettings, "UpdateLogUrl", "https://gitee.com/nibadianbanxiaban/software/releases/download/resources/update_log.json");
                    CheckAndAddSetting(appSettings, "VersionColor", "Red");
                    CheckAndAddSetting(appSettings, "UpdateTimeColor", "Blue");
                    CheckAndAddSetting(appSettings, "Culture", "zh-CN");
                    CheckAndAddSetting(appSettings, "EnableAutoUpdate", "true");
                    // 保存修改后的配置文件
                    doc.Save("Software.dll.config");
                }

                // 获取当前活动的进程
                Process currentProcess = Process.GetCurrentProcess();

                // 检查是否有其他相同的进程正在运行
                var runningProcess = Process.GetProcesses().FirstOrDefault(p =>
                    p.Id != currentProcess.Id &&
                    p.ProcessName.Equals(currentProcess.ProcessName, StringComparison.Ordinal));

                // 如果有其他的进程正在运行，那么关闭当前进程
                if (runningProcess != null)
                {
                    MessageBox.Show("应用程序已经在运行中。");
                    this.Shutdown();
                    return;
                }



            }
            catch (Exception ex)
            {
                // 将异常信息写入日志
                File.WriteAllText("error.log", ex.ToString());

                // 显示一个错误消息
                MessageBox.Show("应用程序在启动时遇到了一个错误。请查看 error.log 文件以获取更多信息。");

                // 关闭应用程序
                this.Shutdown();
            }
            
            base.OnStartup(e);
        }

        // 检查一个设置是否存在，如果不存在，就添加这个设置
        void CheckAndAddSetting(XElement appSettings, string key, string value)
        {
            XElement setting = appSettings.Elements("add").FirstOrDefault(e => e.Attribute("key").Value == key);
            if (setting == null)
            {
                // 如果设置不存在，添加这个设置
                appSettings.Add(new XElement("add", new XAttribute("key", key), new XAttribute("value", value)));
            }
        }

        public class SettingsMigrator
        {
            public void MigrateSettings()
            {
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                var settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Software");
                var softwareDirectories = Directory.GetDirectories(settingsDirectory);

                // 选择名称以“Software_Url_”开头的文件夹
                var softwareUrlDirectory = softwareDirectories.FirstOrDefault(dir => Path.GetFileName(dir).StartsWith("Software_Url_"));

                if (softwareUrlDirectory == null)
                {
                    throw new Exception("未找到名称以“Software_Url_”开头的文件夹");
                }

                var currentVersionDirectory = Path.Combine(softwareUrlDirectory, currentVersion);

                if (!Directory.Exists(currentVersionDirectory))
                {
                    Directory.CreateDirectory(currentVersionDirectory);
                }

                foreach (var directory in Directory.GetDirectories(softwareUrlDirectory))
                {
                    var directoryName = Path.GetFileName(directory);
                    if (directoryName != currentVersion)
                    {
                        foreach (var file in Directory.GetFiles(directory))
                        {
                            var fileName = Path.GetFileName(file);
                            File.Copy(file, Path.Combine(currentVersionDirectory, fileName), true);
                        }
                        Directory.Delete(directory, true);
                    }
                }
            }
        }

    }
}
