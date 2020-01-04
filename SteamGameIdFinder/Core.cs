using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading;

namespace SteamGameIdFinder
{
	public class Core
	{
		private readonly MainWindow Main;
		public static ObservableCollection<Game> GameCollection = new ObservableCollection<Game>();
		private static AppListStructure? AppList = new AppListStructure();
		private const string APP_LIST_URL = "http://api.steampowered.com/ISteamApps/GetAppList/v2";
		private const int MAX_RETRY_COUNT = 3;
		public static int StoreGamesCount => AppList != null ? AppList.AppListCollection.Apps.Count() : 0;

		public static DateTime LastTextEnteredTime = DateTime.Now;
		public static bool StartProcessing = false;
		public static CancellationTokenSource SearchToken = new CancellationTokenSource(TimeSpan.FromDays(1));

		public Core(MainWindow main)
		{
			Main = main;
		}

		public void Init()
		{
			FetchSteamGamesList();
			StartProcessing = true;
		}

		public void ProcessSearch(string searchQuery, DateTime enteredTime)
		{
			if (string.IsNullOrEmpty(searchQuery) || enteredTime == null || !StartProcessing || AppList == null)
			{
				return;
			}

			if (AppList.AppListCollection.Apps.Count() <= 0)
			{
				return;
			}

			Main.Dispatcher.Invoke(() =>
			{
				GameCollection.Clear();
			});

			for (int i = 0; i < AppList.AppListCollection.Apps.Count(); i++)
			{
				if (AppList.AppListCollection.Apps[i] == null || string.IsNullOrEmpty(AppList.AppListCollection.Apps[i].GameName) || AppList.AppListCollection.Apps[i].GameID <= 0)
				{
					continue;
				}

				if (SearchToken.IsCancellationRequested)
				{
					return;
				}

				if (!uint.TryParse(AppList.AppListCollection.Apps[i].GameID.ToString(), out uint appId))
				{
					continue;
				}

				if (!string.IsNullOrEmpty(AppList.AppListCollection.Apps[i].GameName) && AppList.AppListCollection.Apps[i].GameName.Contains(searchQuery, StringComparison.InvariantCultureIgnoreCase))
				{
					Main.Dispatcher.Invoke(() =>
					{
						GameCollection.Add(new Game(AppList.AppListCollection.Apps[i].GameName, appId));
					});
				}
			}
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

				AppList = JsonConvert.DeserializeObject<AppListStructure>(json);

				if (AppList == null || AppList.AppListCollection.Apps.Length <= 0)
				{
					return false;
				}

				for (int i = 0; i < AppList.AppListCollection.Apps.Length; i++)
				{
					if (string.IsNullOrEmpty(AppList.AppListCollection.Apps[i].GameName) || !uint.TryParse(AppList.AppListCollection.Apps[i].GameID.ToString(), out uint appID))
					{
						continue;
					}

					if (i < 300)
					{
						if (AppList.AppListCollection.Apps[i].GameName.Length > 45)
						{
							string gameName = AppList.AppListCollection.Apps[i].GameName.Substring(0, 43);
							gameName += "...";
							Main.Dispatcher.Invoke(() =>
							{
								GameCollection.Add(new Game(gameName, appID));
							});
						}
						else
						{
							Main.Dispatcher.Invoke(() =>
							{
								GameCollection.Add(new Game(AppList.AppListCollection.Apps[i].GameName, appID));
							});
						}
					}
				}

				Main.gameCountLabel.Dispatcher.Invoke(() => Main.gameCountLabel.Content = $"Steam Game Count: {StoreGamesCount}");
				return AppList.AppListCollection.Apps.Length > 0;
			}
			catch (Exception)
			{
				//throw
				return false;
			}
		}

		public void LoadDefaults()
		{
			if (AppList == null)
			{
				return;
			}

			if (AppList.AppListCollection.Apps.Length <= 0)
			{
				return;
			}

			for (int i = 0; i < AppList.AppListCollection.Apps.Length; i++)
			{
				if (string.IsNullOrEmpty(AppList.AppListCollection.Apps[i].GameName) || !uint.TryParse(AppList.AppListCollection.Apps[i].GameID.ToString(), out uint appID))
				{
					continue;
				}

				if (i < 100)
				{
					if (AppList.AppListCollection.Apps[i].GameName.Length > 45)
					{
						string gameName = AppList.AppListCollection.Apps[i].GameName.Substring(0, 43);
						gameName += "...";
						Main.Dispatcher.Invoke(() =>
						{
							GameCollection.Add(new Game(gameName, appID));
						});
					}
					else
					{
						Main.Dispatcher.Invoke(() =>
						{
							GameCollection.Add(new Game(AppList.AppListCollection.Apps[i].GameName, appID));
						});
					}
				}
				else
				{
					break;
				}
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
