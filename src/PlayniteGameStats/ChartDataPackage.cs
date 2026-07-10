using System.Collections.Generic;

namespace PlayniteGameStats;

public class ChartDataPackage
{
	public SummaryStats Summary;

	public List<DailyStat> DailyStats;

	public List<GamePlayed> PeriodGames;

	public List<GamePlayed> TopGames;

	public List<GamePlayed> RecentGames;

	public List<GenreStat> GenreStats;

	public List<HourlyDateStat> HourlyDateStats;

	public List<GenreStat> GenreStatsWeek;

	public List<GenreStat> GenreStatsMonth;

	public List<GenreStat> GenreStatsYear;

	public bool HasSessionData;
}
