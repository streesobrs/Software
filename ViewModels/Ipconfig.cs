using System;
using System.Windows;
using System.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Software.ViewModels
{
    class Ipconfig
    {
        private static readonly HttpClient client = new HttpClient();

        public async Task GetAdcodeAsync()
        {
            try
            {
                var response = await client.GetStringAsync("https://restapi.amap.com/v3/ip?output=json&key=71d6333d58f635ab3136a8955cec1e8c&city");
                var json = JObject.Parse(response);
                var adcode = json["adcode"].ToString();

                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings["adcode"].Value = adcode;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"信息获取失败：{ex.Message}");
            }
        }
    }
}
