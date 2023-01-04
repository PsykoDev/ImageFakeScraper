﻿namespace ImageFakeScraper;

public class httpRequest
{
    private static readonly HttpClient client = new();
    private static readonly HtmlDocument doc = new();

    public static async Task<HtmlDocument> Get(string uri, params object[] query)
    {
        string url = string.Format(uri, query);
        HttpResponseMessage resp = await client.GetAsync(url);
        string data = await resp.Content.ReadAsStringAsync();
        doc.LoadHtml(data);
        return doc;
    }

    public static async Task<string> GetJson(string uri, params object[] query)
    {
        string url = string.Format(uri, query);
        HttpResponseMessage resp = await client.GetAsync(url);
        string data = await resp.Content.ReadAsStringAsync();
        return data;
    }

    public static async Task<string> PostJson(string uri, string json)
    {
        StringContent content = new(json, Encoding.UTF8, "application/json");
        HttpResponseMessage result = await client.PostAsync(uri, content);
        string data = await result.Content.ReadAsStringAsync();
        return data;
    }
}
