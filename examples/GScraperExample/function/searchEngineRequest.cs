﻿using GScraper;
using GScraper.Brave;
using GScraper.DuckDuckGo;
using GScraper.Google;
using HtmlAgilityPack;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GScraperExample.function
{
    internal class searchEngineRequest
    {

        private static readonly GoogleScraper scraper = new();
        private static readonly DuckDuckGoScraper duck = new();
        private static readonly BraveScraper brave = new();
        private static bool ddc = false;
        private static bool brv = false;
        private static bool ov = true;
        private static readonly bool printLog = false;
        private static readonly Dictionary<string, IEnumerable<IImageResult>> tmp = new();
        private static readonly Queue<string> qword = new();
        private static HtmlNodeCollection? table;
        private static readonly List<NeewItem> newItem = new();
        private static readonly HttpClient http = new();
        private static readonly Regex RegexCheck = new(@".*\.(jpg|png|gif)?$");


        public static async Task<Dictionary<string, IEnumerable<IImageResult>>> getAllDataFromsearchEngineAsync(string text)
        {


            if (GoogleScraper.gg)
            {
                IEnumerable<IImageResult> google;
                try
                {
                    google = await scraper.GetImagesAsync(text);
                    tmp.Add("Google", google);
                }
                catch (Exception e) when (e is HttpRequestException or GScraperException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Google: {e.Message}");
                    Console.ResetColor();
                    if (e.Message.Contains("429"))
                    {
                        GoogleScraper.gg = false;
                    }
                }
            }

            if (ddc)
            {
                IEnumerable<IImageResult> duckduck;
                try
                {
                    duckduck = await duck.GetImagesAsync(text);
                    tmp.Add("DuckDuckGo", duckduck);

                }
                catch (Exception e) when (e is HttpRequestException or GScraperException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Duckduckgo: {e.Message}");
                    Console.ResetColor();
                    if (e.Message.Contains("token") || e.Message.Contains("403"))
                    {
                        ddc = false;
                    }
                }
            }

            if (brv)
            {
                IEnumerable<IImageResult> bravelist;
                try
                {
                    bravelist = await brave.GetImagesAsync(text);
                    tmp.Add("Brave", bravelist);
                }
                catch (Exception e) when (e is HttpRequestException or GScraperException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Brave: {e.Message}");
                    Console.ResetColor();
                    if (e.Message.Contains("429"))
                    {
                        brv = false;
                    }
                }
            }

            if (ov)
            {
                try
                {
                    string data = await http.GetStringAsync($"https://api.openverse.engineering/v1/images/?format=json&q={text}&page=1&mature=true");
                    Root jsonparse = JsonConvert.DeserializeObject<Root>(data);

                    for (int i = 0; i < jsonparse.results.Count; i++)
                    {
                        if (RegexCheck.IsMatch(jsonparse.results[i].url))
                        {

                            NeewItem blap2 = new()
                            {
                                Url = jsonparse.results[i].url,
                                Title = jsonparse.results[i].title,
                                Height = 0,
                                Width = 0
                            };

                            newItem.Add(blap2);

                        }
                    }
                    tmp.Add($"Openverse", newItem.AsEnumerable());
                }
                catch (Exception e) when (e is HttpRequestException or GScraperException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Openverse: {e.Message}");
                    Console.ResetColor();
                    if (e.Message.Contains("429"))
                    {
                        ov = false;
                    }
                }
            }

            if (!GoogleScraper.gg && !ddc && !brv && !ov)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                await Console.Out.WriteLineAsync("All search engine down for now");
                Console.ResetColor();
            }
            else if (GoogleScraper.gg && ddc && brv && ov)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                await Console.Out.WriteLineAsync("All search engine up");
                Console.ResetColor();
            }
            else
            {
                if (!GoogleScraper.gg)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    if (printLog)
                    {
                        Console.WriteLine("Google stopped");
                    }

                    Console.ResetColor();
                    GoogleScraper.gg = true;
                }
                if (!ddc)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    if (printLog)
                    {
                        Console.WriteLine("Duckduckgo stopped");
                    }

                    Console.ResetColor();
                    //ddc = true;
                }
                if (!brv)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    if (printLog)
                    {
                        Console.WriteLine("Brave stopped");
                    }

                    Console.ResetColor();
                    brv = true;
                }
                if (!ov)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    if (printLog)
                    {
                        Console.WriteLine("Openverse stopped");
                    }

                    Console.ResetColor();
                    ov = true;
                }
            }

            return tmp;
        }

        public static async Task<Queue<string>> getAllNextTag(string text, ConnectionMultiplexer redis)
        {

            string url = $"https://www.google.com/search?q={text}&tbm=isch&hl=en";
            using (HttpClient client = new())
            {
                try
                {
                    using HttpResponseMessage response = client.GetAsync(url).Result;
                    using HttpContent content = response.Content;
                    string result = content.ReadAsStringAsync().Result;
                    HtmlDocument document = new();
                    document.LoadHtml(result);

                    table = document.DocumentNode.SelectNodes("//a[@class='TwVfHd']");
                }
                catch { }

                try
                {
                    if (table != null)
                    {
                        for (int j = 0; j < table.Count; j++)
                        {
                            if (await Read(redis, table[j].InnerText) == -1)
                            {
                                qword.Enqueue(table[j].InnerText);
                                //Console.ForegroundColor = ConsoleColor.Green;
                                //await Console.Out.WriteLineAsync($"Tag Added {table[j].InnerText}");
                                //Console.ResetColor();
                            }
                            else
                            {
                                //Console.ForegroundColor = ConsoleColor.Red;
                                //await Console.Out.WriteLineAsync($"Tag already exist {table[j].InnerText}");
                                //Console.ResetColor();
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    await Console.Out.WriteLineAsync("No tag found!");
                    await Console.Out.WriteLineAsync(e.Message);
                    Console.ResetColor();
                }
            }

            return qword;
        }

        private static async Task<long> Read(ConnectionMultiplexer redis, string text)
        {
            return await redis.GetDatabase().ListPositionAsync("already_done_list", text);
        }
    }

    public class NeewItem : IImageResult
    {
        public string? Url { get; set; }

        public string? Title { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }
    }
}
