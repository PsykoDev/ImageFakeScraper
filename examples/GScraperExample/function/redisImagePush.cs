﻿using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Cryptography;

namespace GScraperExample.function;

internal class redisImagePush
{
    #region Var
    private static readonly bool printLog = false;
    public static long recordtmp { get; private set; } = 0;
    public static long record { get; private set; } = 0;

    private static int stopAfter { get; } = 11;
    private static int restartAfter { get; set; } = 10;
    #endregion

    #region getAllImage
    public static async Task<long> GetAllImageAndPush(IDatabase conn, Dictionary<string, List<string>> site, string[] args)
    {
        long data = 0;
        long totalpushactual = 0;
        foreach (KeyValuePair<string, List<string>> image in site)
        {
            if (image.Value != null)
            {

                List<string> list = new();
                List<string> list2 = new();

                foreach (string daata in image.Value)
                {
                    var filter = Builders<BsonDocument>.Filter.Eq("hash", CreateMD5(daata));

                    var find = Program.Collection.Find(filter).ToList();
                    if (find.Count == 0)
                    {
                        list.Add(daata);
                        var document = new BsonDocument { { "hash", CreateMD5(daata) } };
                        Program.Collection.InsertOne(document);
                    }
                }

                foreach (string item in Program.blackList)
                {


                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i].Contains(item))
                        {
                            list.Remove(list[i]);
                        }

                    }
                }

                RedisValue[] push = Array.ConvertAll(list.ToArray(), item => (RedisValue)item);
                try
                {
                    Uri opts = new(args[0]);
                    List<RedisKey> redisList = GetAllTable();

                    RedisValue nextIndex = await conn.StringGetAsync("jobs_last_index");
                    int parseKey = int.Parse(nextIndex.ToString());
                    Program.key = $"image_jobs_{parseKey}";

                    if (conn.SetLength(Program.key) >= 1_000_000)
                    {
                        if (redisList.Count >= stopAfter)
                        {
                            while (true)
                            {
                                if (redisList.Count <= restartAfter)
                                {
                                    Console.WriteLine("");
                                    break;
                                }
                                else
                                {
                                    for (int a = 120; a >= 0; a--)
                                    {
                                        Console.Write("{0} Queue in process, Retry after {1}\r", redisList.Count - restartAfter, TimeSpan.FromMinutes(a));
                                        Thread.Sleep(1000);
                                    }
                                    GetAllTable();
                                }
                            }
                        }

                        Program.key = $"image_jobs_{parseKey + 1}";
                        await conn.StringSetAsync("jobs_last_index", parseKey + 1);
                    }

                    if (image.Value.Count >= 1_000_000 - conn.SetLength(Program.key))
                    {

                        for (int i = 0; i < 1_000_000 - conn.SetLength(Program.key); i++)
                        {
                            list2.Add(list[i]);
                            list.Remove(list[i]);
                        }

                        RedisValue[] push2 = Array.ConvertAll(list2.ToArray(), item => (RedisValue)item);

                        data = await conn.SetAddAsync(Program.key, push2);
                    }
                    else
                        data = await conn.SetAddAsync(Program.key, push);

                    Console.ForegroundColor = ConsoleColor.Green;
                    if (image.Key == "DuckDuckGo" || image.Key.Contains("Immerse"))
                        Console.WriteLine($"{image.Key}:\t{data} / {push.Length}");
                    else
                        Console.WriteLine($"{image.Key}:\t\t{data} / {push.Length}");

                    totalpushactual += data;
                    if (image.Key == "Pixel")
                    {
                        Console.WriteLine($"Total:\t\t{totalpushactual}");
                        recordtmp = totalpushactual;
                        totalpushactual = 0;
                    }
                    Console.ResetColor();
                    if (recordtmp > record)
                    {
                        record = recordtmp;
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"RECORD:\t\t{record}");
                        Console.ResetColor();
                    }
                    try
                    {
                        if (recordtmp > int.Parse(conn.StringGet("record_push").ToString().Split(" ").Last()))
                        {
                            record = recordtmp;
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"RECORD:\t\t{record}");
                            await conn.StringSetAsync("record_push", $"{args[2]} {record}");
                            Console.ResetColor();
                        }
                    }
                    catch { }
                    Program.totalimageupload += data;
                    data = 0;

                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    await Console.Out.WriteLineAsync($"/!\\ Fail upload redis {image.Key} ! /!\\");
                    await Console.Out.WriteLineAsync($"/!\\ Fail upload redis {image.Key} ! /!\\");
                    await Console.Out.WriteLineAsync($"/!\\ Fail upload redis {image.Key} ! /!\\");
                    Console.ResetColor();

                    await Console.Out.WriteLineAsync("/!\\ Reconnecting to redis server ! /!\\");
                    //redisConnection.redisConnect();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                if (image.Key == "DuckDuckGo" || image.Key.Contains("Immerse"))
                    Console.WriteLine($"{image.Key}\tdown");
                else
                    Console.WriteLine($"{image.Key}\t\tdown");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Green;

                if (image.Key == "Pixel")
                {
                    Console.WriteLine($"Total:\t\t{totalpushactual}");
                    recordtmp = totalpushactual;
                    totalpushactual = 0;
                }
                Console.ResetColor();
                if (recordtmp > record)
                {
                    record = recordtmp;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"RECORD:\t\t{record}");
                    Console.ResetColor();
                }
                try
                {
                    if (recordtmp > int.Parse(conn.StringGet("record_push").ToString().Split(" ").Last()))
                    {
                        record = recordtmp;
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"RECORD:\t\t{record}");
                        await conn.StringSetAsync("record_push", $"{args[2]} {record}");
                        Console.ResetColor();
                    }
                }
                catch { }
            }
        }
        return data;
    }
    #endregion

    public static string CreateMD5(string input)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            return Convert.ToHexString(hashBytes);
        }
    }

    private static List<RedisKey> GetAllTable() => redisConnection.GetServers.Keys(0, "*image_jobs*").ToList();
}
