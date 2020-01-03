using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SteamGameIdFinder
{
	public class Core
	{
		private readonly MainWindow Main;
		public static ObservableCollection<Game> GameCollection = new ObservableCollection<Game>();
		private static AppListStructure? AppList = new AppListStructure();
		private const string APP_LIST_URL = "http://api.steampowered.com/ISteamApps/GetAppList/v2";
		private static readonly SemaphoreSlim DbLock = new SemaphoreSlim(1, 1);
		private static readonly SemaphoreSlim DbCommandLock = new SemaphoreSlim(1, 1);
		private const int MAX_RETRY_COUNT = 3;
		public static int StoreGamesCount => AppList != null ? AppList.AppListCollection.Apps.Count() : 0;

		private const string DbServer = "localhost"; //Database will be removed later on. added only to test.
		private const string DbUsername = "rm_user";
		private const string DbPassword = "astlavistababy"; // thats litrelly the password bro but its a localhost so dont waste your brain memory remembering it
		private const string DbName = "SteamIdFinderDb";
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
			Core.InBackgroundThread(async () => await InitDatabase().ConfigureAwait(false));
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

		private async Task CreateTableIfNotExist(NpgsqlConnection conn)
		{
			if (conn == null)
			{
				return;
			}

			try
			{
				await DbCommandLock.WaitAsync().ConfigureAwait(false);
				string cmdString = "CREATE TABLE IF NOT EXISTS SteamIds (game_name varchar, app_id oid);";
				await using var createTable = new NpgsqlCommand(cmdString, conn);
				await createTable.ExecuteNonQueryAsync();
			}
			finally
			{
				DbCommandLock.Release();
			}
		}

		private async Task InsertIntoTable(NpgsqlConnection conn, Game? game)
		{
			if (conn == null || game == null || string.IsNullOrEmpty(game.GameName) || game.GameID <= 0)
			{
				return;
			}

			try
			{
				await DbCommandLock.WaitAsync().ConfigureAwait(false);
				string cmdString = "INSERT INTO SteamIds (game_name, app_id) VALUES (@gameName, @appid);";
				await using var cmd = new NpgsqlCommand(cmdString, conn);
				cmd.Parameters.AddWithValue("gameName", NpgsqlDbType.Varchar, game.GameName);
				cmd.Parameters.AddWithValue("appid", NpgsqlDbType.Oid, game.GameID);
				await cmd.ExecuteNonQueryAsync();
			}
			finally
			{
				DbCommandLock.Release();
			}
		}

		private async Task<long> CheckGameCount(NpgsqlConnection conn)
		{
			if (conn == null)
			{
				return -1;
			}

			try
			{
				await DbCommandLock.WaitAsync().ConfigureAwait(false);
				string comString = $"SELECT count(*) from SteamIds;";
				await using var cmd = new NpgsqlCommand(comString, conn);
				long count = (long) await cmd.ExecuteScalarAsync().ConfigureAwait(false);
				return count;
			}
			catch (Exception e)
			{
				return -1;
			}
			finally
			{
				DbCommandLock.Release();
			}
		}

		private async Task DeleteAllRows(NpgsqlConnection conn)
		{
			if (conn == null)
			{
				return;
			}

			try
			{
				await DbCommandLock.WaitAsync().ConfigureAwait(false);
				string cmdString = "DELETE FROM SteamIds";
				await using var cmd = new NpgsqlCommand(cmdString, conn);
				await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
			}
			finally
			{
				DbCommandLock.Release();
			}
		}

		private async Task<bool> InitDatabase()
		{
			NpgsqlConnection? conn = null;

			try
			{
				await DbLock.WaitAsync().ConfigureAwait(false);
				string connString = $"Server={DbServer}; User Id={DbUsername}; Password={DbPassword}; Database={DbName};";
				conn = new NpgsqlConnection(connString);
				await conn.OpenAsync().ConfigureAwait(false);
				await CreateTableIfNotExist(conn).ConfigureAwait(false);
				long gameCount = await CheckGameCount(conn).ConfigureAwait(false);

				if (AppList == null)
				{
					return false;
				}

				if (gameCount != StoreGamesCount && gameCount > 0)
				{
					await DeleteAllRows(conn).ConfigureAwait(false);

					if (AppList.AppListCollection.Apps.Count() <= 0)
					{
						return false;
					}

					for (int i = 0; i < AppList.AppListCollection.Apps.Length; i++)
					{
						if (!uint.TryParse(AppList.AppListCollection.Apps[i].GameID.ToString(), out uint appid))
						{
							continue;
						}

						await InsertIntoTable(conn, new Game(AppList.AppListCollection.Apps[i].GameName, appid)).ConfigureAwait(false);
					}
				}

				return true;
			}
			finally
			{
				DbLock.Release();
				if (conn != null)
				{
					await conn.DisposeAsync().ConfigureAwait(false);
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

				int counter = 0;

				for(int i = 0; i< AppList.AppListCollection.Apps.Length; i++)
				{
					if (string.IsNullOrEmpty(AppList.AppListCollection.Apps[i].GameName) || !uint.TryParse(AppList.AppListCollection.Apps[i].GameID.ToString(), out uint appID))
					{
						continue;
					}

					if(i < 300)
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
			if(AppList == null)
			{
				return;
			}

			if (AppList.AppListCollection.Apps.Length <= 0)
			{
				return;
			}

			int counter = 0;

			for(int i = 0; i < AppList.AppListCollection.Apps.Length; i++)
			{
				if (string.IsNullOrEmpty(AppList.AppListCollection.Apps[i].GameName) || !uint.TryParse(AppList.AppListCollection.Apps[i].GameID.ToString(), out uint appID))
				{
					continue;
				}

				if(i < 100)
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
