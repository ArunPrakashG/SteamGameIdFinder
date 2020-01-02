using Newtonsoft.Json;

namespace SteamGameIdFinder
{
	public class AppListStructure
	{
		[JsonProperty("applist")]
		public AppList AppListCollection { get; set; } = new AppList();

		public class AppList
		{
			[JsonProperty("apps")]
			public App[] Apps { get; set; } = new App[] { };
		}

		public class App
		{
			[JsonProperty("appid")]
			public int AppId { get; set; } = 0;
			[JsonProperty("name")]
			public string AppName { get; set; } = string.Empty;
		}
	}
}
