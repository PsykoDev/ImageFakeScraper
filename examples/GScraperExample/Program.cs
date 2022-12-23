﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GScraper;
using GScraper.Brave;
using GScraper.DuckDuckGo;
using GScraper.Google;
using HtmlAgilityPack;
using StackExchange.Redis;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GScraperExample;

internal static class Program
{
    static string path = @"done.txt";
    static string alreadydone = "";

    private static async Task Main(string[] args)
    {

        var options = ConfigurationOptions.Parse("imagefake.net:6379");
        options.Password = "yoloimage";
        options.CommandMap = CommandMap.Create(new HashSet<string> { "SUBSCRIBE" }, false);
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(options);

        //IDatabase conn = redis.GetDatabase();

        using var scraper = new GoogleScraper();
        using var duck = new DuckDuckGoScraper();
        using var brave = new BraveScraper();
        HtmlNodeCollection table;
        string actual;

        bool stopBrave = false;

        List<string> qword = new();
        if (!File.Exists(path))
        {
            using (StreamWriter sw = File.CreateText(path))
            {
                sw.Write("Meow");
            }
        }

        var key = new RedisKey("already_done_ZADD");
        var meow = await redis.GetDatabase().SortedSetRangeByRankAsync(key, stop:1, order: Order.Descending);
        string text = meow.FirstOrDefault();

        qword.Add(text);

        int waittime;
        if (args.Length > 1)
            waittime = int.Parse(args[1]);
        else
            waittime = 5;

        //string[] readText = File.ReadAllLines("google_twunter_lol.txt");
        //foreach (string s in readText)
        //{
        //    qword.Add(s);
        //}
        //
        //qword.Reverse();


        if (redis.IsConnected)
        {

            // ImageDownloader.DownloadImagesFromUrl("https://techno.firenode.net/index.sh");

            Thread thread = new Thread(() => Reddit.RedditCrawler(redis));
            //thread.Start();


            await Console.Out.WriteLineAsync("Redis Connected");

            await Console.Out.WriteLineAsync("=====================================================================");
            await Console.Out.WriteLineAsync(qword[0]);
            await Console.Out.WriteLineAsync("=====================================================================");

            for (int i = 0; i < qword.Count; i++)
            {

                IEnumerable<IImageResult> google;
                try
                {
                    google = await scraper.GetImagesAsync(text);
                }
                catch (Exception e) when (e is HttpRequestException or GScraperException)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }

               // IEnumerable<IImageResult> duckduck;
               // try
               // {
               //     duckduck = await duck.GetImagesAsync(text);
               //
               // }
               // catch (Exception e) when (e is HttpRequestException or GScraperException)
               // {
               //     Console.WriteLine(e.Message);
               //     continue;
               // }

                //IEnumerable<IImageResult> bravelist;
                //try
                //{
                //    bravelist = await brave.GetImagesAsync(text);
                //}
                //catch (Exception e) when (e is HttpRequestException or GScraperException)
                //{
                //    Console.WriteLine(e.Message);
                //    continue;
                //}

                var images = new List<IEnumerable<IImageResult>>
               {
                 // bravelist,
                 // duckduck,
                  google
               };

                var url = $"https://www.google.com/search?q={text}&tbm=isch&hl=en";
                using (HttpClient client = new HttpClient())
                {
                    using (HttpResponseMessage response = client.GetAsync(url).Result)
                    {
                        using (HttpContent content = response.Content)
                        {
                            string result = content.ReadAsStringAsync().Result;
                            HtmlDocument document = new();
                            document.LoadHtml(result);

                            table = document.DocumentNode.SelectNodes("//a[@class='TwVfHd']");

                            try
                            {
                                if (table != null)
                                {
                                    for (var j = 0; j < table.Count; j++)
                                    {
                                        if (!await Read(redis, table[j].InnerText))
                                        {
                                            qword.Add(table[j].InnerText);
                                        }
                                    }
                                    var listduplicate = RemoveDuplicatesSet(qword);
                                    qword.Clear();
                                    qword.AddRange(listduplicate);
                                }
                            }
                            catch (Exception e)
                            {
                                await Console.Out.WriteLineAsync("No Tag!");
                                await Console.Out.WriteLineAsync(e.Message);
                            }
                        }
                    }
                }

                foreach (var image in images)
                {
                    foreach (var daata in image)
                    {
                        Console.WriteLine();
                        Console.WriteLine(JsonSerializer.Serialize(daata, daata.GetType(), new JsonSerializerOptions { WriteIndented = true })); ;
                        await redis.GetDatabase().SetAddAsync("image_jobs", daata.Url);
                        Console.WriteLine();

                    }
                }
                images.Clear();
                text = qword[i];

                if (!redis.IsConnected)
                {
                    await Console.Out.WriteLineAsync("redis disconnected, press enter to stop");
                    Console.ReadLine();
                    break;
                }

                if (table == null)
                    await Console.Out.WriteLineAsync("No more Tag found!");

                if (redis.GetDatabase().SetLength("image_jobs") == uint.MaxValue - 10000)
                {
                    await Console.Out.WriteLineAsync($"Redis queue alomst full {redis.GetDatabase().ListLength("image_jobs")}");
                    Console.ReadLine();
                }

                write(text, redis);


                await Console.Out.WriteLineAsync("=====================================================================");
                try
                {
                    await Console.Out.WriteLineAsync($"Previous done: {text}, Next: {qword[i + 1]}, Redis ListLen: {redis.GetDatabase().SetLength("image_jobs")} / {uint.MaxValue}, already done word: {redis.GetDatabase().SetMembers(key).Length}");
                }
                catch { }
                await Console.Out.WriteLineAsync("=====================================================================");
                await Console.Out.WriteLineAsync($"Sleep {waittime}sec;");
                Thread.Sleep(TimeSpan.FromSeconds(waittime));


            }
        }
    }

   
    private static async void write(string text, ConnectionMultiplexer redis)
    {
        var value = new RedisValue(text);
        var key = new RedisKey("already_done_ZADD");
        await redis.GetDatabase().SortedSetAddAsync(key, value, await redis.GetDatabase().SortedSetLengthAsync(key) + 1);
    }

    private static async Task<bool> Read(ConnectionMultiplexer redis, string text) => await redis.GetDatabase().SetContainsAsync("already_done_list", text);

    public static List<string> RemoveDuplicatesSet(List<string> items)
    {
        if (items.Count == 1)
            return items;

        var result = new List<string>();
        var set = new HashSet<string>();
        for (int i = 0; i < items.Count; i++)
        {
            if (!set.Contains(items[i]))
            {
                result.Add(items[i]);
                set.Add(items[i]);
            }
        }
        return result;
    }
}