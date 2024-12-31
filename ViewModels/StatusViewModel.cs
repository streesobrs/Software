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
    // StatusViewModel 类用于管理系统状态相关数据，并实现属性变更通知以更新 UI，与图表展示相关
    public class StatusViewModel : INotifyPropertyChanged
    {
        // 存储内存使用率
        private double _memoryUsage;
        // 存储 CPU 使用率
        private double _cpuUsage;
        // 存储系统运行时间
        private TimeSpan _uptime;
        // 存储系统运行天数
        private int _days;
        // 存储磁盘可用空间大小（GB）
        private double _diskSpace;
        // 存储磁盘总空间大小（GB）
        private double _totalDiskSpace;
        // 存储磁盘已使用空间大小（GB）
        private double _usedDiskSpace;
        // 存储网络连接状态
        private string _networkStatus;
        // 存储图表的数据系列集合
        private SeriesCollection _seriesCollection;

        // 复用的内存使用和 CPU 使用数据点对象
        private DateTimePoint _memoryUsagePoint;
        private DateTimePoint _cpuUsagePoint;

        // MemoryUsage 属性，设置时更新图表数据
        public double MemoryUsage
        {
            get { return _memoryUsage; }
            set
            {
                SetProperty(ref _memoryUsage, value);
                UpdateSeriesCollection();
            }
        }

        // CPUUsage 属性，设置时更新图表数据
        public double CPUUsage
        {
            get { return _cpuUsage; }
            set
            {
                SetProperty(ref _cpuUsage, value);
                UpdateSeriesCollection();
            }
        }

        // Uptime 属性，实现属性变更通知
        public TimeSpan Uptime
        {
            get { return _uptime; }
            set { SetProperty(ref _uptime, value); }
        }

        // 新增用于获取运行时间天数的属性
        public int Days
        {
            get { return (int)_uptime.TotalDays; }
            set
            {
                if (_days != value)
                {
                    _days = value;
                    OnPropertyChanged(nameof(Days));
                }
            }
        }

        // DiskSpace 属性，设置时触发 UsedDiskSpace 重新计算和通知
        public double DiskSpace
        {
            get => _diskSpace;
            set
            {
                SetProperty(ref _diskSpace, value);
                OnDiskSpaceOrTotalDiskSpaceChanged();
            }
        }

        // TotalDiskSpace 属性，设置时触发 UsedDiskSpace 重新计算和通知
        public double TotalDiskSpace
        {
            get => _totalDiskSpace;
            set
            {
                SetProperty(ref _totalDiskSpace, value);
                OnDiskSpaceOrTotalDiskSpaceChanged();
            }
        }

        // UsedDiskSpace 属性，实现属性变更通知
        public double UsedDiskSpace
        {
            get => _usedDiskSpace;
            set { SetProperty(ref _usedDiskSpace, value); }
        }

        // NetworkStatus 属性，实现属性变更通知
        public string NetworkStatus
        {
            get => _networkStatus;
            set
            {
                SetProperty(ref _networkStatus, value);
            }
        }

        // SeriesCollection 属性，实现属性变更通知
        public SeriesCollection SeriesCollection
        {
            get { return _seriesCollection; }
            set { SetProperty(ref _seriesCollection, value); }
        }

        // 用于格式化图表坐标轴数值显示的委托属性
        public Func<double, string> Formatter { get; set; }

        // 构造函数，初始化系统状态数据和图表相关内容
        public StatusViewModel()
        {
            SeriesCollection = new SeriesCollection
            {
                new LineSeries { Title = "内存使用（MB）", Values = new ChartValues<DateTimePoint>() },
                new LineSeries { Title = "CPU 使用率（%）", Values = new ChartValues<DateTimePoint>() }
            };

            _memoryUsagePoint = new DateTimePoint(DateTime.Now, 0);
            _cpuUsagePoint = new DateTimePoint(DateTime.Now, 0);

            Formatter = value => new DateTime((long)value).ToString("HH:mm:ss");

            UpdateDiskSpace();
            NetworkStatus = GetNetworkStatus();
            AddInitialDataPoints();
            UpdateSeriesCollection();
        }

        // 更新图表数据系列集合，控制数据点数量，复用数据点对象并在 UI 线程更新
        private async void UpdateSeriesCollection()
        {
            await Task.Run(() =>
            {
                var memorySeries = SeriesCollection[0];
                var cpuSeries = SeriesCollection[1];

                if (memorySeries.Values.Count > 30)
                {
                    if (memorySeries.Values.Count > 0)
                    {
                        memorySeries.Values.RemoveAt(0);
                    }
                }

                if (cpuSeries.Values.Count > 30)
                {
                    if (cpuSeries.Values.Count > 0)
                    {
                        cpuSeries.Values.RemoveAt(0);
                    }
                }

                _memoryUsagePoint.DateTime = DateTime.Now;
                _memoryUsagePoint.Value = Math.Round(MemoryUsage, 2);
                _cpuUsagePoint.DateTime = DateTime.Now;
                _cpuUsagePoint.Value = Math.Round(CPUUsage, 2);

                App.Current.Dispatcher.Invoke(() =>
                {
                    memorySeries.Values.Add(_memoryUsagePoint);
                    cpuSeries.Values.Add(_cpuUsagePoint);
                });
            });
        }

        // 获取磁盘空间信息，处理异常并更新磁盘空间属性
        private void UpdateDiskSpace()
        {
            try
            {
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string applicationDriveLetter = Path.GetPathRoot(appDirectory);
                DriveInfo drive = new DriveInfo(applicationDriveLetter);
                TotalDiskSpace = drive.TotalSize / (1024.0 * 1024 * 1024);
                DiskSpace = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"磁盘空间获取出现异常: {ex.Message}");
                TotalDiskSpace = 0;
                DiskSpace = 0;
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"磁盘空间获取出现异常: {ex.Message}");
                TotalDiskSpace = 0;
                DiskSpace = 0;
            }
            finally
            {
                UsedDiskSpace = TotalDiskSpace - DiskSpace;
            }
        }

        // 获取网络状态，处理异常并返回合理状态表示
        private string GetNetworkStatus()
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable() ? "已连接" : "未连接";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"网络状态获取出现异常: {ex.Message}");
                return "未知";
            }
        }

        // 给图表数据系列添加初始数据点
        private void AddInitialDataPoints()
        {
            var memorySeries = SeriesCollection[0];
            memorySeries.Values.Add(new DateTimePoint(DateTime.Now, 0));
            memorySeries.Values.Add(new DateTimePoint(DateTime.Now, 0));

            var cpuSeries = SeriesCollection[1];
            cpuSeries.Values.Add(new DateTimePoint(DateTime.Now, 0));
            cpuSeries.Values.Add(new DateTimePoint(DateTime.Now, 0));
        }

        // 处理磁盘空间属性变更，重新计算并通知 UsedDiskSpace 变化
        private void OnDiskSpaceOrTotalDiskSpaceChanged()
        {
            UsedDiskSpace = TotalDiskSpace - DiskSpace;
            OnPropertyChanged(nameof(UsedDiskSpace));
        }

        // INotifyPropertyChanged 接口事件
        public event PropertyChangedEventHandler PropertyChanged;

        // 触发属性变更通知的辅助方法
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 设置属性值并触发变更通知的辅助方法
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