using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Windows;
using System.Configuration; // 引用System.Configuration命名空间以读取App.config文件中的设置
using System.ComponentModel; // 引用System.ComponentModel命名空间以使用INotifyPropertyChanged接口

namespace Software.ViewModels
{
    class Weather : INotifyPropertyChanged
    {
        private string _data_Province;
        public string Data_Province
        {
            get { return _data_Province; }
            set
            {
                _data_Province = value;
                OnPropertyChanged("Data_Province");
            }
        }

        private string _data_City;
        public string Data_City
        {
            get { return _data_City; }
            set
            {
                _data_City = value;
                OnPropertyChanged("Data_City");
            }
        }

        private string _data_Adcode;
        public string Data_Adcode
        {
            get { return _data_Adcode; }
            set
            {
                _data_Adcode = value;
                OnPropertyChanged("Data_Adcode");
            }
        }

        private string _data_Weather;
        public string Data_Weather
        {
            get { return _data_Weather; }
            set
            {
                _data_Weather = value;
                OnPropertyChanged("Data_Weather");
            }
        }

        private string _data_Temperature;
        public string Data_Temperature
        {
            get { return _data_Temperature; }
            set
            {
                _data_Temperature = value;
                OnPropertyChanged("Data_Temperature");
            }
        }

        private string _data_Winddirection;
        public string Data_Winddirection
        {
            get { return _data_Winddirection; }
            set
            {
                _data_Winddirection = value;
                OnPropertyChanged("Data_Winddirection");
            }
        }

        private string _data_Windpower;
        public string Data_Windpower
        {
            get { return _data_Windpower; }
            set
            {
                _data_Windpower = value;
                OnPropertyChanged("Data_Windpower");
            }
        }

        private string _data_Humidity;
        public string Data_Humidity
        {
            get { return _data_Humidity; }
            set
            {
                _data_Humidity = value;
                OnPropertyChanged("Data_Humidity");
            }
        }

        private string _data_Reporttime;
        public string Data_Reporttime
        {
            get { return _data_Reporttime; }
            set
            {
                _data_Reporttime = value;
                OnPropertyChanged("Data_Reporttime");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 定义一个异步方法来加载天气信息
        public async Task LoadAsync()
        {
            await City(city: "500000");
        }

        // 定义一个私有的异步方法来获取指定城市的天气信息
        private async Task City(string city)
        {
            // 从App.config文件中获取重试的次数和间隔时间
            int retryCount = 0;
            int maxRetryCount = int.Parse(ConfigurationManager.AppSettings["RetryCount"]);
            int retryDelay = int.Parse(ConfigurationManager.AppSettings["RetryDelay"]);

            // 如果网络不可用，等待一段时间后重试
            while (retryCount < maxRetryCount)
            {
                try
                {
                    if (!NetworkInterface.GetIsNetworkAvailable())
                    {
                        // No network connection, wait and retry
                        await Task.Delay(TimeSpan.FromSeconds(retryDelay));
                        retryCount++;
                        continue;
                    }

                    // 创建一个HttpClient对象来发送HTTP请求
                    var client = new HttpClient();
                    var request = new HttpRequestMessage();
                    request.RequestUri = new Uri($"https://restapi.amap.com/v3/weather/weatherInfo?key=71d6333d58f635ab3136a8955cec1e8c&city={city}");
                    request.Method = HttpMethod.Get;

                    // 发送HTTP请求并获取响应
                    var response = await client.SendAsync(request);
                    var result = await response.Content.ReadAsStringAsync();

                    // 解析响应内容并更新Weather类的属性
                    var lives = JObject.Parse(result)["lives"];
                    foreach (var item in lives)
                    {
                        Data_Province = item["province"].ToString();
                        Data_City = item["city"].ToString();
                        Data_Adcode = item["adcode"].ToString();
                        Data_Weather = item["weather"].ToString();
                        Data_Temperature = item["temperature"].ToString();
                        Data_Winddirection = item["winddirection"].ToString();
                        Data_Windpower = item["windpower"].ToString();
                        Data_Humidity = item["humidity"].ToString();
                        Data_Reporttime = item["reporttime"].ToString();
                    }

                    break; // 如果成功获取到天气信息，跳出循环
                }
                catch (Exception ex)
                {
                    // 如果发生异常，显示错误信息并等待一段时间后重试
                    MessageBox.Show($"天气信息获取失败：{ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(retryDelay));
                    retryCount++;
                }
            }
        }
    }
}
