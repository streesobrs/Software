using Software.其他界面;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Software
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
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

            base.OnStartup(e);
        }
    }
}
