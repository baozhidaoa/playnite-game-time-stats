using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace PlayniteGameStats;

public class StatsCalculator
{
	private readonly IPlayniteAPI _api;

	private readonly SessionStore _sessionStore;

	private readonly PlaytimeAttributionStore _attributionStore;

	private readonly SteamDataProvider _steamDataProvider;

	private readonly object _cacheLock = new object();

	private readonly object _computeLock = new object();

	private ChartDataPackage _cachedStats;

	private int _dataVersion;

	private int _cachedVersion = -1;

	private bool _forceLocalSteamRefresh;

	private bool _forceOnlineSteamRefresh;

	private readonly string _libraryFilesDir;

	public StatsCalculator(IPlayniteAPI api, SessionStore sessionStore, PlaytimeAttributionStore attributionStore, SteamDataProvider steamDataProvider)
	{
		_api = api;
		_sessionStore = sessionStore;
		_attributionStore = attributionStore;
		_steamDataProvider = steamDataProvider;
		_libraryFilesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Playnite", "library", "files");
	}

	public void Invalidate(bool forceLocalSteamRefresh = false, bool forceOnlineSteamRefresh = false)
	{
		lock (_cacheLock)
		{
			_dataVersion++;
			_forceLocalSteamRefresh |= forceLocalSteamRefresh;
			_forceOnlineSteamRefresh |= forceOnlineSteamRefresh;
		}
	}

	public ChartDataPackage GetFullStats()
	{
		int requestedVersion;
		bool forceLocal;
		bool forceOnline;
		lock (_cacheLock)
		{
			if (_cachedStats != null && _cachedVersion == _dataVersion)
			{
				return _cachedStats;
			}
			requestedVersion = _dataVersion;
			forceLocal = _forceLocalSteamRefresh;
			forceOnline = _forceOnlineSteamRefresh;
			_forceLocalSteamRefresh = false;
			_forceOnlineSteamRefresh = false;
		}
		lock (_computeLock)
		{
			ChartDataPackage stats = BuildFullStats(forceLocal, forceOnline);
			lock (_cacheLock)
			{
				if (requestedVersion == _dataVersion)
				{
					_cachedStats = stats;
					_cachedVersion = requestedVersion;
				}
			}
			return stats;
		}
	}

	private ChartDataPackage BuildFullStats(bool forceLocalSteamRefresh, bool forceOnlineSteamRefresh)
	{
		List<Game> games = _api.Database.Games != null ? _api.Database.Games.ToList() : new List<Game>();
		Dictionary<string, SteamAppStats> steamStats = _steamDataProvider != null
			? _steamDataProvider.Load(forceLocalSteamRefresh, forceOnlineSteamRefresh)
			: new Dictionary<string, SteamAppStats>(StringComparer.OrdinalIgnoreCase);
		_sessionStore.FlushIfDirty();
		List<SessionRecord> sessions = _sessionStore.GetAllSessions().Where(IsTrackedSession).ToList();
		Dictionary<string, string> gameNames = BuildGameNameMap(games);
		Dictionary<string, DailyStat> dailyByDate = new Dictionary<string, DailyStat>(StringComparer.Ordinal);
		Dictionary<string, Dictionary<string, double>> actualMinutesByGame = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, double> actualMinutesByDate = new Dictionary<string, double>(StringComparer.Ordinal);
		foreach (SessionRecord session in sessions)
		{
			AddSessionToDaily(dailyByDate, actualMinutesByGame, actualMinutesByDate, session, gameNames);
		}

		List<PlaytimeAttributionInput> attributionInputs = new List<PlaytimeAttributionInput>();
		foreach (Game game in games)
		{
			string gameId = game.Id.ToString();
			actualMinutesByGame.TryGetValue(gameId, out Dictionary<string, double> exactDates);
			attributionInputs.Add(new PlaytimeAttributionInput
			{
				GameId = gameId,
				TotalMinutes = GetPlayniteMinutes(game),
				ReleaseDateLocal = GetReleaseDate(game),
				PlayniteLastActivityLocal = ToLocal(game.LastActivity),
				SteamLastPlayedLocal = ToLocal(GetSteamStats(game, steamStats)?.LastPlayed),
				ExactMinutesByDate = exactDates ?? new Dictionary<string, double>(StringComparer.Ordinal)
			});
		}
		List<AttributedGameDay> attributedDays = _attributionStore.Synchronize(attributionInputs, actualMinutesByDate);
		Dictionary<string, Dictionary<string, double>> dailyMinutesByGame = CloneDailyMinutes(actualMinutesByGame);
		foreach (AttributedGameDay attributed in attributedDays)
		{
			if (!gameNames.TryGetValue(attributed.GameId, out string gameName) || attributed.Minutes <= 0.0001)
			{
				continue;
			}
			DailyStat stat = GetOrCreateDaily(dailyByDate, attributed.Date);
			stat.TotalMinutes += attributed.Minutes;
			AddGameMinutes(stat.GameDurations, attributed.GameId, gameName, attributed.Minutes);
			AddGameDateMinutes(dailyMinutesByGame, attributed.GameId, attributed.Date, attributed.Minutes);
		}
		foreach (DailyStat stat in dailyByDate.Values)
		{
			FinalizeDailyStat(stat);
		}

		Dictionary<string, List<SessionRecord>> sessionsByGame = BuildSessionMap(sessions);
		Dictionary<Guid, GamePlayed> gamePlaytime = ComputeGamePlaytime(games, sessionsByGame, steamStats, dailyMinutesByGame);
		Dictionary<Guid, Game> gamesById = games.ToDictionary((Game game) => game.Id);
		Dictionary<Guid, string> categoryNames = BuildCategoryNameMap();
		List<DailyStat> dailyStats = dailyByDate.Values.OrderBy((DailyStat stat) => stat.Date).ToList();
		DateTime now = DateTime.Today;
		DateTime weekStart = now.AddDays(-((int)now.DayOfWeek == 0 ? 6 : (int)now.DayOfWeek - 1));
		List<CategoryStat> categoryStats = ComputeCategoryStats(gamesById, categoryNames, gamePlaytime, dailyMinutesByGame, null, null);
		List<CategoryStat> categoryWeek = ComputeCategoryStats(gamesById, categoryNames, gamePlaytime, dailyMinutesByGame, weekStart, null);
		List<CategoryStat> categoryMonth = ComputeCategoryStats(gamesById, categoryNames, gamePlaytime, dailyMinutesByGame, new DateTime(now.Year, now.Month, 1), null);
		List<CategoryStat> categoryYear = ComputeCategoryStats(gamesById, categoryNames, gamePlaytime, dailyMinutesByGame, new DateTime(now.Year, 1, 1), null);
		return new ChartDataPackage
		{
			Summary = ComputeSummary(dailyStats, gamePlaytime),
			DailyStats = dailyStats,
			PeriodGames = ComputePeriodGames(gamePlaytime),
			TopGames = gamePlaytime.Values.Where((GamePlayed game) => game.MinutesPlayed > 0.0).OrderByDescending((GamePlayed game) => game.MinutesPlayed).Take(10).ToList(),
			RecentGames = gamePlaytime.Values.Where((GamePlayed game) => game.UserScore > 0 && game.MinutesPlayed > 0.0).OrderByDescending((GamePlayed game) => game.UserScore).ThenByDescending((GamePlayed game) => game.MinutesPlayed).Take(10).ToList(),
			CategoryStats = categoryStats.OrderByDescending((CategoryStat stat) => stat.TotalMinutes).Take(10).ToList(),
			HourlyDateStats = ComputeHourlyDateStats(sessions, gameNames),
			CategoryStatsWeek = categoryWeek.OrderByDescending((CategoryStat stat) => stat.TotalMinutes).Take(10).ToList(),
			CategoryStatsMonth = categoryMonth.OrderByDescending((CategoryStat stat) => stat.TotalMinutes).Take(10).ToList(),
			CategoryStatsYear = categoryYear.OrderByDescending((CategoryStat stat) => stat.TotalMinutes).Take(10).ToList(),
			HasSessionData = sessions.Count > 0
		};
	}

	private static Dictionary<string, Dictionary<string, double>> CloneDailyMinutes(Dictionary<string, Dictionary<string, double>> source)
	{
		Dictionary<string, Dictionary<string, double>> result = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<string, Dictionary<string, double>> pair in source)
		{
			result[pair.Key] = new Dictionary<string, double>(pair.Value, StringComparer.Ordinal);
		}
		return result;
	}

	private static bool IsTrackedSession(SessionRecord session)
	{
		return session != null && session.Source != SessionSources.SteamDelta && session.Source != SessionSources.SteamEstimate && session.Source != SessionSources.PlayniteEstimate;
	}

	private static double GetPlayniteMinutes(Game game)
	{
		return game == null ? 0.0 : game.Playtime / 60.0;
	}

	private static DateTime GetReleaseDate(Game game)
	{
		if (game?.ReleaseDate == null)
		{
			return DateTime.MinValue;
		}
		try
		{
			return game.ReleaseDate.Value.Date.Date;
		}
		catch
		{
			return DateTime.MinValue;
		}
	}

	private Dictionary<Guid, string> BuildCategoryNameMap()
	{
		Dictionary<Guid, string> result = new Dictionary<Guid, string>();
		foreach (Category category in _api.Database.Categories != null ? _api.Database.Categories.ToList() : new List<Category>())
		{
			result[category.Id] = category.Name;
		}
		return result;
	}

	private static Dictionary<string, string> BuildGameNameMap(List<Game> games)
	{
		return games.Where((Game game) => game != null).GroupBy((Game game) => game.Id.ToString(), StringComparer.OrdinalIgnoreCase).ToDictionary((IGrouping<string, Game> group) => group.Key, (IGrouping<string, Game> group) => group.First().Name, StringComparer.OrdinalIgnoreCase);
	}

	private static Dictionary<string, List<SessionRecord>> BuildSessionMap(List<SessionRecord> sessions)
	{
		return sessions.GroupBy((SessionRecord session) => session.GameId, StringComparer.OrdinalIgnoreCase).ToDictionary((IGrouping<string, SessionRecord> group) => group.Key, (IGrouping<string, SessionRecord> group) => group.ToList(), StringComparer.OrdinalIgnoreCase);
	}

	private Dictionary<Guid, GamePlayed> ComputeGamePlaytime(List<Game> games, Dictionary<string, List<SessionRecord>> sessionsByGame, Dictionary<string, SteamAppStats> steamStats, Dictionary<string, Dictionary<string, double>> dailyMinutesByGame)
	{
		Dictionary<Guid, GamePlayed> result = new Dictionary<Guid, GamePlayed>();
		foreach (Game game in games)
		{
			string gameId = game.Id.ToString();
			sessionsByGame.TryGetValue(gameId, out List<SessionRecord> sessions);
			sessions = sessions ?? new List<SessionRecord>();
			double exact = sessions.Where((SessionRecord session) => !IsRecoveredSession(session)).Sum(GetDurationMinutes);
			double recovered = sessions.Where(IsRecoveredSession).Sum(GetDurationMinutes);
			double actual = exact + recovered;
			double playniteTotal = GetPlayniteMinutes(game);
			double total = Math.Max(playniteTotal, actual);
			double attributed = dailyMinutesByGame.TryGetValue(gameId, out Dictionary<string, double> dates) ? Math.Max(0.0, dates.Values.Sum() - actual) : 0.0;
			if (total <= 0.0001)
			{
				continue;
			}
			GamePlayed played = new GamePlayed
			{
				GameId = gameId,
				GameName = game.Name,
				MinutesPlayed = total,
				ExactMinutes = exact,
				RecoveredMinutes = recovered,
				EstimatedMinutes = attributed,
				SessionCount = sessions.Count((SessionRecord session) => GetDurationMinutes(session) > 0.0),
				CoverImage = ResolveCover(game.CoverImage),
				IconImage = ResolveCover(game.Icon),
				LastPlayed = GetLastPlayed(game, sessions, GetSteamStats(game, steamStats)),
				UserScore = game.UserScore.GetValueOrDefault()
			};
			if (played.SessionCount == 0 && game.PlayCount > 0)
			{
				played.SessionCount = (int)game.PlayCount;
			}
			result[game.Id] = played;
		}
		return result;
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
		string appId = ExtractSteamAppId(game.GameId);
		return !string.IsNullOrEmpty(appId) && steamStats.TryGetValue(appId, out value) ? value : null;
	}

	private string ResolveCover(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return null;
		}
		raw = raw.Trim();
		if (raw.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
		{
			try { raw = new Uri(raw).LocalPath; } catch { }
		}
		if (File.Exists(raw) || raw.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			return raw;
		}
		string candidate = Path.Combine(_libraryFilesDir, raw);
		return File.Exists(candidate) ? candidate : null;
	}

	private static SummaryStats ComputeSummary(List<DailyStat> dailyStats, Dictionary<Guid, GamePlayed> games)
	{
		return new SummaryStats
		{
			TotalPlayTimeHours = Math.Round(games.Values.Sum((GamePlayed game) => game.MinutesPlayed) / 60.0, 1),
			TotalGamesPlayed = games.Count,
			CurrentStreakDays = ComputeStreak(dailyStats),
			AverageDailyMinutes = Math.Round(dailyStats.Count > 0 ? dailyStats.Average((DailyStat stat) => stat.TotalMinutes) : 0.0, 1)
		};
	}

	private static int ComputeStreak(List<DailyStat> dailyStats)
	{
		List<DateTime> dates = dailyStats.Where((DailyStat stat) => stat.TotalMinutes > 0.0).Select((DailyStat stat) => DateTime.ParseExact(stat.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture)).Where((DateTime date) => date <= DateTime.Today).Distinct().OrderByDescending((DateTime date) => date).ToList();
		if (dates.Count == 0 || (DateTime.Today - dates[0]).TotalDays > 1.0)
		{
			return 0;
		}
		int streak = 1;
		for (int i = 1; i < dates.Count && (dates[i - 1] - dates[i]).TotalDays <= 1.0; i++)
		{
			streak++;
		}
		return streak;
	}

	private static List<GamePlayed> ComputePeriodGames(Dictionary<Guid, GamePlayed> games)
	{
		DateTime cutoff = DateTime.Now.AddDays(-30.0);
		List<GamePlayed> result = games.Values.Where((GamePlayed game) => game.LastPlayed >= cutoff && game.MinutesPlayed > 0.0).OrderByDescending((GamePlayed game) => game.MinutesPlayed).Take(6).ToList();
		return result.Count > 0 ? result : games.Values.Where((GamePlayed game) => game.MinutesPlayed > 0.0).OrderByDescending((GamePlayed game) => game.MinutesPlayed).Take(6).ToList();
	}

	private static List<CategoryStat> ComputeCategoryStats(Dictionary<Guid, Game> games, Dictionary<Guid, string> categoryNames, Dictionary<Guid, GamePlayed> gamePlaytime, Dictionary<string, Dictionary<string, double>> dailyMinutesByGame, DateTime? fromDate, DateTime? toDate)
	{
		Dictionary<string, CategoryStat> result = new Dictionary<string, CategoryStat>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<Guid, GamePlayed> pair in gamePlaytime)
		{
			if (!games.TryGetValue(pair.Key, out Game game) || game.CategoryIds == null)
			{
				continue;
			}
			double minutes = pair.Value.MinutesPlayed;
			if (fromDate.HasValue || toDate.HasValue)
			{
				minutes = 0.0;
				if (dailyMinutesByGame.TryGetValue(pair.Value.GameId, out Dictionary<string, double> dates))
				{
					foreach (KeyValuePair<string, double> date in dates)
					{
						DateTime parsed = DateTime.ParseExact(date.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture);
						if ((!fromDate.HasValue || parsed >= fromDate.Value.Date) && (!toDate.HasValue || parsed <= toDate.Value.Date))
						{
							minutes += date.Value;
						}
					}
				}
			}
			if (minutes <= 0.0001)
			{
				continue;
			}
			foreach (Guid categoryId in game.CategoryIds)
			{
				string name = categoryNames.TryGetValue(categoryId, out string categoryName) ? categoryName : PluginLocalization.Get("DataSourceOther", "Other");
				if (!result.TryGetValue(name, out CategoryStat stat))
				{
					stat = new CategoryStat { CategoryName = name };
					result[name] = stat;
				}
				stat.TotalMinutes += minutes;
				stat.GameCount++;
			}
		}
		return result.Values.ToList();
	}

	private static List<HourlyDateStat> ComputeHourlyDateStats(List<SessionRecord> sessions, Dictionary<string, string> gameNames)
	{
		Dictionary<string, HourlyDateStat> result = new Dictionary<string, HourlyDateStat>(StringComparer.Ordinal);
		foreach (SessionRecord session in sessions)
		{
			string gameName = gameNames.TryGetValue(session.GameId, out string name) ? name : null;
			foreach (SessionSlice slice in SliceSession(session, "hour"))
			{
				string date = slice.LocalStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
				string key = date + "|" + slice.LocalStart.Hour;
				if (!result.TryGetValue(key, out HourlyDateStat stat))
				{
					stat = new HourlyDateStat { Date = date, Hour = slice.LocalStart.Hour, GameDurations = new List<GameTimeDetail>() };
					result[key] = stat;
				}
				stat.TotalMinutes += slice.Minutes;
				if (IsRecoveredSession(session)) stat.RecoveredMinutes += slice.Minutes; else stat.ExactMinutes += slice.Minutes;
				stat.SessionCount++;
				AddGameMinutes(stat.GameDurations, session.GameId, gameName, slice.Minutes);
			}
		}
		foreach (HourlyDateStat stat in result.Values)
		{
			stat.TotalMinutes = Math.Round(stat.TotalMinutes, 1);
			stat.ExactMinutes = Math.Round(stat.ExactMinutes, 1);
			stat.RecoveredMinutes = Math.Round(stat.RecoveredMinutes, 1);
			SortGameMinutes(stat.GameDurations);
		}
		return result.Values.OrderBy((HourlyDateStat stat) => stat.Date).ThenBy((HourlyDateStat stat) => stat.Hour).ToList();
	}

	private static void AddSessionToDaily(Dictionary<string, DailyStat> dailyByDate, Dictionary<string, Dictionary<string, double>> actualMinutesByGame, Dictionary<string, double> actualMinutesByDate, SessionRecord session, Dictionary<string, string> gameNames)
	{
		string gameName = gameNames.TryGetValue(session.GameId, out string name) ? name : null;
		foreach (SessionSlice slice in SliceSession(session, "day"))
		{
			string date = slice.LocalStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			DailyStat stat = GetOrCreateDaily(dailyByDate, date);
			stat.TotalMinutes += slice.Minutes;
			if (IsRecoveredSession(session)) stat.RecoveredMinutes += slice.Minutes; else stat.ExactMinutes += slice.Minutes;
			stat.SessionCount++;
			AddGameMinutes(stat.GameDurations, session.GameId, gameName, slice.Minutes);
			AddGameDateMinutes(actualMinutesByGame, session.GameId, date, slice.Minutes);
			AddMinutes(actualMinutesByDate, date, slice.Minutes);
		}
	}

	private static DailyStat GetOrCreateDaily(Dictionary<string, DailyStat> dailyByDate, string date)
	{
		if (!dailyByDate.TryGetValue(date, out DailyStat stat))
		{
			stat = new DailyStat { Date = date, GameDurations = new List<GameTimeDetail>() };
			dailyByDate[date] = stat;
		}
		return stat;
	}

	private static IEnumerable<SessionSlice> SliceSession(SessionRecord session, string boundary)
	{
		DateTime startUtc = GetSessionStartUtc(session);
		DateTime endUtc = GetSessionEndUtc(session);
		if (startUtc <= DateTime.MinValue || endUtc <= startUtc)
		{
			yield break;
		}
		double durationSeconds = GetDurationSeconds(session);
		double spanSeconds = Math.Max(1.0, (endUtc - startUtc).TotalSeconds);
		DateTime cursor = startUtc;
		while (cursor < endUtc)
		{
			DateTime local = cursor.ToLocalTime();
			DateTime nextLocal = boundary == "day" ? local.Date.AddDays(1.0) : new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Local).AddHours(1.0);
			DateTime nextUtc = nextLocal.ToUniversalTime();
			if (nextUtc <= cursor) nextUtc = cursor.AddHours(1.0);
			DateTime segmentEnd = nextUtc < endUtc ? nextUtc : endUtc;
			double seconds = (segmentEnd - cursor).TotalSeconds;
			if (seconds > 0.0)
			{
				yield return new SessionSlice { LocalStart = local, Minutes = durationSeconds * seconds / spanSeconds / 60.0 };
			}
			cursor = segmentEnd;
		}
	}

	private static void AddGameDateMinutes(Dictionary<string, Dictionary<string, double>> values, string gameId, string date, double minutes)
	{
		if (!values.TryGetValue(gameId, out Dictionary<string, double> dates))
		{
			dates = new Dictionary<string, double>(StringComparer.Ordinal);
			values[gameId] = dates;
		}
		AddMinutes(dates, date, minutes);
	}

	private static void AddMinutes(Dictionary<string, double> values, string date, double minutes)
	{
		values[date] = (values.TryGetValue(date, out double current) ? current : 0.0) + minutes;
	}

	private static void AddGameMinutes(List<GameTimeDetail> details, string gameId, string gameName, double minutes)
	{
		if (details == null || string.IsNullOrWhiteSpace(gameId) || minutes <= 0.0001)
		{
			return;
		}
		GameTimeDetail detail = details.FirstOrDefault((GameTimeDetail item) => string.Equals(item.GameId, gameId, StringComparison.OrdinalIgnoreCase));
		if (detail == null)
		{
			detail = new GameTimeDetail { GameId = gameId, GameName = gameName };
			details.Add(detail);
		}
		detail.Minutes += minutes;
	}

	private static void FinalizeDailyStat(DailyStat stat)
	{
		stat.TotalMinutes = Math.Round(stat.TotalMinutes, 1);
		stat.ExactMinutes = Math.Round(stat.ExactMinutes, 1);
		stat.RecoveredMinutes = Math.Round(stat.RecoveredMinutes, 1);
		SortGameMinutes(stat.GameDurations);
	}

	private static void SortGameMinutes(List<GameTimeDetail> details)
	{
		if (details == null) return;
		foreach (GameTimeDetail detail in details) detail.Minutes = Math.Round(detail.Minutes, 1);
		details.Sort((GameTimeDetail left, GameTimeDetail right) => right.Minutes.CompareTo(left.Minutes));
	}

	private static bool IsRecoveredSession(SessionRecord session)
	{
		return session.Source == SessionSources.Recovered || session.Confidence == SessionConfidence.Recovered;
	}

	private static double GetDurationMinutes(SessionRecord session)
	{
		return GetDurationSeconds(session) / 60.0;
	}

	private static double GetDurationSeconds(SessionRecord session)
	{
		if (session.DurationSeconds > 0) return session.DurationSeconds;
		if (session.ElapsedSeconds > 0) return session.ElapsedSeconds;
		DateTime start = GetSessionStartUtc(session);
		DateTime end = GetSessionEndUtc(session);
		return end > start ? (end - start).TotalSeconds : 0.0;
	}

	private static DateTime GetSessionStartUtc(SessionRecord session)
	{
		DateTime start = ToUtc(session.StartUtc);
		if (start > DateTime.MinValue) return start;
		DateTime end = GetSessionEndUtc(session);
		return end > DateTime.MinValue ? end.AddSeconds(-GetDurationSeconds(session)) : DateTime.MinValue;
	}

	private static DateTime GetSessionEndUtc(SessionRecord session)
	{
		DateTime end = ToUtc(session.EndUtc);
		if (end > DateTime.MinValue) return end;
		return ToUtc(session.Timestamp);
	}

	private static DateTime GetLastPlayed(Game game, List<SessionRecord> sessions, SteamAppStats steamStats)
	{
		DateTime result = ToLocal(game.LastActivity);
		DateTime steam = ToLocal(steamStats?.LastPlayed ?? DateTime.MinValue);
		if (steam > result) result = steam;
		foreach (SessionRecord session in sessions)
		{
			DateTime end = GetSessionEndUtc(session).ToLocalTime();
			if (end > result) result = end;
		}
		return result;
	}

	private static DateTime ToLocal(DateTime? value)
	{
		return value.HasValue ? ToLocal(value.Value) : DateTime.MinValue;
	}

	private static DateTime ToLocal(DateTime value)
	{
		if (value <= DateTime.MinValue) return DateTime.MinValue;
		if (value.Kind == DateTimeKind.Local) return value;
		if (value.Kind == DateTimeKind.Utc) return value.ToLocalTime();
		return DateTime.SpecifyKind(value, DateTimeKind.Local);
	}

	private static DateTime ToUtc(DateTime value)
	{
		if (value <= DateTime.MinValue) return DateTime.MinValue;
		if (value.Kind == DateTimeKind.Utc) return value;
		if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
		return DateTime.SpecifyKind(value, DateTimeKind.Utc);
	}

	private static string ExtractSteamAppId(string value)
	{
		if (string.IsNullOrEmpty(value)) return null;
		string best = "";
		string current = "";
		foreach (char c in value)
		{
			if (char.IsDigit(c))
			{
				current += c;
				if (current.Length > best.Length) best = current;
			}
			else current = "";
		}
		return best.Length > 0 ? best : null;
	}

	private class SessionSlice
	{
		public DateTime LocalStart;

		public double Minutes;
	}
}
