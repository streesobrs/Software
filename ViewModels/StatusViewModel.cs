using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace Software.ViewModels
{
    // 资源项类，用于展示资源详细信息
    public class ResourceItem : INotifyPropertyChanged
    {
        public string Type { get; set; }
        public string Path { get; set; }
        public double Size { get; set; } // 单位：KB
        public DateTime LastModified { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // 资源扫描配置类
    public class ResourceScanConfig
    {
        public string TypeName { get; set; }        // 显示的资源类型名称（如"日志文件"）
        public string Path { get; set; }           // 扫描路径
        public Func<string, bool> FileFilter { get; set; }  // 文件筛选条件
        public int MaxDisplayCount { get; set; }   // 最大显示数量
        public bool Recursive { get; set; } = true; // 是否递归扫描子目录
        public string SizePropertyName { get; set; } // 对应StatusViewModel中的大小属性名
    }

    // StatusViewModel 类用于管理系统状态相关数据
    public class StatusViewModel : INotifyPropertyChanged
    {
        // 核心计时器：使用Stopwatch实现精确计时
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        // CPU性能计数器
        private readonly PerformanceCounter _cpuCounter =
            new PerformanceCounter("Processor", "% Processor Time", "_Total");

        // 内存性能计数器
        private readonly PerformanceCounter _memoryCounter =
            new PerformanceCounter("Memory", "Available MBytes");

        // 系统状态属性
        private TimeSpan _uptime;
        private int _days;
        private double _cpuUsage;
        private double _memoryUsage;
        private double _diskSpace;
        private double _totalDiskSpace;
        private double _usedDiskSpace;
        private string _networkStatus;
        private string _systemVersion;
        private string _softwareVersion;
        private string _currentUser;
        private DateTime _startTime;
        private double _applicationSize;
        private double _resourcesSize;
        private double _browserCacheSize;
        private double _logsSize;
        private double _tempFilesSize;
        private double _userDataSize;
        private List<ResourceItem> _resourceItems;
        private ResourceItem _selectedResourceItem;

        // 资源扫描配置列表（可扩展）
        private readonly List<ResourceScanConfig> _resourceConfigs = new List<ResourceScanConfig>();

        // 命令
        private ICommand _cleanCacheCommand;
        private ICommand _refreshResourcesCommand;
        private ICommand _deleteResourceCommand;

        // 构造函数
        public StatusViewModel()
        {
            // 初始化系统信息
            UpdateDiskSpace();
            NetworkStatus = GetNetworkStatus();
            SystemVersion = Environment.OSVersion.ToString();
            SoftwareVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            CurrentUser = Environment.UserName;
            StartTime = DateTime.Now;

            // 初始化资源扫描配置
            InitializeResourceConfigs();

            // 初始化资源信息
            RefreshResources();

            // 启动性能监控定时器
            var timer = new System.Timers.Timer(1000);
            timer.Elapsed += (sender, e) => UpdateSystemStatus();
            timer.Start();

            // 预热CPU计数器
            _cpuCounter.NextValue();
        }

        // 初始化资源扫描配置
        private void InitializeResourceConfigs()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;

            _resourceConfigs.Add(new ResourceScanConfig
            {
                TypeName = "应用程序文件",
                Path = appDir,
                FileFilter = file => new FileInfo(file).Length > 1024, // >1KB
                MaxDisplayCount = 20,
                Recursive = false,
                SizePropertyName = nameof(ApplicationSize)
            });

            _resourceConfigs.Add(new ResourceScanConfig
            {
                TypeName = "日志文件",
                Path = Path.Combine(appDir, "Logs"),
                FileFilter = file => new FileInfo(file).Length > 1024, // >1KB
                MaxDisplayCount = 50,
                SizePropertyName = nameof(LogsSize)
            });

            _resourceConfigs.Add(new ResourceScanConfig
            {
                TypeName = "用户数据",
                Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyApp"),
                FileFilter = file => new FileInfo(file).Length > 1024 * 5, // >5KB
                MaxDisplayCount = 15,
                SizePropertyName = nameof(UserDataSize)
            });
        }

        // 系统状态更新方法
        private void UpdateSystemStatus()
        {
            try
            {
                var cpuUsage = _cpuCounter.NextValue();
                var memoryUsage = _memoryCounter.NextValue();
                var uptime = _stopwatch.Elapsed;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Uptime = uptime;
                    CPUUsage = Math.Round(cpuUsage, 2);
                    MemoryUsage = Math.Round(memoryUsage, 2);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新系统状态时出错: {ex.Message}");
            }
        }

        // 计算目录大小
        private long CalculateDirectorySize(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    return 0;

                long size = 0;
                foreach (var file in Directory.GetFiles(directoryPath))
                {
                    try { size += new FileInfo(file).Length; }
                    catch { /* 忽略无法访问的文件 */ }
                }

                foreach (var subDir in Directory.GetDirectories(directoryPath))
                {
                    try { size += CalculateDirectorySize(subDir); }
                    catch { /* 忽略无法访问的目录 */ }
                }

                return size;
            }
            catch { return 0; }
        }

        // 刷新资源信息（核心可扩展方法）
        public void RefreshResources()
        {
            try
            {
                var allResources = new List<ResourceItem>();

                // 遍历所有配置，扫描资源
                foreach (var config in _resourceConfigs)
                {
                    try
                    {
                        if (!Directory.Exists(config.Path))
                            continue;

                        // 获取所有文件
                        var searchOption = config.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                        var files = Directory.GetFiles(config.Path, "*.*", searchOption);

                        // 应用筛选规则并转换为ResourceItem
                        var resources = files
                            .Where(file => config.FileFilter(file))
                            .Select(file => new ResourceItem
                            {
                                Type = config.TypeName,
                                Path = file,
                                Size = new FileInfo(file).Length / (1024.0), // KB
                                LastModified = File.GetLastWriteTime(file)
                            })
                            .OrderByDescending(item => item.Size)
                            .Take(config.MaxDisplayCount)
                            .ToList();

                        allResources.AddRange(resources);

                        // 更新对应大小属性
                        UpdateSizeProperty(config.SizePropertyName, CalculateDirectorySize(config.Path));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"扫描 {config.TypeName} 时出错: {ex.Message}");
                    }
                }

                // 按大小排序并更新列表
                ResourceItems = allResources.OrderByDescending(item => item.Size).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刷新资源信息时出错: {ex.Message}");
            }
        }

        // 动态更新大小属性
        private void UpdateSizeProperty(string propertyName, long sizeInBytes)
        {
            if (string.IsNullOrEmpty(propertyName))
                return;

            double sizeInMB = sizeInBytes / (1024.0 * 1024.0);

            // 使用反射更新对应的属性
            var property = GetType().GetProperty(propertyName);
            if (property != null && property.CanWrite && property.PropertyType == typeof(double))
            {
                property.SetValue(this, Math.Round(sizeInMB, 2));
            }
        }

        // 清理缓存
        public void CleanCache()
        {
            try
            {
                // 清理临时文件
                string tempPath = Path.GetTempPath();
                DeleteDirectoryContent(tempPath);

                // 清理日志文件（可选）
                string logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                DeleteDirectoryContent(logsPath);

                // 刷新资源
                RefreshResources();
                Debug.WriteLine("缓存清理完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清理缓存时出错: {ex.Message}");
            }
        }

        // 删除目录内容
        private void DeleteDirectoryContent(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return;

            try
            {
                foreach (var file in Directory.GetFiles(directoryPath))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch { /* 忽略无法删除的文件 */ }
                }

                foreach (var subDir in Directory.GetDirectories(directoryPath))
                {
                    try
                    {
                        DeleteDirectoryContent(subDir);
                        Directory.Delete(subDir, true);
                    }
                    catch { /* 忽略无法删除的目录 */ }
                }
            }
            catch { /* 忽略异常 */ }
        }

        // 删除单个资源
        public void DeleteResource(ResourceItem resourceItem)
        {
            if (resourceItem == null || !File.Exists(resourceItem.Path)) return;

            try
            {
                File.SetAttributes(resourceItem.Path, FileAttributes.Normal);
                File.Delete(resourceItem.Path);

                ResourceItems.Remove(resourceItem);
                ResourceItems = new List<ResourceItem>(ResourceItems); // 触发属性变更通知
                RefreshResources();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"删除资源时出错: {ex.Message}");
            }
        }

        // 更新磁盘空间信息
        private void UpdateDiskSpace()
        {
            try
            {
                string driveLetter = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
                DriveInfo drive = new DriveInfo(driveLetter);
                TotalDiskSpace = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                DiskSpace = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                UsedDiskSpace = TotalDiskSpace - DiskSpace;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取磁盘空间时出错: {ex.Message}");
                TotalDiskSpace = 0;
                DiskSpace = 0;
                UsedDiskSpace = 0;
            }
        }

        // 获取网络状态
        private string GetNetworkStatus()
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable() ? "已连接" : "未连接";
            }
            catch { return "未知"; }
        }

        // 动态添加资源扫描配置
        public void AddResourceScanConfig(ResourceScanConfig config)
        {
            _resourceConfigs.Add(config);
            // 立即刷新资源
            RefreshResources();
        }

        // 属性实现（简化版）
        public TimeSpan Uptime { get => _uptime; private set { if (value >= _uptime) { _uptime = value; OnPropertyChanged(); Days = (int)value.TotalDays; } } }
        public int Days { get => _days; private set => SetProperty(ref _days, value); }
        public double CPUUsage { get => _cpuUsage; private set => SetProperty(ref _cpuUsage, value); }
        public double MemoryUsage { get => _memoryUsage; private set => SetProperty(ref _memoryUsage, value); }
        public double DiskSpace { get => _diskSpace; set { SetProperty(ref _diskSpace, value); OnDiskSpaceOrTotalDiskSpaceChanged(); } }
        public double TotalDiskSpace { get => _totalDiskSpace; set { SetProperty(ref _totalDiskSpace, value); OnDiskSpaceOrTotalDiskSpaceChanged(); } }
        public double UsedDiskSpace { get => _usedDiskSpace; private set => SetProperty(ref _usedDiskSpace, value); }
        public string NetworkStatus { get => _networkStatus; set => SetProperty(ref _networkStatus, value); }
        public string SystemVersion { get => _systemVersion; set => SetProperty(ref _systemVersion, value); }
        public string SoftwareVersion { get => _softwareVersion; set => SetProperty(ref _softwareVersion, value); }
        public string CurrentUser { get => _currentUser; set => SetProperty(ref _currentUser, value); }
        public DateTime StartTime { get => _startTime; set => SetProperty(ref _startTime, value); }
        public double ApplicationSize { get => _applicationSize; set => SetProperty(ref _applicationSize, value); }
        public double ResourcesSize { get => _resourcesSize; set => SetProperty(ref _resourcesSize, value); }
        public double BrowserCacheSize { get => _browserCacheSize; set => SetProperty(ref _browserCacheSize, value); }
        public double LogsSize { get => _logsSize; set => SetProperty(ref _logsSize, value); }
        public double TempFilesSize { get => _tempFilesSize; set => SetProperty(ref _tempFilesSize, value); }
        public double UserDataSize { get => _userDataSize; set => SetProperty(ref _userDataSize, value); }
        public List<ResourceItem> ResourceItems { get => _resourceItems; set => SetProperty(ref _resourceItems, value); }
        public ResourceItem SelectedResourceItem { get => _selectedResourceItem; set => SetProperty(ref _selectedResourceItem, value); }

        // 命令实现
        public ICommand CleanCacheCommand => _cleanCacheCommand ??= new RelayCommand(_ => CleanCache(), _ => true);
        public ICommand RefreshResourcesCommand => _refreshResourcesCommand ??= new RelayCommand(_ => RefreshResources(), _ => true);
        public ICommand DeleteResourceCommand => _deleteResourceCommand ??= new RelayCommand(param => DeleteResource(param as ResourceItem), param => param is ResourceItem);

        // INotifyPropertyChanged 实现
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        private void OnDiskSpaceOrTotalDiskSpaceChanged() { UsedDiskSpace = TotalDiskSpace - DiskSpace; OnPropertyChanged(nameof(UsedDiskSpace)); }
    }

    // 命令实现类
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);
    }
}