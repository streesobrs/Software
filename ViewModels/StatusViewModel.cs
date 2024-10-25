using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;
using System;
using System.ComponentModel;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Software.ViewModels
{
    public class StatusViewModel : INotifyPropertyChanged
    {
        private double _memoryUsage;
        private double _cpuUsage;
        private TimeSpan _uptime;
        private double diskSpace;
        private double totalDiskSpace;
        private double usedDiskSpace;
        private string networkStatus;
        private SeriesCollection _seriesCollection;

        public double MemoryUsage
        {
            get { return _memoryUsage; }
            set
            {
                SetProperty(ref _memoryUsage, value);
                UpdateSeriesCollection();
            }
        }

        public double CPUUsage
        {
            get { return _cpuUsage; }
            set
            {
                SetProperty(ref _cpuUsage, value);
                UpdateSeriesCollection();
            }
        }

        public TimeSpan Uptime
        {
            get { return _uptime; }
            set { SetProperty(ref _uptime, value); }
        }

        public double DiskSpace
        {
            get => diskSpace;
            set
            {
                diskSpace = value;
                OnPropertyChanged(nameof(DiskSpace));
            }
        }

        public double TotalDiskSpace
        {
            get => totalDiskSpace;
            set
            {
                totalDiskSpace = value;
                OnPropertyChanged(nameof(TotalDiskSpace));
            }
        }

        public double UsedDiskSpace
        {
            get => usedDiskSpace;
            set
            {
                usedDiskSpace = value;
                OnPropertyChanged(nameof(UsedDiskSpace));
            }
        }

        public string NetworkStatus
        {
            get => networkStatus;
            set
            {
                networkStatus = value;
                OnPropertyChanged(nameof(NetworkStatus));
            }
        }

        public SeriesCollection SeriesCollection
        {
            get { return _seriesCollection; }
            set { SetProperty(ref _seriesCollection, value); }
        }

        public Func<double, string> Formatter { get; set; }

        public StatusViewModel()
        {
            SeriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "内存使用（MB）",
                    Values = new ChartValues<DateTimePoint> { new DateTimePoint(DateTime.Now, 0) }
                },
                new LineSeries
                {
                    Title = "CPU 使用率（%）",
                    Values = new ChartValues<DateTimePoint> { new DateTimePoint(DateTime.Now, 0) }
                }
            };

            Formatter = value => new DateTime((long)value).ToString("HH:mm:ss");

            // 初始化磁盘空间和网络状态
            UpdateDiskSpace();
            NetworkStatus = GetNetworkStatus();
        }

        private async void UpdateSeriesCollection()
        {
            await Task.Run(() =>
            {
                if (SeriesCollection[0].Values.Count > 30)
                {
                    SeriesCollection[0].Values.RemoveAt(0);
                }
                if (SeriesCollection[1].Values.Count > 30)
                {
                    SeriesCollection[1].Values.RemoveAt(0);
                }

                var memoryUsage = new DateTimePoint(DateTime.Now, Math.Round(MemoryUsage, 2));
                var cpuUsage = new DateTimePoint(DateTime.Now, Math.Round(CPUUsage, 2));

                // 使用 Dispatcher 确保数据更新在UI线程上执行
                App.Current.Dispatcher.Invoke(() =>
                {
                    SeriesCollection[0].Values.Add(memoryUsage);
                    SeriesCollection[1].Values.Add(cpuUsage);

                    // 更新磁盘空间和网络状态
                    UpdateDiskSpace();
                    NetworkStatus = GetNetworkStatus();
                });
            });
        }

        private void UpdateDiskSpace()
        {
            // 获取当前应用程序所在目录
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            // 获取盘符
            string driveLetter = Path.GetPathRoot(appDirectory);
            // 获取磁盘空间的逻辑
            DriveInfo drive = new DriveInfo(driveLetter);
            TotalDiskSpace = drive.TotalSize / (1024.0 * 1024 * 1024); // 返回GB
            DiskSpace = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024); // 返回GB
            UsedDiskSpace = TotalDiskSpace - DiskSpace;
        }

        private string GetNetworkStatus()
        {
            // 获取网络状态的逻辑
            return NetworkInterface.GetIsNetworkAvailable() ? "已连接" : "未连接";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
        }
    }
}
