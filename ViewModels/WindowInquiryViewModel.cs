using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using Software.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Software.ViewModels;

internal partial class WindowInquiryViewModel : ObservableObject
{

    [ObservableProperty]
    ObservableCollection<ClassWindowInquirySystem> charData = new();

    [ObservableProperty]
    ClassWindowInquirySystem selectinQuirySystem;

    [RelayCommand]

    async Task Loaded()
    {
        await LoadCity(mid: "332872375");
    }

    [RelayCommand]
    async Task LoadCity(string mid)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage();
        request.RequestUri = new Uri("https://api.bilibili.com/x/space/acc/info?mid={mid}");
        request.Method = HttpMethod.Get;

        var response = await client.SendAsync(request);
        var result = await response.Content.ReadAsStringAsync();

        var data = JObject.Parse(result)["data"];

        CharData.Clear();
        foreach (var item in data)
        {
            CharData.Add(new ClassWindowInquirySystem
            {
                mid = item["mid"].ToString(),
                name = item["name"].ToString(),
                face = item["face"][0]["url"].ToString(),
                sign = item["sign"].ToString(),
                level = item["level"].ToString()
            });
        }
        SelectinQuirySystem = CharData[0];
    }

}
