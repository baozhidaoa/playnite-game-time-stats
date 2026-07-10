using System;

namespace PlayniteGameStats;

public class GamePlayed
{
	public string GameId;

	public string GameName;

	public double MinutesPlayed;

	public double ExactMinutes;

	public double RecoveredMinutes;

	public double SteamDeltaMinutes;

	public double EstimatedMinutes;

	public int SessionCount;

	public bool IsEstimated;

	public string CoverImage;

	public string IconImage;

	public DateTime LastPlayed;

	public string DataSource;

	public string EstimateReason;

	public int UserScore;
}
