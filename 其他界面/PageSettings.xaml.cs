using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using Software;

namespace Software.其他界面
{
    /// <summary>
    /// PageSettings.xaml 的交互逻辑
    /// </summary>
    public partial class PageSettings : Page
    {
        public PageSettings()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // 读取配置文件
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            // 读取"EnableCounting"的值
            bool enableCounting = bool.Parse(ConfigurationManager.AppSettings["EnableCounting"]);
            //读取“updatePath”的值
            string updatePath = config.AppSettings.Settings["updatePath"].Value;
            string updateLogPath = config.AppSettings.Settings["UpdateLogUrl"].Value;

            // 设置CheckBox的状态
            EnableCountingCheckBox.IsChecked = enableCounting;
            // 设置TextBox的内容
            Update_IP_address.Text = updatePath;
            Update_Log_IP_address.Text = updateLogPath;
        }

        public void HandleLaunchCount()
        {
            // 读取"EnableCounting"的值
            bool enableCounting = bool.Parse(ConfigurationManager.AppSettings["EnableCounting"]);

            // 设置CheckBox的状态
            EnableCountingCheckBox.IsChecked = enableCounting;

            // 如果启用计数，则执行计数逻辑
            if (enableCounting)
            {
                // 读取并增加启动次数
                int launchCount = int.Parse(ConfigurationManager.AppSettings["LaunchCount"]) + 1;

                // 更新启动次数
                UpdateLaunchCount(launchCount);

                // 显示启动次数
                MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
                mainWindow.LaunchCount.Content = $"软件已启动 {launchCount} 次 ";
            }
        }

        private void UpdateLaunchCount(int launchCount)
        {
            // 打开配置文件
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            // 更新启动次数
            config.AppSettings.Settings["LaunchCount"].Value = launchCount.ToString();

            // 保存配置文件
            config.Save(ConfigurationSaveMode.Modified);

            // 刷新配置文件
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void UpdateEnableCounting(bool enableCounting)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["EnableCounting"].Value = enableCounting.ToString();
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void EnableCountingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            UpdateEnableCounting(true);
            mainWindow.LaunchCount.Visibility = Visibility.Visible;
        }

        private void EnableCountingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            UpdateEnableCounting(false);
            mainWindow.LaunchCount.Visibility = Visibility.Hidden;
        }

        private void Update_IP_address_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = Update_IP_address.Text;
            // 保存到配置文件
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["UpdatePath"].Value = text;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void Update_Log_IP_address_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = Update_Log_IP_address.Text;
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["UpdateLogUrl"].Value = text;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void RadioButton_Click_English(object sender, RoutedEventArgs e)
        {
            SaveCultureInfo("en-US");
        }

        private void RadioButton_Click_Chinese(object sender, RoutedEventArgs e)
        {
            SaveCultureInfo("zh-CN");
        }

        private void SaveCultureInfo(string cultureName)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["Culture"].Value = cultureName;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {

        }
    }
}
