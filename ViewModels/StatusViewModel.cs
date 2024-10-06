using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.ComponentModel;

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

        public StatusViewModel()
        {
            SeriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "内存使用",
                    Values = new ChartValues<double> { 0 }
                },
                new LineSeries
                {
                    Title = "CPU 使用率",
                    Values = new ChartValues<double> { 0 }
                }
            };
        }

        private void UpdateSeriesCollection()
        {
            SeriesCollection[0].Values.Add(Math.Round(MemoryUsage / (1024.0 * 1024.0), 2)); // 将内存使用转换为MB并保留两位小数
            SeriesCollection[1].Values.Add(Math.Round(CPUUsage, 2)); // 保留两位小数
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
