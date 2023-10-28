using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Software.Helpers;
using Newtonsoft.Json.Linq;
using Software.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Software.ViewModels;

internal partial class MainWindowViewModel:ObservableObject
{
    [ObservableProperty]
    ObservableCollection<Character> charList = new();

    [ObservableProperty]
    Character selectedItem;

    [ObservableProperty]
    BitmapImage dawnImage;

    [ObservableProperty]
    BitmapImage duskImage;

    [ObservableProperty]
    string backgroundUrl;

    [RelayCommand]

    async Task Loaded()
    {
        await LoadCity(id:"150");
    }

    [RelayCommand]
    async Task LoadCity(string id)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage();
        request.RequestUri = new Uri(uriString:$"https://content-static.mihoyo.com/content/ysCn/getContentList?pageSize=20&pageNum=1&order=asc&channelId={id}");
        request.Method = HttpMethod.Get;

        var response = await client.SendAsync(request);
        var result = await response.Content.ReadAsStringAsync();

        var list = JObject.Parse(result)["data"]["list"];

        CharList.Clear();
        foreach (var item in list)
        {
            CharList.Add(new Character {
                Name = item["title"].ToString(),
                IconUrl = item["ext"].First(v => v["arrtName"].ToString() == "角色-ICON")["value"][0]["url"].ToString(),
                ProtraitUrl = item["ext"].First(v => v["arrtName"].ToString() == "角色-PC端主图")["value"][0]["url"].ToString(),
                NameUrl = item["ext"].First(v => v["arrtName"].ToString() == "角色-名字")["value"][0]["url"].ToString(),
                ElementUrl = item["ext"].First(v => v["arrtName"].ToString() == "角色-属性")["value"][0]["url"].ToString(),
                DialogueUrl = item["ext"].First(v => v["arrtName"].ToString() == "角色-台词")["value"][0]["url"].ToString()
            });
        }

        SelectedItem = CharList[0];
        _ = ChangeBg(id);
    }

    async Task ChangeBg(string id)
    {
        var dawnUrl = id switch
        {
            "150" => @"https://uploadstatic.mihoyo.com/contentweb/20200211/2020021114220951905.jpg",
            "151" => @"https://uploadstatic.mihoyo.com/contentweb/20200515/2020051511073340128.jpg",
            "324" => @"https://uploadstatic.mihoyo.com/contentweb/20210719/2021071917030766463.jpg",
            "350" => @"https://webstatic.mihoyo.com/upload/contentweb/2022/08/15/04d542b08cdee91e5dabfa0e85b8995e_8653892990016707198.jpg",
            "358" => @"https://act-webstatic.mihoyo.com/upload/contentweb/hk4e/34ec75c9ed70f793cdd698ad1a4764e5_731983624099835302.jpg",
            _ => throw new ArgumentException(id)
        };

        var duskUrl = id switch
        {
            "150" => @"https://uploadstatic.mihoyo.com/contentweb/20200211/2020021114221470532.jpg",
            "151" => @"https://uploadstatic.mihoyo.com/contentweb/20200515/2020051511072867344.jpg",
            "324" => @"https://uploadstatic.mihoyo.com/contentweb/20210719/2021071917033032133.jpg",
            "350" => @"https://webstatic.mihoyo.com/upload/contentweb/2022/08/15/ab72edd8acc105904aa50da90e4e788e_2299455865599609620.jpg",
            "358" => @"https://act-webstatic.mihoyo.com/upload/contentweb/hk4e/3ce8f43e9de08e1988aafc00fdff2410_8142185104639306099.jpg",
            _ => throw new ArgumentException(id)
        };

        var dawn = HttpHelper.GetImageAsync(dawnUrl);
        var dusk = HttpHelper.GetImageAsync(duskUrl);
        await Task.WhenAll(dawn, dusk);

        DawnImage = dawn.Result;
        DuskImage = dusk.Result;
    }

}
