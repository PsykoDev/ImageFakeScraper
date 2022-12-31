﻿namespace GScraperExample;

internal static class Program
{
    #region Var
    private static readonly object ConsoleWriterLock = new();
    public static ConnectionMultiplexer redis;
    public static redisConnection? redisConnector;
    public static Queue<string>? qword;
    private static Dictionary<string, IEnumerable<GScraper.IImageResult>>? site;
    private static readonly KestrelMetricServer server = new(port: 4444);
    public static string key;
    public static long totalimageupload = 0;
    #endregion

    #region Start
    private static async Task Main(string[] args)
    {
        // C:\Users\johan\Desktop\GScraper-master\examples\GScraperExample\bin\Release\net7.0\GScraperExample.exe "redis://:Wau7eCrEMenreUVErQ6JrkHJ@imagefake.net:6379" 0
        using GoogleScraper scraper = new();
        using DuckDuckGoScraper duck = new();
        using BraveScraper brave = new();
        Random random = new Random();
        qword = new();

        //string[] readText = File.ReadAllText("google_twunter_lol.txt").Split("\n");
        //
        //random.Shuffle(readText);
        //foreach (string s in readText)
        //{
        //    qword.Enqueue(s);
        //}

        string credential = args[0];
        redisConnector = new(credential, 5000);
        redis = redisConnection.redisConnect();
        IDatabase conn = redis.GetDatabase();

        //write("mot random en cas de besoin", redis);

        RedisKey key = new("words_list");
        long rng = await conn.ListLengthAsync(key);
        random = new Random();
        RedisValue getredisValue = await conn.ListGetByIndexAsync(key, random.Next(0, (int)rng - 1));
        string text = getredisValue.ToString();
        qword.Enqueue(text);


        double waittime = args.Length > 0.1 ? double.Parse(args[1]) : 0;

        if (redis.IsConnected)
        {

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Redis Connected");
            Console.ResetColor();

            printData(qword.First());

            Stopwatch timer = new();

            Stopwatch uptime = new();
            uptime.Start();

            while (qword.Count != 0)
            {
                timer.Start();


                Process currentProcess = Process.GetCurrentProcess();
                long usedMemory = currentProcess.PrivateMemorySize64;

                site = await searchEngineRequest.getAllDataFromsearchEngineAsync(text);

                Queue<string> callQword = await searchEngineRequest.getAllNextTag(text, conn);

                while (callQword.Count != 0)
                {
                    qword.Enqueue(callQword.Dequeue()); // euh
                }

                _ = await redisImagePush.GetAllImageAndPush(conn, site, args);

                site.Clear();

                if (qword.Count <= 2)
                {
                    RedisValue newword = await redisGetNewTag(conn);
                    qword.Enqueue(newword.ToString());
                    text = qword.Dequeue();
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"Missing tag pick a new random: {newword}");
                    Console.ResetColor();
                }
                else
                {
                    text = qword.Dequeue();
                }

                if (!redis.IsConnected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    printData("redis disconnected");
                    Console.ResetColor();
                }
                else
                {
                    while (!redis.IsConnected)
                    {
                        await Console.Out.WriteLineAsync("/!\\ Reconnecting to redis server ! 10sec /!\\");
                        //_ = redisConnection.redisConnect();
                        Thread.Sleep(TimeSpan.FromSeconds(10));
                    }

                }

                if (conn.SetLength("image_jobs_0") == uint.MaxValue - 10000)
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Redis queue alomst full {conn.ListLength("image_jobs_0")}");
                        Console.ResetColor();
                        _ = Console.ReadLine();
                    }
                    catch { }
                }

                redisWriteNewTag(text, conn);

                timer.Stop();

                string uptimeFormated = $"{uptime.Elapsed.Days} days {uptime.Elapsed.Hours:00}:{uptime.Elapsed.Minutes:00}:{uptime.Elapsed.Seconds:00}";
                long redisDBLength = conn.SetLength(Program.key);
                string redisLength = $"{redisDBLength} / {1_000_000} ({100.0 * redisDBLength / 1_000_000:0.00}%)";
                double elapsed = TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds).TotalSeconds;
                RedisKey key_done = new("words_done");

                printData(
                        $"Uptime\t\t{uptimeFormated}\n" +
                        $"Done in\t\t{elapsed}s\n" +
                        $"Sleep\t\t{waittime} sec\n" +
                        $"Memory\t\t{SizeSuffix(usedMemory)}\n" +
                        $"Previous\t{text}\n" +
                        $"NbRequest\t{searchEngineRequest.NbOfRequest}\n" +
                        $"Tags\t\t{qword.Count}\n" +
                        $"Tag done\t{await redis.GetDatabase().ListLengthAsync(key_done)}\n" +
                        $"Tag remaining\t{await redis.GetDatabase().ListLengthAsync("words_list")}\n" +
                        $"{Program.key}\t{redisLength}\n" +
                        $"Total upload\t{totalimageupload}\n" +
                        $"Record:\t\t{redisImagePush.record}");


                Thread.Sleep(TimeSpan.FromSeconds(waittime));

                timer.Reset();
                searchEngineRequest.NbOfRequest = 0;
            }
        }
        else
        {
            while (!redis.IsConnected)
            {
                await Console.Out.WriteLineAsync("/!\\ Reconnecting to redis server ! 10sec /!\\");
                //_ = redisConnection.redisConnect();
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
        }
    }
    #endregion

    #region printData
    private static void printData(string text)
    {
        lock (ConsoleWriterLock)
        {
            Console.WriteLine("========================================================");
            Console.WriteLine(text);
            Console.WriteLine("========================================================");
        }
    }
    #endregion
    #region redisGetNewTag
    private static async Task<RedisValue> redisGetNewTag(IDatabase redis)
    {
        return await redis.ListLeftPopAsync("words_list");
    }
    #endregion
    #region redisWriteNewTag
    private static async void redisWriteNewTag(string text, IDatabase redis)
    {
        RedisValue value = new(text);
        RedisKey key = new("words_done");
        _ = await redis.ListLeftPushAsync(key, value);
    }
    #endregion
    #region SizeSuffix
    private static string SizeSuffix(long value, int decimalPlaces = 1)
    {
        string[] SizeSuffixes = { "bytes", "KB", "MB", "GB" };
        if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
        if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }
        if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }
        int mag = (int)Math.Log(value, 1024);
        decimal adjustedSize = (decimal)value / (1L << (mag * 10));
        if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
        { mag += 1; adjustedSize /= 1024; }
        return string.Format("{0:n" + decimalPlaces + "} {1}",
            adjustedSize,
            SizeSuffixes[mag]);
    }
    #endregion
    #region ShuffleArray
    public static void Shuffle<T>(this Random rng, T[] array)
    {
        int n = array.Length;
        while (n > 1)
        {
            int k = rng.Next(n--);
            T temp = array[n];
            array[n] = array[k];
            array[k] = temp;
        }
    }
    #endregion
}