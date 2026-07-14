using System.Collections.Generic;

namespace PlayniteGameStats;

public class ChartDataPackage
{
	public SummaryStats Summary;

	public List<DailyStat> DailyStats;

	public List<GamePlayed> PeriodGames;

	public List<GamePlayed> TopGames;

	public List<GamePlayed> RecentGames;

	public List<CategoryStat> CategoryStats;

	public List<HourlyDateStat> HourlyDateStats;

	public List<CategoryStat> CategoryStatsWeek;

	public List<CategoryStat> CategoryStatsMonth;

	public List<CategoryStat> CategoryStatsYear;

	public bool HasSessionData;
}
