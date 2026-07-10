using System.Collections.Generic;

namespace PlayniteGameStats;

public class DailyStat
{
	public string Date;

	public double TotalMinutes;

	public double ExactMinutes;

	public double RecoveredMinutes;

	public double SteamDeltaMinutes;

	public double EstimatedMinutes;

	public int SessionCount;

	public bool IsEstimated;

	public string Source;

	public string SourceBreakdown;

	public List<string> GameNames;
}
