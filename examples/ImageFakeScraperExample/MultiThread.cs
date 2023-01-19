﻿

#pragma warning disable
namespace ImageFakeScraperExample
{
	public class MultiThread
	{
		private SettingsDll settings = new();
		private SemaphoreSlim mySemaphoreSlim = new SemaphoreSlim(1, 1);
		private SimpleMovingAverage MovingAverage = new SimpleMovingAverage(30);
		private SimpleMovingAverageLong DownloadSpeed = new(15);
		private AutoResetEvent auto = new(false);
		private Dictionary<string, Scraper> dicoEngine = new();
		private static Object _lock = new();
		private List<Thread> threadList = new();
		private static readonly List<Task> tasks = new();
		private int ThreadCount = 0;
		private int QueueLimit = 30;
		private bool printLog;
		private bool printLogTag;
		private DateTime last_time = DateTime.Now;
		private int rates = 0;
		private int ratesPrint = 0;
		private long ratesSpeed = 0;
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

			// if ((bool)Program.ConfigFile.Configs["settings"]["BingRun"]) 
			// 	dicoEngine.Add("Deposit", new DepositphotosScraper());

			if ((bool)Program.ConfigFile.Configs["settings"]["ShutterstockRun"]) 
				dicoEngine.Add("Shutterstock", new ShutterstockScraper());

			if ((bool)Program.ConfigFile.Configs["settings"]["BingRun"])
				dicoEngine.Add("Bing", new BingImageFakeScraper());

			if ((bool)Program.ConfigFile.Configs["settings"]["QwantRun"])
				dicoEngine.Add("Qwant", new QwantScraper());

			if ((bool)Program.ConfigFile.Configs["settings"]["UnsplashRun"])
				dicoEngine.Add("UnsplashNapi", new UnsplashNapiScraper());

			if ((bool)Program.ConfigFile.Configs["settings"]["UnsplashRun"])
				dicoEngine.Add("UnsplashNgetty", new UnsplashScraperngetty());

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

			foreach (var engine in dicoEngine)
			{
				engine.Value.setRedis(redisConnection.GetDatabase);
				engine.Value.setOptions(options);
				engine.Value.setMovingAverage(MovingAverage);
				engine.Value.setMovingAverageDownloadSpeed(DownloadSpeed);
			}
		}

		private void LogPrintData()
		{
			while (true)
			{
				Thread.Sleep(TimeSpan.FromMinutes(1));
				Console.Clear();
				Console.WriteLine(FiggleFonts.Standard.Render("Scraper"));
				try
				{
					string uptimeFormated = $"{uptime.Elapsed.Days} days {uptime.Elapsed.Hours:00}:{uptime.Elapsed.Minutes:00}:{uptime.Elapsed.Seconds:00}";
					printData(
						$"Uptime\t\t{uptimeFormated}\n" +
						$"Thread\t\t{Program.nbThread}\n" +
						$"Sleep\t\t{Program.waittime}\n" +
						$"Request/sec\t{Program.requestMaxPerSec}\n" +
						$"Total Push\t{SettingsDll.nbPushTotal}\n" +
						$"Alamy\t\t{SettingsDll.TotalPushAlamy}\n" +
						$"Bing\t\t{SettingsDll.TotalPushBing} Need Fix\n" +
						$"Getty\t\t{SettingsDll.TotalPushGetty}\n" +
						$"Google\t\t{SettingsDll.TotalPushGoogle}\n" +
						$"Immerse\t\t{SettingsDll.TotalPushImmerse} Disable this shit\n" +
						$"Open\t\t{SettingsDll.TotalPushOpen}\n" +
						$"Pixel\t\t{SettingsDll.TotalPushPixel}\n" +
						$"Qwant\t\t{SettingsDll.TotalPushQwant}\n" +
						$"Shutterstock\t{SettingsDll.TotalPushShutterstock}\n" +
						$"Unsplash\t{SettingsDll.TotalPushUnsplash}\n" +
						$"Yahoo\t\t{SettingsDll.TotalPushYahoo}");
				}
				catch { }
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
				Console.Write($"\rTotal Push {SettingsDll.nbPushTotal}, [ {ratesPrint}/s ] Total DL {ConvertBytes(SettingsDll.downloadTotal)}, [{ConvertBytes(ratesSpeed)}/s] ");
				Thread.Sleep(TimeSpan.FromMilliseconds(100));
			}
		}

		public static string ConvertBytes(long bytes)
		{
			string[] sizes = { "B", "KB", "MB", "GB", "TB" };
			int order = 0;
			while (bytes >= 1024 && order < sizes.Length - 1)
			{
				order++;
				bytes = bytes / 1024;
			}
			return String.Format("{0:0.##} {1}", bytes, sizes[order]);
		}

		private async void Worker()
		{
			var ratesTmp = 0;
			var ratesDLTmp = 0.0;

			while (true)
			{
				try
				{
					RedisValue keywords = await redisConnection.GetDatabase.SetPopAsync(Program.ConfigFile.Configs["words_list"].ToString());
					Random rand = new Random();
					dicoEngine = dicoEngine.OrderBy(x => rand.Next()).ToDictionary(item => item.Key, item => item.Value);

					for (int i = 0; i < dicoEngine.Count; i++)
					{
						object[] args = new object[] { keywords.ToString(), 1, 1_500, false, redisConnection.GetDatabase };
						AsyncCallback callBack = new AsyncCallback(onRequestFinih);
						var (rate, dlspeed) = dicoEngine.ElementAt(i).Value.GetImages(callBack, args).Result;

						ratesTmp += rate;
						//ratesDLTmp += dlspeed;
						// set
						ratesPrint = (int)MovingAverage.Update(ratesTmp);
						ratesSpeed = (long)DownloadSpeed.Update((long)dlspeed);

						Thread.Sleep(TimeSpan.FromSeconds(Program.waittime));
					}

					// reset
					ratesTmp = 0;
					ratesPrint = 0;
					ratesSpeed = 0;
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
				Console.WriteLine("\n" + line);
				Console.WriteLine(text);
				Console.WriteLine(line);
			}
		}
	}
}

