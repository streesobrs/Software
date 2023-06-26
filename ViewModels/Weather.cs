using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Globalization;

namespace Software.ViewModels
{
    class Weather
    {
        public string Data_Province { get; set; }
        public string Data_City { get; set; }
        public string Data_Adcode { get; set; }
        public string Data_Weather { get; set; }
        public string Data_Temperature { get; set; }
        public string Data_Winddirection { get; set; }
        public string Data_Windpower { get; set; }
        public string Data_Humidity { get; set; }
        public DateTime Data_Reporttime { get; set; }

        public async Task LoadAsync()
        {
            await City(city: "500000");
        }

        private async Task City(string city)
        {
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
                Data_Reporttime = DateTime.ParseExact(item["reporttime"].ToString(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
        }
    }
}
