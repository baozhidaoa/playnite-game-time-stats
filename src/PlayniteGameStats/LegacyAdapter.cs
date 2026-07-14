using System;
using System.Collections.Generic;
using System.Linq;
using Playnite.SDK.Models;

namespace PlayniteGameStats;

public class LegacyAdapter
{
	public List<DailyStat> SynthesizeStats(Game game, SteamAppStats steamStats)
	{
		List<DailyStat> list = new List<DailyStat>();
		if (steamStats != null)
		{
			DateTime dateTime = ((steamStats.LastPlayed > DateTime.MinValue) ? steamStats.LastPlayed.ToLocalTime().Date : DateTime.Today);
			if (steamStats.Playtime2WeeksMinutes > 0.0)
			{
				double totalMinutes = Math.Round(steamStats.Playtime2WeeksMinutes / 14.0, 1);
				for (int num = 13; num >= 0; num--)
				{
					list.Add(new DailyStat
					{
						Date = dateTime.AddDays(-num).ToString("yyyy-MM-dd"),
						TotalMinutes = totalMinutes,
						SessionCount = 1,
						IsEstimated = true,
						Source = PluginLocalization.Get("DataSourceSteamRecentEstimate", "Steam recent estimate")
					});
				}
				return list.Where((DailyStat s) => s.TotalMinutes > 0.0).ToList();
			}
			if (steamStats.LastPlayed > DateTime.MinValue && steamStats.PlaytimeMinutes > 0.0)
			{
				list.Add(new DailyStat
				{
					Date = dateTime.ToString("yyyy-MM-dd"),
					TotalMinutes = Math.Round(Math.Min(steamStats.PlaytimeMinutes, 60.0), 1),
					SessionCount = 1,
					IsEstimated = true,
					Source = PluginLocalization.Get("DataSourceSteamLastPlayedEstimate", "Steam last-played estimate")
				});
				return list;
			}
		}
		if (game.Playtime == 0L || !game.LastActivity.HasValue)
		{
			return new List<DailyStat>();
		}
		double val = (double)game.Playtime / 60.0;
		DateTime date = game.LastActivity.Value.Date;
		list.Add(new DailyStat
		{
			Date = date.ToString("yyyy-MM-dd"),
			TotalMinutes = Math.Round(Math.Min(val, 60.0), 1),
			SessionCount = 1,
			IsEstimated = true,
			Source = PluginLocalization.Get("DataSourcePlayniteLastPlayedEstimate", "Playnite last-played estimate")
		});
		return list;
	}

}
