using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace PlayniteGameStats;

public class StatsCalculator
{
	private readonly IPlayniteAPI _api;

	private readonly SessionStore _sessionStore;

	private readonly LegacyAdapter _legacyAdapter;

	private readonly SteamDataProvider _steamDataProvider;

	private readonly SteamDeltaImporter _steamDeltaImporter;

	private Dictionary<string, string> _coverIndex;

	private string _libraryFilesDir;

	public StatsCalculator(IPlayniteAPI api, SessionStore sessionStore, SteamDataProvider steamDataProvider, SteamDeltaImporter steamDeltaImporter)
	{
		_api = api;
		_sessionStore = sessionStore;
		_legacyAdapter = new LegacyAdapter(sessionStore);
		_steamDataProvider = steamDataProvider;
		_steamDeltaImporter = steamDeltaImporter;
		_libraryFilesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Playnite", "library", "files");
		_coverIndex = BuildCoverIndex();
	}

	private Dictionary<string, string> BuildCoverIndex()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			if (!Directory.Exists(_libraryFilesDir))
			{
				return dictionary;
			}
			string[] files = Directory.GetFiles(_libraryFilesDir, "*.*", SearchOption.AllDirectories);
			foreach (string text in files)
			{
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
				string fileName = Path.GetFileName(text);
				if (!string.IsNullOrEmpty(fileNameWithoutExtension) && !dictionary.ContainsKey(fileNameWithoutExtension))
				{
					dictionary[fileNameWithoutExtension] = text;
				}
				if (!string.IsNullOrEmpty(fileName) && !dictionary.ContainsKey(fileName))
				{
					dictionary[fileName] = text;
				}
				string text2 = text.Substring(_libraryFilesDir.Length).TrimStart('\\', '/');
				if (!string.IsNullOrEmpty(text2) && !dictionary.ContainsKey(text2))
				{
					dictionary[text2] = text;
				}
				string text3 = text2.Replace('\\', '/');
				if (!string.IsNullOrEmpty(text3) && !dictionary.ContainsKey(text3))
				{
					dictionary[text3] = text;
				}
			}
		}
		catch
		{
		}
		return dictionary;
	}

	public ChartDataPackage GetFullStats()
	{
		List<Game> games = ((_api.Database.Games != null) ? _api.Database.Games.ToList() : new List<Game>());
		Dictionary<string, SteamAppStats> steamStats = ((_steamDataProvider != null) ? _steamDataProvider.Load() : new Dictionary<string, SteamAppStats>(StringComparer.OrdinalIgnoreCase));
		_steamDeltaImporter?.Import(games, steamStats);
		_sessionStore.FlushIfDirty();
		List<SessionRecord> allSessions = _sessionStore.GetAllSessions();
		bool hasSessionData = _sessionStore.HasAnySessions();
		Dictionary<string, string> gameNames = BuildGameNameMap(games);
		List<DailyStat> dailyStats = ComputeDailyStats(games, allSessions, steamStats, gameNames);
		Dictionary<Guid, GamePlayed> gamePlaytime = ComputeGamePlaytime(games, allSessions, steamStats);
		SummaryStats summary = ComputeSummary(dailyStats, gamePlaytime);
		List<GamePlayed> topGames = ComputeTopGames(gamePlaytime);
		List<GamePlayed> periodGames = ComputePeriodGames(gamePlaytime, allSessions);
		List<GamePlayed> recentGames = ComputeRecentGames(gamePlaytime);
		DateTime nowLocal = DateTime.Now;
		Dictionary<Guid, string> genreNames = BuildGenreNameMap();
		List<GenreStat> source = ComputeGenreStats(gamePlaytime, games, genreNames, allSessions, DateTime.MinValue, DateTime.MaxValue);
		List<HourlyDateStat> hourlyDateStats = ComputeHourlyDateStats(allSessions, gameNames);
		DateTime yearStartUtc = LocalToUtc(new DateTime(nowLocal.Year, 1, 1));
		List<GenreStat> source2 = ComputeGenreStats(gamePlaytime, games, genreNames, allSessions, yearStartUtc, DateTime.MaxValue);
		DateTime monthStartUtc = LocalToUtc(new DateTime(nowLocal.Year, nowLocal.Month, 1));
		List<GenreStat> source3 = ComputeGenreStats(gamePlaytime, games, genreNames, allSessions, monthStartUtc, DateTime.MaxValue);
		DateTime todayLocal = nowLocal.Date;
		int dayOfWeek = (int)todayLocal.DayOfWeek;
		DateTime weekStartUtc = LocalToUtc(todayLocal.AddDays(-((dayOfWeek == 0) ? 6 : (dayOfWeek - 1))));
		List<GenreStat> source4 = ComputeGenreStats(gamePlaytime, games, genreNames, allSessions, weekStartUtc, DateTime.MaxValue);
		return new ChartDataPackage
		{
			Summary = summary,
			DailyStats = dailyStats.OrderBy((DailyStat d) => d.Date).ToList(),
			PeriodGames = periodGames,
			TopGames = topGames,
			RecentGames = recentGames,
			GenreStats = source.OrderByDescending((GenreStat g) => g.TotalMinutes).Take(10).ToList(),
			HourlyDateStats = hourlyDateStats,
			GenreStatsWeek = source4.OrderByDescending((GenreStat g) => g.TotalMinutes).Take(10).ToList(),
			GenreStatsMonth = source3.OrderByDescending((GenreStat g) => g.TotalMinutes).Take(10).ToList(),
			GenreStatsYear = source2.OrderByDescending((GenreStat g) => g.TotalMinutes).Take(10).ToList(),
			HasSessionData = hasSessionData
		};
	}

	private Dictionary<Guid, string> BuildGenreNameMap()
	{
		Dictionary<Guid, string> dictionary = new Dictionary<Guid, string>();
		foreach (Genre item in (_api.Database.Genres != null) ? _api.Database.Genres.ToList() : new List<Genre>())
		{
			dictionary[item.Id] = item.Name;
		}
		return dictionary;
	}

	private static Dictionary<string, string> BuildGameNameMap(List<Game> games)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (Game game in games)
		{
			string key = game.Id.ToString();
			if (!string.IsNullOrEmpty(key) && !dictionary.ContainsKey(key))
			{
				dictionary[key] = game.Name;
			}
		}
		return dictionary;
	}

	private List<DailyStat> ComputeDailyStats(List<Game> games, List<SessionRecord> allSessions, Dictionary<string, SteamAppStats> steamStats, Dictionary<string, string> gameNames)
	{
		Dictionary<string, DailyStat> dictionary = new Dictionary<string, DailyStat>();
		foreach (SessionRecord session in allSessions)
		{
			AddSessionToDaily(dictionary, session, gameNames);
		}
		foreach (Game game in games)
		{
			SteamAppStats steamStats2 = GetSteamStats(game, steamStats);
			if (_sessionStore.HasSessionsForGame(game.Id) || (game.Playtime == 0L && GetSteamTotalMinutes(steamStats2) <= 0.0))
			{
				continue;
			}
			foreach (DailyStat item in _legacyAdapter.SynthesizeStats(game, steamStats2))
			{
				if (!dictionary.TryGetValue(item.Date, out DailyStat value))
				{
					value = new DailyStat
					{
						Date = item.Date
					};
					dictionary[item.Date] = value;
				}
				value.TotalMinutes += item.TotalMinutes;
				value.EstimatedMinutes += item.TotalMinutes;
				value.SessionCount += Math.Max(item.SessionCount, 1);
				value.IsEstimated = true;
				AddGameName(value, game.Name);
			}
		}
		foreach (DailyStat value2 in dictionary.Values)
		{
			FinalizeDailyStat(value2);
		}
		return dictionary.Values.ToList();
	}

	private Dictionary<Guid, GamePlayed> ComputeGamePlaytime(List<Game> games, List<SessionRecord> allSessions, Dictionary<string, SteamAppStats> steamStats)
	{
		Dictionary<Guid, GamePlayed> dictionary = new Dictionary<Guid, GamePlayed>();
		foreach (Game game in games)
		{
			string gameIdStr = game.Id.ToString();
			List<SessionRecord> sessions = allSessions.Where((SessionRecord s) => s.GameId == gameIdStr).ToList();
			SteamAppStats steamStats2 = GetSteamStats(game, steamStats);
			double exactMinutes = sessions.Where(IsExactSession).Sum(GetDurationMinutes);
			double recoveredMinutes = sessions.Where(IsRecoveredSession).Sum(GetDurationMinutes);
			double steamDeltaMinutes = sessions.Where(IsSteamDeltaSession).Sum(GetDurationMinutes);
			double sessionTotal = exactMinutes + recoveredMinutes + steamDeltaMinutes;
			double steamTotal = GetSteamTotalMinutes(steamStats2);
			double playniteTotal = (game.Playtime != 0L) ? ((double)game.Playtime / 60.0) : 0.0;
			double historicalTotal = Math.Max(steamTotal, playniteTotal);
			double estimatedMinutes = Math.Max(0.0, historicalTotal - sessionTotal);
			double totalMinutes = NormalizeMinutes(sessionTotal + estimatedMinutes);
			if (!IsEffectiveMinutes(totalMinutes))
			{
				continue;
			}
			GamePlayed gamePlayed = new GamePlayed
			{
				GameId = gameIdStr,
				GameName = game.Name,
				MinutesPlayed = totalMinutes,
				ExactMinutes = exactMinutes,
				RecoveredMinutes = recoveredMinutes,
				SteamDeltaMinutes = steamDeltaMinutes,
				EstimatedMinutes = estimatedMinutes,
				SessionCount = sessions.Count((SessionRecord s) => GetDurationMinutes(s) > 0.0),
				IsEstimated = estimatedMinutes > 0.0 || steamDeltaMinutes > 0.0,
				CoverImage = ResolveCover(game.CoverImage),
				IconImage = ResolveCover(game.Icon),
				LastPlayed = GetLastPlayed(game, sessions, steamStats2),
				DataSource = BuildGameSourceLabel(exactMinutes, recoveredMinutes, steamDeltaMinutes, estimatedMinutes),
				EstimateReason = BuildGameSourceReason(exactMinutes, recoveredMinutes, steamDeltaMinutes, estimatedMinutes),
				UserScore = game.UserScore.GetValueOrDefault()
			};
			if (gamePlayed.SessionCount == 0 && game.PlayCount > 0)
			{
				gamePlayed.SessionCount = (int)game.PlayCount;
			}
			dictionary[game.Id] = gamePlayed;
		}
		return dictionary;
	}

	private SteamAppStats GetSteamStats(Game game, Dictionary<string, SteamAppStats> steamStats)
	{
		if (game == null || steamStats == null || string.IsNullOrEmpty(game.GameId))
		{
			return null;
		}
		if (steamStats.TryGetValue(game.GameId, out SteamAppStats value))
		{
			return value;
		}
		string text = ExtractSteamAppId(game.GameId);
		if (string.IsNullOrEmpty(text) || !steamStats.TryGetValue(text, out value))
		{
			return null;
		}
		return value;
	}

	private string ResolveCover(string raw)
	{
		if (string.IsNullOrEmpty(raw))
		{
			return null;
		}
		raw = raw.Trim();
		if (raw.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
		{
			try
			{
				raw = new Uri(raw).LocalPath;
			}
			catch
			{
			}
		}
		if (File.Exists(raw))
		{
			return raw;
		}
		if (raw.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			return raw;
		}
		string text = Path.Combine(_libraryFilesDir, raw);
		if (File.Exists(text))
		{
			return text;
		}
		if (_coverIndex.TryGetValue(raw, out string value))
		{
			return value;
		}
		string text2 = raw.Replace('\\', '/');
		if (_coverIndex.TryGetValue(text2, out value))
		{
			return value;
		}
		string fileName = Path.GetFileName(text2);
		if (!string.IsNullOrEmpty(fileName) && _coverIndex.TryGetValue(fileName, out value))
		{
			return value;
		}
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
		if (!string.IsNullOrEmpty(fileNameWithoutExtension) && _coverIndex.TryGetValue(fileNameWithoutExtension, out value))
		{
			return value;
		}
		return null;
	}

	private SummaryStats ComputeSummary(List<DailyStat> dailyStats, Dictionary<Guid, GamePlayed> gamePlaytime)
	{
		double num = gamePlaytime.Values.Where((GamePlayed g) => IsEffectiveMinutes(g.MinutesPlayed)).Sum((GamePlayed g) => g.MinutesPlayed);
		double value = ((dailyStats.Count > 0) ? dailyStats.Average((DailyStat d) => d.TotalMinutes) : 0.0);
		return new SummaryStats
		{
			TotalPlayTimeHours = Math.Round(num / 60.0, 1),
			TotalGamesPlayed = gamePlaytime.Count((KeyValuePair<Guid, GamePlayed> g) => IsEffectiveMinutes(g.Value.MinutesPlayed)),
			CurrentStreakDays = ComputeStreak(dailyStats),
			AverageDailyMinutes = Math.Round(value, 1)
		};
	}

	private int ComputeStreak(List<DailyStat> dailyStats)
	{
		if (dailyStats.Count == 0)
		{
			return 0;
		}
		List<DateTime> list = (from d in dailyStats.Where((DailyStat d) => d.TotalMinutes > 0.0).Select((DailyStat d) => DateTime.Parse(d.Date)).Distinct()
			orderby d descending
			select d).ToList();
		if (list.Count == 0)
		{
			return 0;
		}
		DateTime today = DateTime.Today;
		DateTime dateTime = list[0];
		if ((today - dateTime).TotalDays > 2.0)
		{
			return 0;
		}
		int num = 1;
		for (int i = 1; i < list.Count && (dateTime - list[i]).TotalDays <= 1.0; i++)
		{
			num++;
			dateTime = list[i];
		}
		return num;
	}

	private List<GamePlayed> ComputeTopGames(Dictionary<Guid, GamePlayed> gp)
	{
		return (from g in gp.Values
			where IsEffectiveMinutes(g.MinutesPlayed)
			orderby g.MinutesPlayed descending
			select g).Take(10).ToList();
	}

	private List<GamePlayed> ComputeRecentGames(Dictionary<Guid, GamePlayed> gp)
	{
		return (from g in gp.Values
			where g.UserScore > 0 && IsEffectiveMinutes(g.MinutesPlayed)
			orderby g.UserScore descending, g.MinutesPlayed descending
			select g).Take(10).ToList();
	}

	private List<GamePlayed> ComputePeriodGames(Dictionary<Guid, GamePlayed> gp, List<SessionRecord> sessions)
	{
		DateTime cutoff = DateTime.UtcNow.AddDays(-30.0);
		HashSet<string> recentIds = new HashSet<string>(from s in sessions
			where GetSessionEndUtc(s) >= cutoff && GetDurationMinutes(s) > 0.0
			select s.GameId);
		List<GamePlayed> list = (from g in gp.Values
			where IsEffectiveMinutes(g.MinutesPlayed) && (recentIds.Contains(g.GameId) || (g.LastPlayed > DateTime.MinValue && ToUtc(g.LastPlayed) >= cutoff))
			orderby g.MinutesPlayed descending
			select g).Take(6).ToList();
		if (list.Count == 0)
		{
			list = (from g in gp.Values
				where IsEffectiveMinutes(g.MinutesPlayed)
				orderby g.MinutesPlayed descending
				select g).Take(6).ToList();
		}
		return list;
	}

	private List<GenreStat> ComputeGenreStats(Dictionary<Guid, GamePlayed> gamePlaytime, List<Game> games, Dictionary<Guid, string> genreNames, List<SessionRecord> sessions, DateTime fromUtc, DateTime toUtc)
	{
		Dictionary<string, GenreStat> dictionary = new Dictionary<string, GenreStat>();
		foreach (KeyValuePair<Guid, GamePlayed> kvp in gamePlaytime)
		{
			Game game = games.FirstOrDefault((Game g) => g.Id.ToString() == kvp.Value.GameId);
			if (game == null || game.GenreIds == null)
			{
				continue;
			}
			double num = (fromUtc <= DateTime.MinValue) ? kvp.Value.MinutesPlayed : SumSessionMinutes(sessions, kvp.Value.GameId, fromUtc, toUtc);
			if (!IsEffectiveMinutes(num))
			{
				continue;
			}
			foreach (Guid genreId in game.GenreIds)
			{
				string text = (genreNames.ContainsKey(genreId) ? genreNames[genreId] : PluginLocalization.Get("DataSourceOther", "Other"));
				if (!dictionary.TryGetValue(text, out GenreStat value))
				{
					value = new GenreStat
					{
						GenreName = text,
						TotalMinutes = 0.0,
						GameCount = 0
					};
					dictionary[text] = value;
				}
				value.TotalMinutes += num;
				value.GameCount++;
			}
		}
		return dictionary.Values.ToList();
	}

	private List<HourlyDateStat> ComputeHourlyDateStats(List<SessionRecord> sessions, Dictionary<string, string> gameNames)
	{
		Dictionary<string, HourlyDateStat> dictionary = new Dictionary<string, HourlyDateStat>(StringComparer.Ordinal);
		foreach (SessionRecord session in sessions)
		{
			AddSessionToHourlyDate(dictionary, session, gameNames);
		}
		foreach (HourlyDateStat value in dictionary.Values)
		{
			value.TotalMinutes = Math.Round(value.TotalMinutes, 1);
			value.ExactMinutes = Math.Round(value.ExactMinutes, 1);
			value.RecoveredMinutes = Math.Round(value.RecoveredMinutes, 1);
			value.SteamDeltaMinutes = Math.Round(value.SteamDeltaMinutes, 1);
		}
		return dictionary.Values.OrderBy((HourlyDateStat h) => h.Date).ThenBy((HourlyDateStat h) => h.Hour).ToList();
	}

	private void AddSessionToDaily(Dictionary<string, DailyStat> dictionary, SessionRecord session, Dictionary<string, string> gameNames)
	{
		string gameName = GetGameName(session, gameNames);
		foreach (SessionSlice slice in SliceSessionByLocalBoundary(session, DateTime.MinValue, DateTime.MaxValue, "day"))
		{
			string text = slice.LocalStart.ToString("yyyy-MM-dd");
			if (!dictionary.TryGetValue(text, out DailyStat value))
			{
				value = new DailyStat
				{
					Date = text
				};
				dictionary[text] = value;
			}
			AddMinutesBySource(value, session, slice.Minutes);
			AddGameName(value, gameName);
			value.SessionCount++;
		}
	}

	private void AddSessionToHourlyDate(Dictionary<string, HourlyDateStat> dictionary, SessionRecord session, Dictionary<string, string> gameNames)
	{
		string gameName = GetGameName(session, gameNames);
		foreach (SessionSlice slice in SliceSessionByLocalBoundary(session, DateTime.MinValue, DateTime.MaxValue, "hour"))
		{
			string date = slice.LocalStart.ToString("yyyy-MM-dd");
			int hour = slice.LocalStart.Hour;
			string key = date + "|" + hour;
			if (!dictionary.TryGetValue(key, out HourlyDateStat value))
			{
				value = new HourlyDateStat
				{
					Date = date,
					Hour = hour,
					TotalMinutes = 0.0,
					SessionCount = 0
				};
				dictionary[key] = value;
			}
			value.TotalMinutes += slice.Minutes;
			AddGameName(value, gameName);
			if (IsSteamDeltaSession(session))
			{
				value.SteamDeltaMinutes += slice.Minutes;
			}
			else if (IsRecoveredSession(session))
			{
				value.RecoveredMinutes += slice.Minutes;
			}
			else
			{
				value.ExactMinutes += slice.Minutes;
			}
			value.SessionCount++;
		}
	}

	private static IEnumerable<SessionSlice> SliceSessionByLocalBoundary(SessionRecord session, DateTime fromUtc, DateTime toUtc, string boundary)
	{
		DateTime startUtc = GetSessionStartUtc(session);
		DateTime endUtc = GetSessionEndUtc(session);
		if (startUtc <= DateTime.MinValue || endUtc <= startUtc)
		{
			yield break;
		}
		DateTime clippedStartUtc = MaxDate(startUtc, fromUtc);
		DateTime clippedEndUtc = MinDate(endUtc, toUtc);
		if (clippedEndUtc <= clippedStartUtc)
		{
			yield break;
		}
		double durationSeconds = GetDurationSeconds(session);
		double spanSeconds = Math.Max(1.0, (endUtc - startUtc).TotalSeconds);
		DateTime cursorUtc = clippedStartUtc;
		while (cursorUtc < clippedEndUtc)
		{
			DateTime local = cursorUtc.ToLocalTime();
			DateTime nextLocal = boundary == "day" ? local.Date.AddDays(1.0) : new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Local).AddHours(1.0);
			DateTime nextUtc = nextLocal.ToUniversalTime();
			if (nextUtc <= cursorUtc)
			{
				nextUtc = cursorUtc.AddHours(1.0);
			}
			DateTime segmentEndUtc = nextUtc < clippedEndUtc ? nextUtc : clippedEndUtc;
			double overlapSeconds = (segmentEndUtc - cursorUtc).TotalSeconds;
			if (overlapSeconds > 0.0)
			{
				yield return new SessionSlice
				{
					LocalStart = local,
					Minutes = durationSeconds * (overlapSeconds / spanSeconds) / 60.0
				};
			}
			cursorUtc = segmentEndUtc;
		}
	}

	private static void AddMinutesBySource(DailyStat stat, SessionRecord session, double minutes)
	{
		stat.TotalMinutes += minutes;
		if (IsSteamDeltaSession(session))
		{
			stat.SteamDeltaMinutes += minutes;
		}
		else if (IsRecoveredSession(session))
		{
			stat.RecoveredMinutes += minutes;
		}
		else if (IsEstimatedSession(session))
		{
			stat.EstimatedMinutes += minutes;
		}
		else
		{
			stat.ExactMinutes += minutes;
		}
	}

	private static void FinalizeDailyStat(DailyStat stat)
	{
		stat.TotalMinutes = Math.Round(stat.TotalMinutes, 1);
		stat.ExactMinutes = Math.Round(stat.ExactMinutes, 1);
		stat.RecoveredMinutes = Math.Round(stat.RecoveredMinutes, 1);
		stat.SteamDeltaMinutes = Math.Round(stat.SteamDeltaMinutes, 1);
		stat.EstimatedMinutes = Math.Round(stat.EstimatedMinutes, 1);
		stat.IsEstimated = stat.EstimatedMinutes > 0.0 || stat.SteamDeltaMinutes > 0.0 || stat.RecoveredMinutes > 0.0;
		stat.SourceBreakdown = BuildSourceBreakdown(stat.ExactMinutes, stat.RecoveredMinutes, stat.SteamDeltaMinutes, stat.EstimatedMinutes);
		stat.Source = stat.SourceBreakdown;
	}

	private static string GetGameName(SessionRecord session, Dictionary<string, string> gameNames)
	{
		if (session == null || string.IsNullOrEmpty(session.GameId) || gameNames == null)
		{
			return null;
		}
		if (gameNames.TryGetValue(session.GameId, out string name))
		{
			return name;
		}
		return null;
	}

	private static void AddGameName(DailyStat stat, string gameName)
	{
		if (stat == null)
		{
			return;
		}
		if (stat.GameNames == null)
		{
			stat.GameNames = new List<string>();
		}
		AddGameName(stat.GameNames, gameName);
	}

	private static void AddGameName(HourlyDateStat stat, string gameName)
	{
		if (stat == null)
		{
			return;
		}
		if (stat.GameNames == null)
		{
			stat.GameNames = new List<string>();
		}
		AddGameName(stat.GameNames, gameName);
	}

	private static void AddGameName(List<string> gameNames, string gameName)
	{
		if (gameNames == null || string.IsNullOrWhiteSpace(gameName))
		{
			return;
		}
		if (!gameNames.Contains(gameName))
		{
			gameNames.Add(gameName);
		}
	}

	private static double SumSessionMinutes(List<SessionRecord> sessions, string gameId, DateTime fromUtc, DateTime toUtc)
	{
		double total = 0.0;
		foreach (SessionRecord session in sessions.Where((SessionRecord s) => s.GameId == gameId))
		{
			total += SumSessionMinutesInRange(session, fromUtc, toUtc);
		}
		return total;
	}

	private static double SumSessionMinutesInRange(SessionRecord session, DateTime fromUtc, DateTime toUtc)
	{
		DateTime startUtc = GetSessionStartUtc(session);
		DateTime endUtc = GetSessionEndUtc(session);
		if (startUtc <= DateTime.MinValue || endUtc <= startUtc)
		{
			return 0.0;
		}
		DateTime overlapStart = MaxDate(startUtc, fromUtc);
		DateTime overlapEnd = MinDate(endUtc, toUtc);
		if (overlapEnd <= overlapStart)
		{
			return 0.0;
		}
		double durationSeconds = GetDurationSeconds(session);
		double spanSeconds = Math.Max(1.0, (endUtc - startUtc).TotalSeconds);
		return durationSeconds * ((overlapEnd - overlapStart).TotalSeconds / spanSeconds) / 60.0;
	}

	private static DateTime GetLastPlayed(Game game, List<SessionRecord> sessions, SteamAppStats steamStats)
	{
		DateTime result = game.LastActivity ?? DateTime.MinValue;
		if (sessions.Count > 0)
		{
			DateTime sessionLast = sessions.Max(GetSessionEndUtc);
			if (sessionLast > result)
			{
				result = sessionLast;
			}
		}
		if (steamStats != null && steamStats.LastPlayed > DateTime.MinValue)
		{
			DateTime steamLast = ToUtc(steamStats.LastPlayed);
			if (steamLast > result)
			{
				result = steamLast;
			}
		}
		return result > DateTime.MinValue ? result.ToLocalTime() : DateTime.MinValue;
	}

	private static string BuildGameSourceLabel(double exactMinutes, double recoveredMinutes, double steamDeltaMinutes, double estimatedMinutes)
	{
		List<string> list = new List<string>();
		if (exactMinutes > 0.0)
		{
			list.Add("Playnite");
		}
		if (recoveredMinutes > 0.0)
		{
			list.Add(PluginLocalization.Get("DataSourceRecovered", "Recovered"));
		}
		if (steamDeltaMinutes > 0.0)
		{
			list.Add(PluginLocalization.Get("DataSourceSteamDelta", "Steam delta"));
		}
		if (estimatedMinutes > 0.0)
		{
			list.Add(PluginLocalization.Get("DataSourceHistoryEstimate", "Historical estimate"));
		}
		return list.Count > 0 ? string.Join("+", list) : PluginLocalization.Get("DataSourceUnknown", "Unknown");
	}

	private static string BuildGameSourceReason(double exactMinutes, double recoveredMinutes, double steamDeltaMinutes, double estimatedMinutes)
	{
		return BuildSourceBreakdown(exactMinutes, recoveredMinutes, steamDeltaMinutes, estimatedMinutes);
	}

	private static string BuildSourceBreakdown(double exactMinutes, double recoveredMinutes, double steamDeltaMinutes, double estimatedMinutes)
	{
		List<string> list = new List<string>();
		AddSourcePart(list, "Playnite", exactMinutes);
		AddSourcePart(list, PluginLocalization.Get("DataSourceRecovered", "Recovered"), recoveredMinutes);
		AddSourcePart(list, PluginLocalization.Get("DataSourceSteamDelta", "Steam delta"), steamDeltaMinutes);
		AddSourcePart(list, PluginLocalization.Get("DataSourceEstimate", "Estimate"), estimatedMinutes);
		return list.Count > 0 ? string.Join(" / ", list) : "";
	}

	private static void AddSourcePart(List<string> parts, string label, double minutes)
	{
		if (minutes <= 0.0)
		{
			return;
		}
		parts.Add(label + " " + FormatMinutes(minutes));
	}

	private static string FormatMinutes(double minutes)
	{
		if (minutes < 60.0)
		{
			return Math.Round(minutes) + "m";
		}
		int h = (int)(minutes / 60.0);
		int m = (int)Math.Round(minutes % 60.0);
		return m == 0 ? h + "h" : h + "h" + m + "m";
	}

	private static bool IsExactSession(SessionRecord session)
	{
		return session.Source == SessionSources.Playnite || string.IsNullOrEmpty(session.Source);
	}

	private static bool IsRecoveredSession(SessionRecord session)
	{
		return session.Source == SessionSources.Recovered || session.Confidence == SessionConfidence.Recovered;
	}

	private static bool IsSteamDeltaSession(SessionRecord session)
	{
		return session.Source == SessionSources.SteamDelta || session.Confidence == SessionConfidence.Delta;
	}

	private static bool IsEstimatedSession(SessionRecord session)
	{
		return session.Confidence == SessionConfidence.Estimated || session.Source == SessionSources.SteamEstimate || session.Source == SessionSources.PlayniteEstimate;
	}

	private static double GetSteamTotalMinutes(SteamAppStats stats)
	{
		if (stats == null)
		{
			return 0.0;
		}
		return Math.Max(stats.PlaytimeMinutes, 0.0) + Math.Max(stats.PlaytimeDisconnectedMinutes, 0.0);
	}

	private static DateTime GetSessionStartUtc(SessionRecord session)
	{
		DateTime startUtc = ToUtc(session.StartUtc);
		DateTime endUtc = GetSessionEndUtc(session);
		double durationSeconds = GetDurationSeconds(session);
		if (startUtc <= DateTime.MinValue && endUtc > DateTime.MinValue && durationSeconds > 0.0)
		{
			startUtc = endUtc.AddSeconds(-durationSeconds);
		}
		return startUtc;
	}

	private static DateTime GetSessionEndUtc(SessionRecord session)
	{
		DateTime endUtc = ToUtc(session.EndUtc);
		if (endUtc <= DateTime.MinValue)
		{
			endUtc = ToUtc(session.Timestamp);
		}
		if (endUtc <= DateTime.MinValue && session.StartUtc > DateTime.MinValue && GetDurationSeconds(session) > 0.0)
		{
			endUtc = ToUtc(session.StartUtc).AddSeconds(GetDurationSeconds(session));
		}
		return endUtc;
	}

	private static double GetDurationMinutes(SessionRecord session)
	{
		return GetDurationSeconds(session) / 60.0;
	}

	private static double GetDurationSeconds(SessionRecord session)
	{
		if (session.DurationSeconds > 0)
		{
			return session.DurationSeconds;
		}
		if (session.ElapsedSeconds > 0)
		{
			return session.ElapsedSeconds;
		}
		DateTime startUtc = ToUtc(session.StartUtc);
		DateTime endUtc = ToUtc(session.EndUtc);
		if (startUtc > DateTime.MinValue && endUtc > startUtc)
		{
			return (endUtc - startUtc).TotalSeconds;
		}
		return 0.0;
	}

	private static string ExtractSteamAppId(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return null;
		}
		string text = "";
		string text2 = "";
		for (int i = 0; i < value.Length; i++)
		{
			char c = value[i];
			if (char.IsDigit(c))
			{
				text2 += c;
				if (text2.Length > text.Length)
				{
					text = text2;
				}
			}
			else
			{
				text2 = "";
			}
		}
		if (text.Length <= 0)
		{
			return null;
		}
		return text;
	}

	private static double NormalizeMinutes(double minutes)
	{
		if (double.IsNaN(minutes) || double.IsInfinity(minutes) || minutes <= 0.0)
		{
			return 0.0;
		}
		return minutes;
	}

	private static bool IsEffectiveMinutes(double minutes)
	{
		return NormalizeMinutes(minutes) >= 1.0;
	}

	private static DateTime ToUtc(DateTime value)
	{
		if (value <= DateTime.MinValue)
		{
			return DateTime.MinValue;
		}
		if (value.Kind == DateTimeKind.Utc)
		{
			return value;
		}
		if (value.Kind == DateTimeKind.Local)
		{
			return value.ToUniversalTime();
		}
		return DateTime.SpecifyKind(value, DateTimeKind.Utc);
	}

	private static DateTime LocalToUtc(DateTime value)
	{
		return DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
	}

	private static DateTime MaxDate(DateTime a, DateTime b)
	{
		return a > b ? a : b;
	}

	private static DateTime MinDate(DateTime a, DateTime b)
	{
		return a < b ? a : b;
	}

	private class SessionSlice
	{
		public DateTime LocalStart;

		public double Minutes;
	}
}
