using System;
using System.Collections.Generic;
using System.Globalization;
using Playnite.SDK;

namespace PlayniteGameStats;

internal static class PluginLocalization
{
	private const string Prefix = "LOCGameTimeStats";

	public static string LanguageTag => Get("LanguageTag", "en-US");

	public static string Get(string suffix, string fallback)
	{
		string key = Prefix + suffix;
		try
		{
			string text = ResourceProvider.GetString(key);
			if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, key, StringComparison.Ordinal))
			{
				return text;
			}
		}
		catch
		{
		}
		return fallback;
	}

	public static string Format(string suffix, string fallback, params object[] args)
	{
		return string.Format(CultureInfo.CurrentCulture, Get(suffix, fallback), args);
	}

	public static Dictionary<string, object> GetWebStrings()
	{
		return new Dictionary<string, object>
		{
			["languageTag"] = LanguageTag,
			["appTitle"] = Get("WebAppTitle", "Game Time Statistics"),
			["appSubtitle"] = Get("WebAppSubtitle", "Understand your play habits"),
			["refreshData"] = Get("WebRefreshData", "Refresh data"),
			["totalPlaytime"] = Get("WebTotalPlaytime", "Total playtime"),
			["gamesPlayed"] = Get("WebGamesPlayed", "Games played"),
			["currentStreak"] = Get("WebCurrentStreak", "Current streak"),
			["dailyAverage"] = Get("WebDailyAverage", "Daily average"),
			["week"] = Get("WebWeek", "Week"),
			["month"] = Get("WebMonth", "Month"),
			["year"] = Get("WebYear", "Year"),
			["total"] = Get("WebTotal", "Total"),
			["thisWeek"] = Get("WebThisWeek", "This week"),
			["thisMonth"] = Get("WebThisMonth", "This month"),
			["thisYear"] = Get("WebThisYear", "This year"),
			["allTime"] = Get("WebAllTime", "All time"),
			["heatmap"] = Get("WebHeatmap", "Heatmap"),
			["barChart"] = Get("WebBarChart", "Bar"),
			["lineChart"] = Get("WebLineChart", "Line"),
			["playTrend"] = Get("WebPlayTrend", "Play trend"),
			["hourlyDistribution"] = Get("WebHourlyDistribution", "Hourly distribution"),
			["hourlyDefaultHint"] = Get("WebHourlyDefaultHint", "Average minutes per hour"),
			["genrePreference"] = Get("WebGenrePreference", "Genre preference"),
			["recentGames"] = Get("WebRecentGames", "Recently played"),
			["topPlaytime"] = Get("WebTopPlaytime", "Top playtime"),
			["favoriteGames"] = Get("WebFavoriteGames", "Favorite games"),
			["legacyNotice"] = Get("WebLegacyNotice", "Some data comes from cumulative playtime estimates. New sessions will provide more accurate stats."),
			["emptyGameHint"] = Get("WebEmptyGameHint", "No data yet. Go play something."),
			["noData"] = Get("WebNoData", "No data"),
			["waitingData"] = Get("WebWaitingData", "Waiting for data..."),
			["gamesCount"] = Get("WebGamesCount", "{0} games"),
			["daysCount"] = Get("WebDaysCount", "{0} days"),
			["playsCount"] = Get("WebPlaysCount", "{0} plays"),
			["playsCountShort"] = Get("WebPlaysCountShort", "{0} plays"),
			["estimatedData"] = Get("WebEstimatedData", "Estimated data"),
			["scoreMeta"] = Get("WebScoreMeta", "Score: {0} / {1} / {2}"),
			["legacySteamDelta"] = Get("WebLegacySteamDelta", "Steam delta"),
			["legacyRecovered"] = Get("WebLegacyRecovered", "Recovered sessions"),
			["legacyEstimated"] = Get("WebLegacyEstimated", "Historical estimates"),
			["legacyBanner"] = Get("WebLegacyBanner", "Includes {0} data. Hover charts to view source breakdown."),
			["legacyJoin"] = Get("WebLegacyJoin", ", "),
			["dailyAverageWeek"] = Get("WebDailyAverageWeek", "Daily average this week"),
			["dailyAverageMonth"] = Get("WebDailyAverageMonth", "Daily average this month"),
			["dailyAverageYear"] = Get("WebDailyAverageYear", "Daily average this year"),
			["activeDayAverage"] = Get("WebActiveDayAverage", "Active-day average"),
			["daily"] = Get("WebDaily", "Daily"),
			["dailyMinutes"] = Get("WebDailyMinutes", "Daily minutes"),
			["hoursSuffix"] = Get("WebHoursSuffix", "hours"),
			["hoursSeries"] = Get("WebHoursSeries", "Playtime (hours)"),
			["unitMinuteShort"] = Get("WebUnitMinuteShort", "m"),
			["unitHourShort"] = Get("WebUnitHourShort", "h"),
			["dayNames"] = Split("WebDayNames", "Mon|Tue|Wed|Thu|Fri|Sat|Sun"),
			["monthNamesShort"] = Split("WebMonthNamesShort", "Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec"),
			["monthNamesLong"] = Split("WebMonthNamesLong", "January|February|March|April|May|June|July|August|September|October|November|December"),
			["hourLabels"] = Split("WebHourLabels", "0:00|1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|16|17|18|19|20|21|22|23")
		};
	}

	private static string[] Split(string suffix, string fallback)
	{
		return Get(suffix, fallback).Split('|');
	}
}
