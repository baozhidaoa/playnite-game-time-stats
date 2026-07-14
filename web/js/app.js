window.GameStats = (function () {
  var mainChart, hourlyChart, categoryChart;
  var fullData = null;
  var currentPeriod = "week";
  var currentChartType = "heatmap";
  var periodOffset = 0;

  function init() {
    applyLocalization();
    var mainDom = document.getElementById("mainChart");
    var hourlyDom = document.getElementById("hourlyChart");
    var categoryDom = document.getElementById("categoryChart");

    if (mainDom)
      mainChart = echarts.init(mainDom, null, { renderer: "canvas" });
    if (hourlyDom)
      hourlyChart = echarts.init(hourlyDom, null, { renderer: "canvas" });
    if (categoryDom)
      categoryChart = echarts.init(categoryDom, null, { renderer: "canvas" });

    window.addEventListener("resize", function () {
      if (mainChart) mainChart.resize();
      if (hourlyChart) hourlyChart.resize();
      if (categoryChart) categoryChart.resize();
    });

    // Period tabs
    document.querySelectorAll(".period-tab").forEach(function (tab) {
      tab.addEventListener("click", function () {
        currentPeriod = this.getAttribute("data-period");
        periodOffset = 0;
        UI.setActiveTab(".period-tab", "active", currentPeriod);
        renderAll();
      });
    });

    // Navigation
    document.getElementById("navPrev").addEventListener("click", function () {
      periodOffset--;
      renderAll();
    });
    document.getElementById("navNext").addEventListener("click", function () {
      periodOffset++;
      renderAll();
    });

    // Chart type
    document.querySelectorAll(".chart-btn").forEach(function (btn) {
      btn.addEventListener("click", function () {
        currentChartType = this.getAttribute("data-chart");
        UI.setActiveTab(".chart-btn", "active", currentChartType);
        renderMainChart();
        UI.updateChartLegend(currentChartType);
      });
    });

    // Refresh button - reload the page with cache busting
    var refreshBtn = document.getElementById("refreshBtn");
    if (refreshBtn) {
      refreshBtn.addEventListener("click", function () {
        var url = window.location.href.split("?")[0];
        window.location.href = url + "?t=" + new Date().getTime();
      });
    }

    // The host injects the initial data after the local page finishes loading.
    if (window.__STATS_DATA__) {
      loadData(window.__STATS_DATA__);
    } else {
      showEmpty(I18n.t("waitingData"));
    }
  }

  function showEmpty(msg) {
    var emp = {
      title: {
        text: msg,
        left: "center",
        top: "center",
        textStyle: { color: "#555", fontSize: 13 },
      },
    };
    if (mainChart) mainChart.setOption(emp);
    if (hourlyChart) hourlyChart.setOption(emp);
    if (categoryChart) categoryChart.setOption(emp);
  }

  function loadData(jsonData) {
    applyLocalization();
    fullData = jsonData;
    renderAll();
  }

  // Called by DLL via injected script (PushData)
  function reload() {
    if (window.__STATS_DATA__) {
      applyLocalization();
      fullData = window.__STATS_DATA__;
      renderAll();
    }
  }

  function renderAll() {
    if (!fullData) return;
    applyLocalization();
    UI.updateSummaryCards(fullData.Summary);
    renderMainChart();
    renderHourlyChart();
    renderCategoryChart();
    renderGameLists();
    UI.updateLegacyBanner(fullData);
    UI.updatePeriodLabel(getPeriodLabel());
  }

  function renderMainChart() {
    if (!mainChart || !fullData) return;
    var mainDom = document.getElementById("mainChart");
    var chartHolder = mainDom ? mainDom.parentElement : null;
    var dailyData = filterDailyData();
    var range = getCalendarRange();
    var option;

    switch (currentChartType) {
      case "heatmap":
        option = Charts.createHeatmapOption(dailyData, range, currentPeriod);
        var size = Charts.getHeatmapLayoutSize(range, currentPeriod);
        if (chartHolder)
          chartHolder.style.overflowX =
            currentPeriod === "year" || currentPeriod === "total"
              ? "auto"
              : "hidden";
        if (mainDom) {
          mainDom.style.width =
            currentPeriod === "year" || currentPeriod === "total"
              ? Math.max(
                  size.width,
                  chartHolder ? chartHolder.clientWidth : 0,
                ) + "px"
              : "100%";
          mainDom.style.height = size.height + "px";
        }
        break;
      case "bar":
        option = Charts.createBarOption(dailyData, currentPeriod === "total");
        if (chartHolder) chartHolder.style.overflowX = "hidden";
        if (mainDom) {
          mainDom.style.width = "100%";
          mainDom.style.height = "320px";
        }
        break;
      case "line":
        option = Charts.createLineOption(dailyData, currentPeriod === "total");
        if (chartHolder) chartHolder.style.overflowX = "hidden";
        if (mainDom) {
          mainDom.style.width = "100%";
          mainDom.style.height = "320px";
        }
        break;
    }
    mainChart.setOption(option, true);
    mainChart.resize();
  }

  function renderHourlyChart() {
    if (!hourlyChart || !fullData) return;
    UI.updateHourlyHint(getHourlyHint());
    hourlyChart.setOption(
      Charts.createHourlyOption(getPeriodHourlyData()),
      true,
    );
  }

  function renderCategoryChart() {
    if (!categoryChart || !fullData) return;
    var data = getPeriodCategoryData();
    var labels = getPeriodSummaryLabels();
    var hint = document.getElementById("categoryFilterHint");
    if (hint) hint.textContent = labels[currentPeriod] || I18n.t("total");
    categoryChart.setOption(
      Charts.createCategoryRadarOption(data, labels[currentPeriod] || I18n.t("total")),
      true,
    );
  }

  function getPeriodHourlyData() {
    var rows = (fullData && fullData.HourlyDateStats) || [];
    var buckets = [];
    for (var i = 0; i < 24; i++) {
      buckets.push({
        Hour: i,
        TotalMinutes: 0,
        ExactMinutes: 0,
        RecoveredMinutes: 0,
        SteamDeltaMinutes: 0,
        SessionCount: 0,
        GameNames: [],
      });
    }
    if (!rows.length) return buckets;

    var range = getEffectiveHourlyRange(getPeriodDateRange());
    var filtered = rows.filter(function (h) {
      return !range || (h.Date >= range.start && h.Date <= range.end);
    });
    var divisor = getHourlyAverageDivisor(filtered, range);
    if (divisor <= 0) return buckets;

    filtered.forEach(function (h) {
      var hour = Number(h.Hour);
      if (hour < 0 || hour > 23 || !buckets[hour]) return;
      buckets[hour].TotalMinutes += h.TotalMinutes || 0;
      buckets[hour].ExactMinutes += h.ExactMinutes || 0;
      buckets[hour].RecoveredMinutes += h.RecoveredMinutes || 0;
      buckets[hour].SteamDeltaMinutes += h.SteamDeltaMinutes || 0;
      buckets[hour].SessionCount += h.SessionCount || 0;
      mergeGameNames(buckets[hour].GameNames, h.GameNames);
    });

    buckets.forEach(function (h) {
      h.TotalMinutes = round1(h.TotalMinutes / divisor);
      h.ExactMinutes = round1(h.ExactMinutes / divisor);
      h.RecoveredMinutes = round1(h.RecoveredMinutes / divisor);
      h.SteamDeltaMinutes = round1(h.SteamDeltaMinutes / divisor);
      h.AverageDays = divisor;
    });
    return buckets;
  }

  function getHourlyAverageDivisor(filtered, range) {
    if (currentPeriod === "total") {
      var activeDates = {};
      filtered.forEach(function (h) {
        if (h.TotalMinutes > 0) activeDates[h.Date] = true;
      });
      return Math.max(1, Object.keys(activeDates).length);
    }
    if (!range) return 1;
    var start = parseDate(range.start);
    var end = parseDate(range.end);
    if (end < start) return 0;
    return Math.floor((end - start) / 86400000) + 1;
  }

  function getEffectiveHourlyRange(range) {
    if (!range) return null;
    var start = parseDate(range.start);
    var end = parseDate(range.end);
    var today = new Date();
    var todayDate = new Date(
      today.getFullYear(),
      today.getMonth(),
      today.getDate(),
    );
    if (start <= todayDate && end > todayDate) {
      end = todayDate;
    }
    return { start: fmtDate(start), end: fmtDate(end) };
  }

  function getHourlyHint() {
    var labels = {
      week: I18n.t("dailyAverageWeek"),
      month: I18n.t("dailyAverageMonth"),
      year: I18n.t("dailyAverageYear"),
      total: I18n.t("activeDayAverage"),
    };
    return labels[currentPeriod] || I18n.t("hourlyDefaultHint");
  }

  function round1(v) {
    if (!v || v <= 0) return 0;
    return Math.round(v * 10) / 10;
  }

  function mergeGameNames(target, names) {
    if (!target || !names || !names.length) return;
    names.forEach(function (name) {
      if (name && target.indexOf(name) < 0) target.push(name);
    });
  }

  function getPeriodDateRange() {
    var now = new Date(),
      today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    var start, end;

    switch (currentPeriod) {
      case "week": {
        var dow = (today.getDay() + 6) % 7;
        start = new Date(today);
        start.setDate(today.getDate() - dow + periodOffset * 7);
        end = new Date(start);
        end.setDate(start.getDate() + 6);
        break;
      }
      case "month":
        start = new Date(
          today.getFullYear(),
          today.getMonth() + periodOffset,
          1,
        );
        end = new Date(
          today.getFullYear(),
          today.getMonth() + periodOffset + 1,
          0,
        );
        break;
      case "year":
        start = new Date(today.getFullYear() + periodOffset, 0, 1);
        end = new Date(today.getFullYear() + periodOffset, 11, 31);
        break;
      default:
        return null;
    }
    return { start: fmtDate(start), end: fmtDate(end) };
  }

  function getPeriodCategoryData() {
    if (!fullData) return [];
    switch (currentPeriod) {
      case "week":
        return fullData.CategoryStatsWeek || fullData.CategoryStats || [];
      case "month":
        return fullData.CategoryStatsMonth || fullData.CategoryStats || [];
      case "year":
        return fullData.CategoryStatsYear || fullData.CategoryStats || [];
      default:
        return fullData.CategoryStats || [];
    }
  }

  function renderGameLists() {
    if (!fullData) return;
    UI.updateGameGrid("gameGrid", (fullData.PeriodGames || []).slice(0, 6));
    UI.updateGameGrid("topGameList", (fullData.TopGames || []).slice(0, 6));
    UI.updateGameGrid("recentGameList", fullData.RecentGames || []);
  }

  function filterDailyData() {
    if (!fullData || !fullData.DailyStats) return [];
    var all = fullData.DailyStats;
    var range = getPeriodDateRange();
    if (!range) return all;
    return all.filter(function (d) {
      return d.Date >= range.start && d.Date <= range.end;
    });
  }

  function parseDate(s) {
    var p = s.split("-");
    return new Date(Number(p[0]), Number(p[1]) - 1, Number(p[2]));
  }

  function getCalendarRange() {
    var today = new Date(
      new Date().getFullYear(),
      new Date().getMonth(),
      new Date().getDate(),
    );
    switch (currentPeriod) {
      case "week": {
        var dow = (today.getDay() + 6) % 7;
        var ws = new Date(today);
        ws.setDate(today.getDate() - dow + periodOffset * 7);
        var we = new Date(ws);
        we.setDate(ws.getDate() + 6);
        return [fmtDate(ws), fmtDate(we)];
      }
      case "month":
        return [
          fmtDate(
            new Date(today.getFullYear(), today.getMonth() + periodOffset, 1),
          ),
          fmtDate(
            new Date(
              today.getFullYear(),
              today.getMonth() + periodOffset + 1,
              0,
            ),
          ),
        ];
      case "year":
        return String(today.getFullYear() + periodOffset);
      default:
        var s = new Date(today);
        s.setFullYear(s.getFullYear() - 1);
        return [fmtDate(s), fmtDate(today)];
    }
  }

  function getPeriodLabel() {
    var today = new Date(
      new Date().getFullYear(),
      new Date().getMonth(),
      new Date().getDate(),
    );
    var monthShort = I18n.list("monthNamesShort");
    var monthLong = I18n.list("monthNamesLong", monthShort);
    switch (currentPeriod) {
      case "week": {
        var dow = (today.getDay() + 6) % 7;
        var ws = new Date(today);
        ws.setDate(today.getDate() - dow + periodOffset * 7);
        var we = new Date(ws);
        we.setDate(ws.getDate() + 6);
        if (I18n.isZh()) {
          return (
            monthShort[ws.getMonth()] +
            ws.getDate() +
            "日 - " +
            monthShort[we.getMonth()] +
            we.getDate() +
            "日"
          );
        }
        return (
          monthShort[ws.getMonth()] +
          " " +
          ws.getDate() +
          " - " +
          monthShort[we.getMonth()] +
          " " +
          we.getDate()
        );
      }
      case "month": {
        var m = new Date(
          today.getFullYear(),
          today.getMonth() + periodOffset,
          1,
        );
        if (I18n.isZh()) {
          return m.getFullYear() + "年" + monthShort[m.getMonth()];
        }
        return monthLong[m.getMonth()] + " " + m.getFullYear();
      }
      case "year":
        return String(today.getFullYear() + periodOffset) + (I18n.isZh() ? "年" : "");
      default:
        return I18n.t("allTime");
    }
  }

  function getPeriodSummaryLabels() {
    return {
      week: I18n.t("thisWeek"),
      month: I18n.t("thisMonth"),
      year: I18n.t("thisYear"),
      total: I18n.t("total"),
    };
  }

  function applyLocalization() {
    if (window.__GTS_I18N__) {
      I18n.set(window.__GTS_I18N__);
    } else {
      I18n.apply();
    }
  }

  function fmtDate(d) {
    return (
      d.getFullYear() +
      "-" +
      String(d.getMonth() + 1).padStart(2, "0") +
      "-" +
      String(d.getDate()).padStart(2, "0")
    );
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }

  return { loadData: loadData, reload: reload };
})();
