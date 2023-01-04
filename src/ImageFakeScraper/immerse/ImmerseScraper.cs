﻿using System.Diagnostics.CodeAnalysis;

namespace ImageFakeScraper.immerse;


public class ImmerseScraper
{

    public ImmerseScraper()
    {

    }

    private readonly List<string> tmp = new();
    private const string uri = "https://www.immerse.zone/api/immerse/search";
    private readonly Regex RegexCheck = new(@"^(http|https:):?([^\s([<,>]*)(\/)[^\s[,><]*(\?[^\s[,><]*)?");

    [RequiresUnreferencedCode("euh")]
    public async Task<List<string>> GetImagesAsync(string query)
    {
        try
        {
            tmp.Clear();
            for (int i = 1; i < Settings.ImmerseMaxPage + 1; i++)
            {
                ImageFakeScraperGuards.NotNull(query, nameof(query));
                JsonCreatePush json = new()
                {
                    searchText = query,
                    pageNum = i
                };

                string jsonString = JsonSerializer.Serialize(json);
                string doc = await httpRequest.PostJson(uri, jsonString);
                Root jsonparsed = Newtonsoft.Json.JsonConvert.DeserializeObject<Root>(doc);

                if (jsonparsed != null)
                {
                    if (jsonparsed.data != null)
                    {
                        if (jsonparsed.data.imageData != null)
                        {
                            for (int j = 0; j < jsonparsed.data.imageData.Count; j++)
                            {
                                if (RegexCheck.IsMatch(jsonparsed.data.imageData[j].sourceImageUrl))
                                {
                                    if (jsonparsed.data.imageData[j].sourceImageUrl.Contains("images.unsplash.com"))
                                    {
                                        string cleanUrl = Regex.Replace(jsonparsed.data.imageData[j].sourceImageUrl, @"[?&][^?&]+=[^?&]+", "");
                                        tmp.Add(cleanUrl);
                                    }
                                    else
                                    {
                                        tmp.Add(jsonparsed.data.imageData[j].sourceImageUrl);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception) { }
        return tmp;
    }
}

public class JsonCreatePush
{
    public string? searchText { get; set; }
    public string imageUrl { get; set; } = "";
    public int? pageNum { get; set; } = 1;
    public int? pageSize { get; set; } = Settings.ImmersePageSize;
    public string searchType { get; set; } = "image";
}