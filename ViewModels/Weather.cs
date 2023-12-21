using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel; // 引用System.ComponentModel命名空间以使用INotifyPropertyChanged接口
using System.Configuration; // 引用System.Configuration命名空间以读取App.config文件中的设置
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;

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

        public async Task LoadAsync()
        {
            string city = ConfigurationManager.AppSettings["adcode"];
            if (string.IsNullOrEmpty(city))
            {
                city = await GetCityCodeAsync();
                // 保存到配置文件
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings["adcode"].Value = city;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            await City(city);
        }

        private async Task<string> GetCityCodeAsync()
        {
            try
            {
                var client = new HttpClient();
                var request = new HttpRequestMessage
                {
                    RequestUri = new Uri("https://restapi.amap.com/v3/ip?output=json&key=71d6333d58f635ab3136a8955cec1e8c"),
                    Method = HttpMethod.Get
                };
                var response = await client.SendAsync(request);
                var result = await response.Content.ReadAsStringAsync();
                var jsonResult = JObject.Parse(result);
                if (jsonResult["city"] is JArray cityArray && cityArray.Count == 0)
                {
                    return "500000";
                }
                return jsonResult["adcode"].ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"错误代码为：{ex.Message}"+"\n默认返回了个值");
                return "500000";
            }
        }

        private async Task City(string city)
        {
            int retryCount = 0;
            int maxRetryCount = int.Parse(ConfigurationManager.AppSettings["RetryCount"]);
            int retryDelay = int.Parse(ConfigurationManager.AppSettings["RetryDelay"]);

            while (retryCount < maxRetryCount)
            {
                try
                {
                    if (!NetworkInterface.GetIsNetworkAvailable())
                    {
                        await Task.Delay(TimeSpan.FromSeconds(retryDelay));
                        retryCount++;
                        continue;
                    }

                    var client = new HttpClient();
                    var request = new HttpRequestMessage();
                    request.RequestUri = new Uri($"https://restapi.amap.com/v3/weather/weatherInfo?key=71d6333d58f635ab3136a8955cec1e8c&city={city}");
                    request.Method = HttpMethod.Get;

                    var response = await client.SendAsync(request);
                    var result = await response.Content.ReadAsStringAsync();

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

                    break;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"天气信息获取失败：{ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(retryDelay));
                    retryCount++;
                }
            }
        }
    }
}
