using System;
using System.Collections.Generic;
using System.Text;

namespace SteamGameIdFinder
{
	public class Game
	{
		public string? GameName { get; set; }
		public uint GameID { get; set; }

		public Game(string? gameName, uint gameId)
		{
			GameName = gameName;
			GameID = gameId;
		}
	}
}
