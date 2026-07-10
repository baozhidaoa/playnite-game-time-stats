using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Playnite.SDK.Models;

namespace PlayniteGameStats;

public class SteamDeltaImporter
{
	private const int CurrentSchemaVersion = 1;

	private static readonly object SyncLock = new object();

	private readonly string _snapshotPath;

	private readonly SessionStore _sessionStore;

	public SteamDeltaImporter(string pluginDataPath, SessionStore sessionStore)
	{
		_snapshotPath = Path.Combine(pluginDataPath, "steam-snapshots.json");
		_sessionStore = sessionStore;
	}

	public void Import(List<Game> games, Dictionary<string, SteamAppStats> steamStats)
	{
		if (games == null || steamStats == null || steamStats.Count == 0)
		{
			return;
		}
		lock (SyncLock)
		{
			DateTime capturedUtc = DateTime.UtcNow;
			SteamSnapshotFile previous = Load();
			Dictionary<string, Game> gamesByAppId = BuildGameMap(games);
			List<SessionRecord> existingSessions = _sessionStore.GetAllSessions();
			foreach (KeyValuePair<string, SteamAppStats> pair in steamStats)
			{
				string appId = NormalizeAppId(pair.Value.AppId) ?? NormalizeAppId(pair.Key);
				if (string.IsNullOrEmpty(appId))
				{
					continue;
				}
				double currentTotal = GetTotalMinutes(pair.Value);
				if (currentTotal < 1.0)
				{
					continue;
				}
				if (previous.Apps.TryGetValue(appId, out SteamAppSnapshot oldSnapshot) && gamesByAppId.TryGetValue(appId, out Game game))
				{
					double deltaMinutes = currentTotal - oldSnapshot.TotalMinutes;
					if (deltaMinutes >= 1.0)
					{
						DateTime windowStartUtc = NormalizeUtc(oldSnapshot.CapturedUtc);
						DateTime windowEndUtc = capturedUtc;
						if (windowStartUtc <= DateTime.MinValue || windowStartUtc >= windowEndUtc)
						{
							windowStartUtc = windowEndUtc.AddMinutes(-deltaMinutes);
						}
						double coveredMinutes = GetCoveredMinutes(existingSessions, game.Id.ToString(), windowStartUtc, windowEndUtc);
						double importedMinutes = Math.Round(Math.Max(0.0, deltaMinutes - coveredMinutes), 2);
						if (importedMinutes >= 1.0)
						{
							DateTime preferredEndUtc = ChooseDeltaEnd(pair.Value, game, windowEndUtc);
							ChooseDeltaWindow(preferredEndUtc, importedMinutes, out DateTime startUtc, out DateTime endUtc);
							SessionRecord record = new SessionRecord
							{
								GameId = game.Id.ToString(),
								StartUtc = startUtc,
								EndUtc = endUtc,
								DurationSeconds = (long)Math.Round(importedMinutes * 60.0),
								ElapsedSeconds = (long)Math.Round(importedMinutes * 60.0),
								Timestamp = endUtc,
								Source = SessionSources.SteamDelta,
								Confidence = SessionConfidence.Delta,
								ExternalAppId = appId
							};
							_sessionStore.AddImportedSession(record);
						}
					}
				}
			}
			Save(BuildSnapshot(steamStats, capturedUtc));
		}
	}

	private SteamSnapshotFile Load()
	{
		try
		{
			if (File.Exists(_snapshotPath))
			{
				string value = File.ReadAllText(_snapshotPath);
				SteamSnapshotFile file = JsonConvert.DeserializeObject<SteamSnapshotFile>(value) ?? new SteamSnapshotFile();
				if (file.Apps == null)
				{
					file.Apps = new Dictionary<string, SteamAppSnapshot>(StringComparer.OrdinalIgnoreCase);
				}
				return file;
			}
		}
		catch
		{
		}
		return new SteamSnapshotFile();
	}

	private void Save(SteamSnapshotFile snapshot)
	{
		try
		{
			string directoryName = Path.GetDirectoryName(_snapshotPath);
			if (!Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			File.WriteAllText(_snapshotPath, JsonConvert.SerializeObject(snapshot, Formatting.Indented));
		}
		catch
		{
		}
	}

	private static SteamSnapshotFile BuildSnapshot(Dictionary<string, SteamAppStats> steamStats, DateTime capturedUtc)
	{
		SteamSnapshotFile file = new SteamSnapshotFile
		{
			SchemaVersion = CurrentSchemaVersion,
			CapturedUtc = capturedUtc,
			Apps = new Dictionary<string, SteamAppSnapshot>(StringComparer.OrdinalIgnoreCase)
		};
		foreach (KeyValuePair<string, SteamAppStats> pair in steamStats)
		{
			string appId = NormalizeAppId(pair.Value.AppId) ?? NormalizeAppId(pair.Key);
			if (string.IsNullOrEmpty(appId))
			{
				continue;
			}
			file.Apps[appId] = new SteamAppSnapshot
			{
				AppId = appId,
				TotalMinutes = GetTotalMinutes(pair.Value),
				PlaytimeMinutes = Math.Max(pair.Value.PlaytimeMinutes, 0.0),
				PlaytimeDisconnectedMinutes = Math.Max(pair.Value.PlaytimeDisconnectedMinutes, 0.0),
				Playtime2WeeksMinutes = Math.Max(pair.Value.Playtime2WeeksMinutes, 0.0),
				LastPlayedUtc = NormalizeUtc(pair.Value.LastPlayed),
				CapturedUtc = capturedUtc
			};
		}
		return file;
	}

	private static Dictionary<string, Game> BuildGameMap(List<Game> games)
	{
		Dictionary<string, Game> result = new Dictionary<string, Game>(StringComparer.OrdinalIgnoreCase);
		foreach (Game game in games)
		{
			string appId = NormalizeAppId(game.GameId);
			if (!string.IsNullOrEmpty(appId) && !result.ContainsKey(appId))
			{
				result[appId] = game;
			}
		}
		return result;
	}

	private static double GetCoveredMinutes(List<SessionRecord> sessions, string gameId, DateTime fromUtc, DateTime toUtc)
	{
		double total = 0.0;
		foreach (SessionRecord session in sessions.Where((SessionRecord s) => s.GameId == gameId && IsPlayniteTracked(s)))
		{
			total += GetOverlapMinutes(session, fromUtc, toUtc);
		}
		return total;
	}

	private static bool IsPlayniteTracked(SessionRecord session)
	{
		return session.Source == SessionSources.Playnite || session.Source == SessionSources.Recovered || string.IsNullOrEmpty(session.Source);
	}

	private static double GetOverlapMinutes(SessionRecord session, DateTime fromUtc, DateTime toUtc)
	{
		DateTime startUtc = NormalizeUtc(session.StartUtc);
		DateTime endUtc = NormalizeUtc(session.EndUtc);
		if (startUtc <= DateTime.MinValue && session.Timestamp > DateTime.MinValue && session.ElapsedSeconds > 0)
		{
			endUtc = NormalizeUtc(session.Timestamp);
			startUtc = endUtc.AddSeconds(-session.ElapsedSeconds);
		}
		if (startUtc <= DateTime.MinValue || endUtc <= startUtc)
		{
			return 0.0;
		}
		DateTime overlapStart = startUtc > fromUtc ? startUtc : fromUtc;
		DateTime overlapEnd = endUtc < toUtc ? endUtc : toUtc;
		if (overlapEnd <= overlapStart)
		{
			return 0.0;
		}
		double totalSeconds = GetDurationSeconds(session);
		double spanSeconds = Math.Max(1.0, (endUtc - startUtc).TotalSeconds);
		double overlapSeconds = (overlapEnd - overlapStart).TotalSeconds;
		return totalSeconds * (overlapSeconds / spanSeconds) / 60.0;
	}

	private static DateTime ChooseDeltaEnd(SteamAppStats stats, Game game, DateTime windowEndUtc)
	{
		DateTime lastPlayed = NormalizeUtc(stats?.LastPlayed ?? DateTime.MinValue);
		if (IsUsableLastPlayed(lastPlayed, windowEndUtc))
		{
			return lastPlayed > windowEndUtc ? windowEndUtc : lastPlayed;
		}
		DateTime playniteLastActivity = NormalizePlayniteLastActivity(game);
		if (IsUsableLastPlayed(playniteLastActivity, windowEndUtc))
		{
			return playniteLastActivity > windowEndUtc ? windowEndUtc : playniteLastActivity;
		}
		return windowEndUtc;
	}

	private static bool IsUsableLastPlayed(DateTime valueUtc, DateTime windowEndUtc)
	{
		return valueUtc > DateTime.MinValue && valueUtc <= windowEndUtc.AddHours(6.0);
	}

	private static void ChooseDeltaWindow(DateTime preferredEndUtc, double importedMinutes, out DateTime startUtc, out DateTime endUtc)
	{
		DateTime endLocal = preferredEndUtc.ToLocalTime();
		DateTime dayStartLocal = endLocal.Date;
		DateTime dayEndLocal = dayStartLocal.AddDays(1.0).AddSeconds(-1.0);
		if (endLocal <= dayStartLocal)
		{
			endLocal = dayStartLocal.AddMinutes(1.0);
		}
		if (endLocal > dayEndLocal)
		{
			endLocal = dayEndLocal;
		}
		double availableMinutes = Math.Max(1.0, (endLocal - dayStartLocal).TotalMinutes);
		double spanMinutes = Math.Min(Math.Max(importedMinutes, 1.0), availableMinutes);
		DateTime startLocal = endLocal.AddMinutes(-spanMinutes);
		if (startLocal < dayStartLocal)
		{
			startLocal = dayStartLocal;
		}
		if (endLocal <= startLocal)
		{
			endLocal = startLocal.AddMinutes(1.0);
		}
		startUtc = startLocal.ToUniversalTime();
		endUtc = endLocal.ToUniversalTime();
	}

	private static double GetTotalMinutes(SteamAppStats stats)
	{
		if (stats == null)
		{
			return 0.0;
		}
		return Math.Max(stats.PlaytimeMinutes, 0.0) + Math.Max(stats.PlaytimeDisconnectedMinutes, 0.0);
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
		DateTime startUtc = NormalizeUtc(session.StartUtc);
		DateTime endUtc = NormalizeUtc(session.EndUtc);
		if (startUtc > DateTime.MinValue && endUtc > startUtc)
		{
			return (endUtc - startUtc).TotalSeconds;
		}
		return 0.0;
	}

	private static DateTime NormalizePlayniteLastActivity(Game game)
	{
		if (game?.LastActivity == null || game.LastActivity.Value <= DateTime.MinValue)
		{
			return DateTime.MinValue;
		}
		DateTime value = game.LastActivity.Value;
		if (value.Kind == DateTimeKind.Utc)
		{
			return value;
		}
		if (value.Kind == DateTimeKind.Local)
		{
			return value.ToUniversalTime();
		}
		return DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
	}

	private static string NormalizeAppId(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return null;
		}
		string text = "";
		string text2 = "";
		foreach (char c in value)
		{
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
		return text.Length > 0 ? text : null;
	}

	private static DateTime NormalizeUtc(DateTime value)
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
}
