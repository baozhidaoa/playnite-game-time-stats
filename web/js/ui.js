var UI = (function () {
  function updateSummaryCards(summary) {
    document.getElementById("cardHours").textContent = formatHours(
      summary.TotalPlayTimeHours,
    );
    document.getElementById("cardGames").textContent = I18n.format(
      "gamesCount",
      "{0} games",
      summary.TotalGamesPlayed,
    );
    document.getElementById("cardStreak").textContent = I18n.format(
      "daysCount",
      "{0} days",
      summary.CurrentStreakDays,
    );
    document.getElementById("cardAvg").textContent = formatMinutes(
      summary.AverageDailyMinutes,
    );
  }

  function updatePeriodLabel(text) {
    document.getElementById("periodLabel").textContent = text;
  }

  function updateHourlyHint(text) {
    var hint = document.getElementById("hourlyFilterHint");
    if (hint) hint.textContent = text || I18n.t("hourlyDefaultHint");
  }

  function updateGameGrid(containerId, games) {
    var container = document.getElementById(containerId);
    if (!container) return;
    if (!games || !games.length) {
      container.innerHTML =
        '<div class="empty-hint">' +
        escapeHtml(I18n.t("emptyGameHint")) +
        "</div>";
      return;
    }
    container.innerHTML = games
      .map(function (g) {
        return renderGameCard(g);
      })
      .join("");
  }

  function renderGameCard(g) {
    var coverHtml = renderCover(g, 120, 144);
    var badge = renderBadge(g);
    return (
      '<div class="game-card">' +
      '<div class="game-cover">' +
      coverHtml +
      "</div>" +
      '<div class="game-info">' +
      '<div class="game-name" title="' +
      escapeHtml(g.GameName) +
      '">' +
      escapeHtml(g.GameName) +
      badge +
      "</div>" +
      '<div class="game-meta">' +
      formatCardMeta(g) +
      "</div>" +
      "</div>" +
      "</div>"
    );
  }

  function renderCover(g, w, h) {
    var src = toFileUrl(g.CoverImage) || toFileUrl(g.IconImage);
    if (src) {
      return (
        '<img src="' +
        src +
        '" width="' +
        w +
        '" height="' +
        h +
        "\" style=\"object-fit:cover\" onerror=\"this.style.display='none';var fb=this.parentNode.querySelector('.fallback');if(fb)fb.style.display='block';\">" +
        '<span class="fallback" style="display:none">&#127918;</span>'
      );
    }
    return '<span class="fallback">&#127918;</span>';
  }

  function renderBadge(g) {
    if (!g) return "";
    var raw = g.DataSource || (g.IsEstimated ? I18n.t("estimatedData") : "");
    if (!raw) return "";
    var title = g.EstimateReason || raw;
    return (
      '<span class="game-badge" title="' +
      escapeHtml(title) +
      '">' +
      escapeHtml(raw) +
      "</span>"
    );
  }

  function formatCardMeta(g) {
    var playText = formatMinutesAsHours(g && g.MinutesPlayed);
    var countText = I18n.format(
      "playsCountShort",
      "{0} plays",
      g && g.SessionCount ? g.SessionCount : 0,
    );
    if (g && g.UserScore)
      return I18n.format(
        "scoreMeta",
        "Score: {0} / {1} / {2}",
        g.UserScore,
        playText,
        countText,
      );
    return playText + " / " + countText;
  }

  function toFileUrl(raw) {
    if (!raw) return null;
    if (raw.indexOf("http") === 0) return raw;
    return "file:///" + raw.replace(/\\/g, "/").replace(/ /g, "%20");
  }

  function updateLegacyBanner(data) {
    var banner = document.getElementById("legacyBanner");
    if (!banner) return;
    var daily = data && data.DailyStats ? data.DailyStats : [];
    var hasEstimated = daily.some(function (d) {
      return d && d.EstimatedMinutes > 0;
    });
    var hasSteamDelta = daily.some(function (d) {
      return d && d.SteamDeltaMinutes > 0;
    });
    var hasRecovered = daily.some(function (d) {
      return d && d.RecoveredMinutes > 0;
    });
    if (hasEstimated || hasSteamDelta || hasRecovered) {
      var parts = [];
      if (hasSteamDelta) parts.push(I18n.t("legacySteamDelta"));
      if (hasRecovered) parts.push(I18n.t("legacyRecovered"));
      if (hasEstimated) parts.push(I18n.t("legacyEstimated"));
      banner.textContent = I18n.format(
        "legacyBanner",
        "Includes {0} data. Hover charts to view source breakdown.",
        parts.join(I18n.t("legacyJoin", ", ")),
      );
      banner.style.display = "block";
    } else {
      banner.style.display = "none";
    }
  }

  function updateChartLegend(chartType) {
    var legend = document.getElementById("chartLegend");
    if (legend)
      legend.style.display = chartType === "heatmap" ? "flex" : "none";
  }

  function setActiveTab(selector, activeClass, targetValue) {
    var tabs = document.querySelectorAll(selector);
    tabs.forEach(function (tab) {
      var val =
        tab.getAttribute("data-period") ||
        tab.getAttribute("data-chart");
      tab.classList.toggle(activeClass, val === targetValue);
    });
  }

  function formatHours(h) {
    if (h === undefined || h === null) return "0" + I18n.t("unitHourShort");
    if (h < 1) return Math.round(h * 60) + I18n.t("unitMinuteShort");
    return h.toFixed(1) + I18n.t("unitHourShort");
  }

  function formatMinutes(m) {
    if (!m || m <= 0) return "0" + I18n.t("unitMinuteShort");
    if (m < 60) return Math.round(m) + I18n.t("unitMinuteShort");
    var h = Math.floor(m / 60);
    var r = Math.round(m % 60);
    return r === 0
      ? h + I18n.t("unitHourShort")
      : h + I18n.t("unitHourShort") + r + I18n.t("unitMinuteShort");
  }

  function formatMinutesAsHours(m) {
    if (!m || m <= 0) return "0.0" + I18n.t("unitHourShort");
    return (m / 60).toFixed(1) + I18n.t("unitHourShort");
  }

  function escapeHtml(str) {
    var div = document.createElement("div");
    div.appendChild(document.createTextNode(str || ""));
    return div.innerHTML;
  }

  return {
    updateSummaryCards: updateSummaryCards,
    updatePeriodLabel: updatePeriodLabel,
    updateHourlyHint: updateHourlyHint,
    updateGameGrid: updateGameGrid,
    updateLegacyBanner: updateLegacyBanner,
    updateChartLegend: updateChartLegend,
    setActiveTab: setActiveTab,
    formatHours: formatHours,
    formatMinutes: formatMinutes,
    formatMinutesAsHours: formatMinutesAsHours,
    escapeHtml: escapeHtml,
  };
})();
