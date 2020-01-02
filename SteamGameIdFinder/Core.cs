using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SteamGameIdFinder
{
	public class Core
	{
		private readonly MainWindow Main;
		public static CustomObservableCollection<Game> GameInfoCollection = new CustomObservableCollection<Game>();
		private const string APP_LIST_URL = "http://api.steampowered.com/ISteamApps/GetAppList/v2";
		private const int MAX_RETRY_COUNT = 3;
		public static int StoreGamesCount => GameInfoCollection.Count;

		public Core(MainWindow main)
		{
			Main = main;
		}

		public void Init()
		{
			FetchSteamGamesList();
			GC.Collect();
		}

		public bool FetchSteamGamesList()
		{
			if (string.IsNullOrEmpty(APP_LIST_URL))
			{
				return false;
			}

			try
			{
				string json = string.Empty;

				for (int i = 0; i < MAX_RETRY_COUNT + 1; i++)
				{
					json = new WebClient().DownloadString(APP_LIST_URL);

					if (string.IsNullOrEmpty(json))
					{
						//might be a steam fuckup, mostly...
						continue;
					}

					break;
				}

				AppListStructure? appList = JsonConvert.DeserializeObject<AppListStructure>(json);

				if (appList == null || appList.AppListCollection.Apps.Length <= 0)
				{
					return false;
				}

				IEnumerable<Game>? list = new List<Game>();
				foreach(var game in appList.AppListCollection.Apps)
				{
					if(string.IsNullOrEmpty(game.AppName) || !uint.TryParse(game.AppId.ToString(), out uint appID))
					{
						continue;
					}

					list.Append(new Game(game.AppName, appID));
				}

				GameInfoCollection.AddRange(list);
				appList = null;
				list = null;
				return GameInfoCollection.Count > 0;
			}
			catch (Exception)
			{
				//throw
				return false;
			}
		}

		public static Thread? InBackgroundThread(Action action, string? threadName = null, bool longRunning = false)
		{
			if (action == null)
			{
				return null;
			}

			ThreadStart threadStart = new ThreadStart(action);
			Thread BackgroundThread = new Thread(threadStart);

			if (longRunning)
			{
				BackgroundThread.IsBackground = true;
			}

			BackgroundThread.Name = string.IsNullOrEmpty(threadName) ? new Random().Next(0, 100000).ToString() : threadName;
			BackgroundThread.Priority = ThreadPriority.Normal;
			BackgroundThread.Start();
			return BackgroundThread;
		}
	}
}
