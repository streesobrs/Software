using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Software.ViewModels
{
    // 资源项类，用于展示资源详细信息
    public class ResourceItem : INotifyPropertyChanged
    {
        public string Type { get; set; }
        public string Path { get; set; }
        public double Size { get; set; }
        public DateTime LastModified { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // StatusViewModel 类用于管理系统状态相关数据，并实现属性变更通知以更新 UI
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
        // 存储系统版本
        private string _systemVersion;
        // 存储软件版本
        private string _softwareVersion;
        // 存储当前用户
        private string _currentUser;
        // 存储启动时间
        private DateTime _startTime;
        // 存储软件安装大小（MB）
        private double _applicationSize;
        // 存储资源文件夹大小（MB）
        private double _resourcesSize;
        // 存储浏览器缓存大小（MB）
        private double _browserCacheSize;
        // 存储日志文件大小（MB）
        private double _logsSize;
        // 存储临时文件大小（MB）
        private double _tempFilesSize;
        // 存储用户数据大小（MB）
        private double _userDataSize;
        // 存储资源项集合
        private List<ResourceItem> _resourceItems;
        // 存储选中的资源项
        private ResourceItem _selectedResourceItem;

        // 清理缓存命令
        private ICommand _cleanCacheCommand;
        // 刷新资源命令
        private ICommand _refreshResourcesCommand;
        // 删除资源命令
        private ICommand _deleteResourceCommand;

        // 计时器，用于定期更新系统状态
        private System.Timers.Timer _updateTimer;

        // MemoryUsage 属性
        public double MemoryUsage
        {
            get { return _memoryUsage; }
            set
            {
                SetProperty(ref _memoryUsage, value);
            }
        }

        // CPUUsage 属性
        public double CPUUsage
        {
            get { return _cpuUsage; }
            set
            {
                SetProperty(ref _cpuUsage, value);
            }
        }

        // Uptime 属性，实现属性变更通知
        public TimeSpan Uptime
        {
            get { return _uptime; }
            set
            {
                SetProperty(ref _uptime, value);
                Days = (int)value.TotalDays; // 更新天数
            }
        }

        // 用于获取运行时间天数的属性
        public int Days
        {
            get { return _days; }
            set
            {
                SetProperty(ref _days, value);
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

        // 系统版本属性
        public string SystemVersion
        {
            get => _systemVersion;
            set => SetProperty(ref _systemVersion, value);
        }

        // 软件版本属性
        public string SoftwareVersion
        {
            get => _softwareVersion;
            set => SetProperty(ref _softwareVersion, value);
        }

        // 当前用户属性
        public string CurrentUser
        {
            get => _currentUser;
            set => SetProperty(ref _currentUser, value);
        }

        // 启动时间属性
        public DateTime StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        // 软件安装大小属性
        public double ApplicationSize
        {
            get => _applicationSize;
            set => SetProperty(ref _applicationSize, value);
        }

        // 资源文件夹大小属性
        public double ResourcesSize
        {
            get => _resourcesSize;
            set => SetProperty(ref _resourcesSize, value);
        }

        // 浏览器缓存大小属性
        public double BrowserCacheSize
        {
            get => _browserCacheSize;
            set => SetProperty(ref _browserCacheSize, value);
        }

        // 日志文件大小属性
        public double LogsSize
        {
            get => _logsSize;
            set => SetProperty(ref _logsSize, value);
        }

        // 临时文件大小属性
        public double TempFilesSize
        {
            get => _tempFilesSize;
            set => SetProperty(ref _tempFilesSize, value);
        }

        // 用户数据大小属性
        public double UserDataSize
        {
            get => _userDataSize;
            set => SetProperty(ref _userDataSize, value);
        }

        // 资源项集合属性
        public List<ResourceItem> ResourceItems
        {
            get => _resourceItems;
            set => SetProperty(ref _resourceItems, value);
        }

        // 选中的资源项属性
        public ResourceItem SelectedResourceItem
        {
            get => _selectedResourceItem;
            set => SetProperty(ref _selectedResourceItem, value);
        }

        // 清理缓存命令属性
        public ICommand CleanCacheCommand
        {
            get
            {
                if (_cleanCacheCommand == null)
                {
                    _cleanCacheCommand = new RelayCommand(
                        param => CleanCache(),
                        param => true
                    );
                }
                return _cleanCacheCommand;
            }
        }

        // 刷新资源命令属性
        public ICommand RefreshResourcesCommand
        {
            get
            {
                if (_refreshResourcesCommand == null)
                {
                    _refreshResourcesCommand = new RelayCommand(
                        param => RefreshResources(),
                        param => true
                    );
                }
                return _refreshResourcesCommand;
            }
        }

        // 删除资源命令属性
        public ICommand DeleteResourceCommand
        {
            get
            {
                if (_deleteResourceCommand == null)
                {
                    _deleteResourceCommand = new RelayCommand(
                        param => DeleteResource(param),
                        param => param is ResourceItem
                    );
                }
                return _deleteResourceCommand;
            }
        }

        // 构造函数，初始化系统状态数据
        public StatusViewModel()
        {
            UpdateDiskSpace();
            NetworkStatus = GetNetworkStatus();

            // 初始化其他系统信息
            SystemVersion = Environment.OSVersion.ToString();
            SoftwareVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            CurrentUser = Environment.UserName;
            StartTime = DateTime.Now; // 记录启动时间

            // 初始化资源信息
            RefreshResources();

            // 启动计时器，定期更新系统状态
            _updateTimer = new System.Timers.Timer(1000); // 每秒更新一次
            _updateTimer.Elapsed += (sender, e) => UpdateSystemStatus();
            _updateTimer.Start();
        }

        // 更新系统状态
        private void UpdateSystemStatus()
        {
            // 模拟更新内存和CPU使用情况
            Random random = new Random();
            App.Current.Dispatcher.Invoke(() =>
            {
                MemoryUsage = Math.Round(512 + random.NextDouble() * 1024, 2);
                CPUUsage = Math.Round(random.NextDouble() * 100, 2);
                Uptime = DateTime.Now - StartTime;
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

        // 刷新资源信息
        private void RefreshResources()
        {
            try
            {
                // 获取应用程序目录
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;

                // 计算应用程序大小
                ApplicationSize = CalculateDirectorySize(appDirectory) / (1024.0);

                // 计算资源文件夹大小
                string resourcesPath = Path.Combine(appDirectory, "Resources");
                ResourcesSize = Directory.Exists(resourcesPath) ? CalculateDirectorySize(resourcesPath) / (1024.0) : 0;

                // 计算浏览器缓存大小
                string browserCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrowserCache");
                BrowserCacheSize = Directory.Exists(browserCachePath) ? CalculateDirectorySize(browserCachePath) / (1024.0) : 0;

                // 计算日志文件大小
                string logsPath = Path.Combine(appDirectory, "Logs");
                LogsSize = Directory.Exists(logsPath) ? CalculateDirectorySize(logsPath) / (1024.0) : 0;

                // 计算临时文件大小
                string tempPath = Path.GetTempPath();
                TempFilesSize = CalculateDirectorySize(tempPath) / (1024.0);

                // 计算用户数据大小
                string userDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyApp");
                UserDataSize = Directory.Exists(userDataPath) ? CalculateDirectorySize(userDataPath) / (1024.0) : 0;

                // 收集详细资源项
                List<ResourceItem> items = new List<ResourceItem>();

                // 添加日志文件
                if (Directory.Exists(logsPath))
                {
                    items.AddRange(Directory.GetFiles(logsPath)
                        .Select(file => new ResourceItem
                        {
                            Type = "日志文件",
                            Path = file,
                            Size = new FileInfo(file).Length / (1024.0),
                            LastModified = File.GetLastWriteTime(file)
                        }));
                }

                // 添加临时文件（限制显示数量）
                if (Directory.Exists(tempPath))
                {
                    items.AddRange(Directory.GetFiles(tempPath)
                        .Select(file => new ResourceItem
                        {
                            Type = "临时文件",
                            Path = file,
                            Size = new FileInfo(file).Length / (1024.0),
                            LastModified = File.GetLastWriteTime(file)
                        })
                        .Take(20)); // 限制显示20个临时文件
                }

                // 添加浏览器缓存文件（限制显示数量）
                if (Directory.Exists(browserCachePath))
                {
                    items.AddRange(Directory.GetFiles(browserCachePath)
                        .Select(file => new ResourceItem
                        {
                            Type = "浏览器缓存",
                            Path = file,
                            Size = new FileInfo(file).Length / (1024.0),
                            LastModified = File.GetLastWriteTime(file)
                        })
                        .Take(20)); // 限制显示20个缓存文件
                }

                ResourceItems = items.OrderByDescending(item => item.Size).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"刷新资源信息时出错: {ex.Message}");
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

                // 计算文件大小
                foreach (string file in Directory.GetFiles(directoryPath))
                {
                    try
                    {
                        size += new FileInfo(file).Length;
                    }
                    catch (Exception)
                    {
                        // 忽略无法访问的文件
                    }
                }

                // 递归计算子目录大小
                foreach (string subDirectory in Directory.GetDirectories(directoryPath))
                {
                    try
                    {
                        size += CalculateDirectorySize(subDirectory);
                    }
                    catch (Exception)
                    {
                        // 忽略无法访问的目录
                    }
                }

                return size;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // 清理缓存
        private void CleanCache()
        {
            try
            {
                // 清理浏览器缓存
                string browserCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrowserCache");
                DeleteDirectoryContent(browserCachePath);

                // 清理临时文件
                string tempPath = Path.GetTempPath();
                DeleteDirectoryContent(tempPath);

                // 刷新资源信息
                RefreshResources();

                Console.WriteLine("缓存清理完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清理缓存时出错: {ex.Message}");
            }
        }

        // 删除目录内容
        private void DeleteDirectoryContent(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                return;

            try
            {
                foreach (string file in Directory.GetFiles(directoryPath))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch (Exception)
                    {
                        // 忽略无法删除的文件
                    }
                }

                foreach (string subDirectory in Directory.GetDirectories(directoryPath))
                {
                    try
                    {
                        DeleteDirectoryContent(subDirectory);
                        Directory.Delete(subDirectory, true);
                    }
                    catch (Exception)
                    {
                        // 忽略无法删除的目录
                    }
                }
            }
            catch (Exception)
            {
                // 忽略异常
            }
        }

        // 删除单个资源
        private void DeleteResource(object parameter)
        {
            if (parameter is ResourceItem resourceItem)
            {
                try
                {
                    if (File.Exists(resourceItem.Path))
                    {
                        File.SetAttributes(resourceItem.Path, FileAttributes.Normal);
                        File.Delete(resourceItem.Path);

                        // 从列表中移除
                        ResourceItems.Remove(resourceItem);
                        ResourceItems = new List<ResourceItem>(ResourceItems); // 触发属性变更通知

                        // 刷新资源大小
                        RefreshResources();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"删除资源时出错: {ex.Message}");
                }
            }
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

    // 简单的命令实现类
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }
    }
}