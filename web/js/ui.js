var UI = (function () {
  function updateSummaryCards(summary) {
    document.getElementById("cardHours").textContent = formatHours(
      summary.TotalPlayTimeHours,
    );
    document.getElementById("cardGames").textContent =
      summary.TotalGamesPlayed + " 款";
    document.getElementById("cardStreak").textContent =
      summary.CurrentStreakDays + " 天";
    document.getElementById("cardAvg").textContent = formatMinutes(
      summary.AverageDailyMinutes,
    );
  }

  function updatePeriodLabel(text) {
    document.getElementById("periodLabel").textContent = text;
  }

  function updateHourlyHint(text) {
    var hint = document.getElementById("hourlyFilterHint");
    if (hint) hint.textContent = text || "每小时日均分钟";
  }

  function updateGameGrid(containerId, games) {
    var container = document.getElementById(containerId);
    if (!container) return;
    if (!games || !games.length) {
      container.innerHTML =
        '<div class="empty-hint">暂无数据，快去玩游戏吧</div>';
      return;
    }
    container.innerHTML = games
      .map(function (g) {
        return renderGameCard(g);
      })
      .join("");
  }

  function updateGameList(containerId, games) {
    var container = document.getElementById(containerId);
    if (!container) return;
    if (!games || !games.length) {
      container.innerHTML = '<div class="empty-hint">--</div>';
      return;
    }
    container.innerHTML = games
      .map(function (g) {
        return renderGameItem(g);
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

  function renderGameItem(g) {
    var coverHtml = renderCover(g, 56, 70);
    var badge = renderBadge(g);
    return (
      '<div class="game-item">' +
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
      formatSessionText(g) +
      "</div>" +
      "</div>" +
      '<div class="game-time">' +
      formatMinutesAsHours(g.MinutesPlayed) +
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
    var raw = g.DataSource || (g.IsEstimated ? "估算" : "");
    if (!raw) return "";
    // Map "会话" to "Playnite" for consistency
    var text = raw === "会话" || raw === "会话+估算" ? "Playnite" : raw;
    var title = g.EstimateReason || raw;
    return (
      '<span class="game-badge" title="' +
      escapeHtml(title) +
      '">' +
      escapeHtml(text) +
      "</span>"
    );
  }

  function formatSessionText(g) {
    if (!g || !g.SessionCount) {
      return g && g.DataSource ? g.DataSource : "估算数据";
    }
    return g.SessionCount + "次游玩";
  }

  function formatCardMeta(g) {
    var playText = formatMinutesAsHours(g && g.MinutesPlayed);
    var countText = (g && g.SessionCount ? g.SessionCount : 0) + "次";
    if (g && g.UserScore)
      return "评分：" + g.UserScore + " / " + playText + " / " + countText;
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
      if (hasSteamDelta) parts.push("Steam 差量");
      if (hasRecovered) parts.push("恢复会话");
      if (hasEstimated) parts.push("历史估算");
      banner.textContent =
        "包含" + parts.join("、") + "数据，悬停图表可查看来源构成";
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
        tab.getAttribute("data-chart") ||
        tab.getAttribute("data-gt");
      tab.classList.toggle(activeClass, val === targetValue);
    });
  }

  function formatHours(h) {
    if (h === undefined || h === null) return "0h";
    if (h < 1) return Math.round(h * 60) + "m";
    return h.toFixed(1) + "h";
  }

  function formatMinutes(m) {
    if (!m || m <= 0) return "0m";
    if (m < 60) return Math.round(m) + "m";
    var h = Math.floor(m / 60);
    var r = Math.round(m % 60);
    return r === 0 ? h + "h" : h + "h" + r + "m";
  }

  function formatMinutesAsHours(m) {
    if (!m || m <= 0) return "0.0h";
    return (m / 60).toFixed(1) + "h";
  }

  function escapeHtml(str) {
    var div = document.createElement("div");
    div.appendChild(document.createTextNode(str));
    return div.innerHTML;
  }

  return {
    updateSummaryCards: updateSummaryCards,
    updatePeriodLabel: updatePeriodLabel,
    updateHourlyHint: updateHourlyHint,
    updateGameGrid: updateGameGrid,
    updateGameList: updateGameList,
    updateLegacyBanner: updateLegacyBanner,
    updateChartLegend: updateChartLegend,
    setActiveTab: setActiveTab,
    formatHours: formatHours,
    formatMinutes: formatMinutes,
    formatMinutesAsHours: formatMinutesAsHours,
    escapeHtml: escapeHtml,
  };
})();
