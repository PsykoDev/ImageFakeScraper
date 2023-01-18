﻿using ImageFakeScraper.OpenVerse;
using ImageFakeScraper.Qwant;
using ImageFakeScraper.Unsplash;
using Microsoft.VisualBasic;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable
namespace ImageFakeScraperExample
{
	public class MultiThread
	{
		private SettingsDll settings = new();

		private SemaphoreSlim mySemaphoreSlim = new SemaphoreSlim(1, 1);

        private SimpleMovingAverage MovingAverage = new SimpleMovingAverage(30);

        private AutoResetEvent auto = new(false);

		private Dictionary<string, Scraper> dicoEngine = new();

		private static Object _lock = new();

		ConcurrentQueue<Tuple<string, string>> queue = new();

		private List<Thread> threadList = new();

		private static readonly List<Task> tasks = new();

		int ThreadCount = 0;

		int QueueLimit = 30;

		bool printLog;
		bool printLogTag;

        DateTime last_time = DateAndTime.Now;

        int rates = 0;
        int ratesPrint = 0;

        private static readonly Stopwatch uptime = new();

		public MultiThread(bool printLog, bool printLogTag, int nbThread = 8, int QueueLimit = 30)
		{
			ThreadCount = nbThread;
			this.QueueLimit = QueueLimit;
			this.printLog = printLog;
			this.printLogTag = printLogTag;
		}

		public void InitMultiThread()
		{
			uptime.Start();
			Dictionary<string, object> options = new()
			{
				{"redis_push_key", Program.key },
				{"redis_queue_limit_name", Program.ConfigFile.Configs["to_download"] },
				{"redis_queue_limit_count",  Program.ConfigFile.Configs["settings"]["stopAfter"] }
			};
			if ((bool)Program.ConfigFile.Configs["settings"]["BingRun"])
				dicoEngine.Add("Bing", new BinImageFakeScraper());
            if ((bool)Program.ConfigFile.Configs["settings"]["QwantRun"])
                dicoEngine.Add("Qwant", new QwantScraper());
            if ((bool)Program.ConfigFile.Configs["settings"]["UnsplashRun"])
                dicoEngine.Add("Unsplash", new UnsplashScraper());
            if ((bool)Program.ConfigFile.Configs["settings"]["GoogleRun"])
				dicoEngine.Add("Google", new GoogleScraper());
			if ((bool)Program.ConfigFile.Configs["settings"]["AlamyRun"])
				dicoEngine.Add("Alamy", new AlamyScraper());
			if ((bool)Program.ConfigFile.Configs["settings"]["OpenVerseRun"])
				dicoEngine.Add("Open", new OpenVerseScraper());
			if ((bool)Program.ConfigFile.Configs["settings"]["YahooRun"])
				dicoEngine.Add("Yahoo", new YahooScraper());
			if ((bool)Program.ConfigFile.Configs["settings"]["GettyImageRun"])
				dicoEngine.Add("Getty", new GettyScraper());
			if ((bool)Program.ConfigFile.Configs["settings"]["EveryPixelRun"])
				dicoEngine.Add("Pixel", new PixelScraper());
			if ((bool)Program.ConfigFile.Configs["settings"]["ImmerseRun"])
				dicoEngine.Add("Immerse", new ImmerseScraper());

			foreach(var engine in dicoEngine)
			{
				engine.Value.setRedis(redisConnection.GetDatabase);
				engine.Value.setOptions(options);
				engine.Value.setMovingAverage(MovingAverage);
            }
		}

		private void LogPrintData()
		{
			while (true)
			{
				try
				{
					string uptimeFormated = $"{uptime.Elapsed.Days} days {uptime.Elapsed.Hours:00}:{uptime.Elapsed.Minutes:00}:{uptime.Elapsed.Seconds:00}";
					printData(
						$"Uptime\t\t{uptimeFormated}\n" +
						$"Total Tag\t{queue.Count}\n" +
						$"Thread\t\t{Program.nbThread}\n" +
						$"Sleep\t\t{Program.waittime}\n" +
						$"Request/sec\t{Program.requestMaxPerSec}\n"+
						$"Total Push\t{SettingsDll.nbPushTotal}");
				}
				catch { }

				Thread.Sleep(TimeSpan.FromMinutes(1));
			}
		}

		public void SpawnThreads()
		{
			for (int i = 0; i < ThreadCount; i++)
			{
				Thread thread1 = new Thread(Worker);
				threadList.Add(thread1);
				thread1.Start();
			}

            Thread poll = new Thread(PrintTotalpersec);
			poll.Start();

			Thread GlobalLog = new Thread(LogPrintData);
			GlobalLog.Start();
		}

        private void PrintTotalpersec(object? obj)
        {
            while (true)
            {

				Console.Write($"\rTotal {SettingsDll.nbPushTotal}, [{ratesPrint}/s]											");

				Thread.Sleep(TimeSpan.FromMilliseconds(100));
			}
        }

        private async void Worker()
		{


			while (true)
			{	
				try
				{

                    RedisValue keywords = await redisConnection.GetDatabase.SetPopAsync(Program.ConfigFile.Configs["words_list"].ToString());
					//Console.WriteLine(keywords);
                    Random rand = new Random();
					dicoEngine = dicoEngine.OrderBy(x => rand.Next()).ToDictionary(item => item.Key, item => item.Value);
					for (int i = 0; i < dicoEngine.Count; i++)
					{
						object[] args = new object[] { keywords.ToString(), 1, 1_500, false, redisConnection.GetDatabase };
						AsyncCallback callBack = new AsyncCallback(onRequestFinih);
						rates += dicoEngine.ElementAt(i).Value.GetImages(callBack, args).Result;
						ratesPrint = (int)MovingAverage.Update(rates);

                        Thread.Sleep(TimeSpan.FromSeconds(Program.waittime));
					}

                    rates = 0;
                    //Console.WriteLine("j'arriv pas queue");
                }
				catch (Exception e) { Console.WriteLine(e); }


			}
		}

		private void onRequestFinih(IAsyncResult ar)
		{

		}


		private static void printData(string text)
		{
			lock (_lock)
			{
				string line = string.Concat(Enumerable.Repeat("=", Console.WindowWidth));
				Console.WriteLine("\n"+line);
				Console.WriteLine(text);
				Console.WriteLine(line);
			}
		}
	}
}

