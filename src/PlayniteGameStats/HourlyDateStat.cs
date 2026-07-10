using System.Collections.Generic;

namespace PlayniteGameStats;

public class HourlyDateStat
{
	public string Date;

	public int Hour;

	public double TotalMinutes;

	public double ExactMinutes;

	public double RecoveredMinutes;

	public double SteamDeltaMinutes;

	public int SessionCount;

	public List<string> GameNames;
}
