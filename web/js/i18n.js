window.I18n = (function () {
  var defaults = {
    languageTag: "en-US",
    appTitle: "Game Time Statistics",
    appSubtitle: "Understand your play habits",
    refreshData: "Refresh data",
    totalPlaytime: "Total playtime",
    gamesPlayed: "Games played",
    currentStreak: "Current streak",
    dailyAverage: "Daily average",
    week: "Week",
    month: "Month",
    year: "Year",
    total: "Total",
    thisWeek: "This week",
    thisMonth: "This month",
    thisYear: "This year",
    allTime: "All time",
    heatmap: "Heatmap",
    barChart: "Bar",
    lineChart: "Line",
    playTrend: "Play trend",
    hourlyDistribution: "Hourly distribution",
    hourlyDefaultHint: "Average minutes per hour",
    genrePreference: "Genre preference",
    recentGames: "Recently played",
    topPlaytime: "Top playtime",
    favoriteGames: "Favorite games",
    legacyNotice:
      "Some data comes from cumulative playtime estimates. New sessions will provide more accurate stats.",
    emptyGameHint: "No data yet. Go play something.",
    noData: "No data",
    waitingData: "Waiting for data...",
    gamesCount: "{0} games",
    daysCount: "{0} days",
    playsCount: "{0} plays",
    playsCountShort: "{0} plays",
    estimatedData: "Estimated data",
    scoreMeta: "Score: {0} / {1} / {2}",
    legacySteamDelta: "Steam delta",
    legacyRecovered: "Recovered sessions",
    legacyEstimated: "Historical estimates",
    legacyBanner: "Includes {0} data. Hover charts to view source breakdown.",
    legacyJoin: ", ",
    dailyAverageWeek: "Daily average this week",
    dailyAverageMonth: "Daily average this month",
    dailyAverageYear: "Daily average this year",
    activeDayAverage: "Active-day average",
    daily: "Daily",
    dailyMinutes: "Daily minutes",
    hoursSuffix: "hours",
    hoursSeries: "Playtime (hours)",
    unitMinuteShort: "m",
    unitHourShort: "h",
    dayNames: ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"],
    monthNamesShort: [
      "Jan",
      "Feb",
      "Mar",
      "Apr",
      "May",
      "Jun",
      "Jul",
      "Aug",
      "Sep",
      "Oct",
      "Nov",
      "Dec",
    ],
    monthNamesLong: [
      "January",
      "February",
      "March",
      "April",
      "May",
      "June",
      "July",
      "August",
      "September",
      "October",
      "November",
      "December",
    ],
    hourLabels: [
      "0:00",
      "1",
      "2",
      "3",
      "4",
      "5",
      "6",
      "7",
      "8",
      "9",
      "10",
      "11",
      "12",
      "13",
      "14",
      "15",
      "16",
      "17",
      "18",
      "19",
      "20",
      "21",
      "22",
      "23",
    ],
  };

  var strings = {};
  set(window.__GTS_I18N__ || {});

  function set(next) {
    strings = {};
    merge(strings, defaults);
    merge(strings, next || {});
    apply();
  }

  function merge(target, source) {
    for (var key in source) {
      if (Object.prototype.hasOwnProperty.call(source, key)) {
        target[key] = source[key];
      }
    }
  }

  function t(key, fallback) {
    var value = strings[key];
    if (value === undefined || value === null || value === "") {
      return fallback !== undefined ? fallback : key;
    }
    return value;
  }

  function format(key, fallback) {
    var args = Array.prototype.slice.call(arguments, 2);
    return String(t(key, fallback)).replace(/\{(\d+)\}/g, function (_, index) {
      var arg = args[Number(index)];
      return arg === undefined || arg === null ? "" : arg;
    });
  }

  function list(key, fallback) {
    var value = t(key, fallback || []);
    if (Array.isArray(value)) return value;
    if (typeof value === "string") return value.split("|");
    return fallback || [];
  }

  function lang() {
    return String(t("languageTag", "en-US"));
  }

  function isZh() {
    return /^zh/i.test(lang());
  }

  function apply() {
    if (!document || !document.querySelectorAll) return;
    document.documentElement.lang = lang();
    var textNodes = document.querySelectorAll("[data-i18n]");
    for (var i = 0; i < textNodes.length; i++) {
      textNodes[i].textContent = t(textNodes[i].getAttribute("data-i18n"));
    }
    var titleNodes = document.querySelectorAll("[data-i18n-title]");
    for (var j = 0; j < titleNodes.length; j++) {
      titleNodes[j].setAttribute(
        "title",
        t(titleNodes[j].getAttribute("data-i18n-title")),
      );
    }
  }

  return {
    set: set,
    t: t,
    format: format,
    list: list,
    lang: lang,
    isZh: isZh,
    apply: apply,
  };
})();
