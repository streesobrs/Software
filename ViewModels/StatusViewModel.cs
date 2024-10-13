using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Software.ViewModels
{
    public class StatusViewModel : INotifyPropertyChanged
    {
        private double _memoryUsage;
        private double _cpuUsage;
        private TimeSpan _uptime;
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
        }

        private async void UpdateSeriesCollection()
        {
            await Task.Run(() =>
            {
                if (SeriesCollection[0].Values.Count > 100)
                {
                    SeriesCollection[0].Values.RemoveAt(0);
                }
                if (SeriesCollection[1].Values.Count > 100)
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
                });
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
        }
    }
}
