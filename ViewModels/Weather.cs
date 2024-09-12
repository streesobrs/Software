using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using Serilog;
using Software.Models;
using System;
using System.ComponentModel; // 引用System.ComponentModel命名空间以使用INotifyPropertyChanged接口
using System.Configuration; // 引用System.Configuration命名空间以读取App.config文件中的设置
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace Software.ViewModels
{
    class Weather : INotifyPropertyChanged
    {
        string databasePath = DatabaseHelper.GetDatabasePath();

        private ILogger logger;

        public ILogger MyLoger
        {
            get
            {
                if (logger == null)
                {
                    logger = Log.ForContext<Weather>();
                }
                return logger;
            }
        }

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

        private string _data_Refreshtime;
        public string Data_Refreshtime
        {
            get { return _data_Refreshtime; }
            set
            {
                _data_Refreshtime = value;
                OnPropertyChanged("Data_Refreshtime");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public async Task RefreshAsync()
        {
            await LoadAsync();
            // 从文件中读取天气数据并更新UI
            await LoadDataAsync("resources\\weather.json");
        }

        /// <summary>
        /// 从数据库中读取 adcode
        /// </summary>
        /// <param name="dbPath">数据库路径</param>
        /// <returns>adcode 值</returns>
        private string GetAdcodeFromDatabase(string dbPath)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    string query = "SELECT Value FROM Settings WHERE Key = 'adcode';";
                    var command = new SqliteCommand(query, connection);
                    return command.ExecuteScalar()?.ToString();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("读取adcode时发生错误:{error}", ex.ToString());
                MessageBox.Show("读取adcode时发生错误: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 保存 adcode 到数据库
        /// </summary>
        /// <param name="dbPath">数据库路径</param>
        /// <param name="adcode">adcode 值</param>
        private void SaveAdcodeToDatabase(string dbPath, string adcode)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    string query = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('adcode', @Adcode);";
                    var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@Adcode", adcode);
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MyLoger.Error("保存adcode时发生错误:{error}", ex.ToString());
                MessageBox.Show("保存adcode时发生错误: " + ex.Message);
            }
        }

        /// <summary>
        /// 异步加载数据
        /// </summary>
        public async Task LoadAsync()
        {
            // 从数据库读取 adcode
            string city = GetAdcodeFromDatabase(databasePath);
            if (string.IsNullOrEmpty(city))
            {
                // 如果数据库中没有 adcode，则获取并保存
                city = await GetCityCodeAsync();
                SaveAdcodeToDatabase(databasePath, city);
            }
            // 下载数据
            await DownloadDataAsync(city, "resources\\weather.json");
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
                    MessageBox.Show("没有成功获取到信息，推荐去设置页面搜索\n已默认为北京");
                    return "110000";
                }
                return jsonResult["adcode"].ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"错误代码为：{ex.Message}" + "\n已默认为北京");
                return "110000";
            }
        }



        public async Task DownloadDataAsync(string city, string filePath)
        {
            try
            {
                var client = new HttpClient();
                var request = new HttpRequestMessage();
                request.RequestUri = new Uri($"https://restapi.amap.com/v3/weather/weatherInfo?key=71d6333d58f635ab3136a8955cec1e8c&city={city}");
                request.Method = HttpMethod.Get;

                var response = await client.SendAsync(request);
                var result = await response.Content.ReadAsStringAsync();

                // 解析 JSON 数据并格式化
                var jsonObject = Newtonsoft.Json.Linq.JObject.Parse(result);

                // 添加新的字段
                jsonObject["Refreshtime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                string formattedJson = jsonObject.ToString(Newtonsoft.Json.Formatting.Indented);

                // 保存格式化的 JSON 数据到临时文件
                string tempFilePath = "resources\\temp_weather.json";
                using (StreamWriter file = File.CreateText(tempFilePath))
                {
                    await file.WriteAsync(formattedJson);
                }

                // 确认数据正确无误后，将临时文件重命名为目标文件
                File.Delete(filePath);
                File.Move(tempFilePath, filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("获取失败，错误信息：\n" + ex.Message);
            }
        }

        public async Task LoadDataAsync(string filePath)
        {
            // 从文件中读取数据
            string result;
            using (StreamReader file = File.OpenText(filePath))
            {
                result = await file.ReadToEndAsync();
            }

            // 解析数据并更新UI
            var jsonObject = JObject.Parse(result);
            var lives = jsonObject["lives"];
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
            Data_Refreshtime = jsonObject["Refreshtime"].ToString();
        }

    }
}
