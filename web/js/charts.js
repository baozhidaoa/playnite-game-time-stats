var Charts = (function () {
  var GREEN = '#40c463';
  var GREEN_DARK = '#216e39';
  var GREEN_MID = '#30a14e';
  var GREEN_LIGHT = '#9be9a8';
  var BG_DARK = 'rgba(22,22,28,0.95)';
  var BORDER = 'rgba(255,255,255,0.08)';

  function tooltip() {
    return {
      backgroundColor: BG_DARK,
      borderColor: BORDER,
      borderWidth: 1,
      padding: [8, 12],
      textStyle: { color: '#e0e0e0', fontSize: 12, fontFamily: 'inherit' },
      extraCssText: 'border-radius:8px;box-shadow:0 8px 32px rgba(0,0,0,0.4);'
    };
  }

  function formatM(v) {
    if (!v || v <= 0) return '0m';
    if (v < 60) return Math.round(v) + 'm';
    var h = Math.floor(v / 60);
    var m = Math.round(v % 60);
    return m === 0 ? h + 'h' : h + 'h' + m + 'm';
  }

  function escapeHtml(value) {
    return String(value || '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function formatGameNames(names) {
    if (!names || !names.length) return '';
    return names
      .filter(function (name) { return name; })
      .map(escapeHtml)
      .join(' / ');
  }

  function gameNamesLine(names) {
    var text = formatGameNames(names);
    return text ? '<br/><span style="color:#999">' + text + '</span>' : '';
  }

  // ── Heatmap ──
  var WIDE_CELL_SIZE = 24;
  var WIDE_CELL_GAP = 1;

  function getHeatmapLayoutSize(range, period) {
    var isWide = period === 'year' || period === 'total';
    var columns = countCalendarColumns(range);
    var cell = isWide ? WIDE_CELL_SIZE : 28;
    return {
      width: Math.max(1, columns) * cell + 70,
      height: 7 * cell + (isWide ? 70 : 62)
    };
  }

  function createHeatmapOption(dailyData, range, period) {
    var data = fillHeatmapRange(dailyData, range);
    var isWide = period === 'year' || period === 'total';
    var compactCellSize = 28;
    var wideColumns = isWide ? countCalendarColumns(range) : 0;
    var cellSize = isWide ? [WIDE_CELL_SIZE, WIDE_CELL_SIZE] : [compactCellSize, compactCellSize];
    var squareGap = isWide ? WIDE_CELL_GAP : 3;
    var radius = isWide ? 3 : 5;
    var compactColumns = isWide ? wideColumns : countCalendarColumns(range);
    var layout = {
      top: isWide ? 38 : 34,
      left: 42,
      right: null,
      bottom: null,
      width: Math.max(1, compactColumns) * (isWide ? WIDE_CELL_SIZE : compactCellSize),
      height: 7 * (isWide ? WIDE_CELL_SIZE : compactCellSize)
    };
    function heatColor(v) {
      if (!v || v <= 0) return '#1e1e24';
      if (v < 30) return GREEN_LIGHT;
      if (v < 60) return GREEN;
      if (v < 120) return GREEN_MID;
      return GREEN_DARK;
    }
    return {
      tooltip: {
        trigger: 'item',
        triggerOn: 'mousemove|click',
        enterable: false,
        confine: true,
        hideDelay: 120,
        transitionDuration: 0,
        position: function (pos) {
          return [pos[0] + 12, pos[1] + 12];
        },
        formatter: function (p) {
          var games = p.value && p.value[2] ? gameNamesLine(p.value[2]) : '';
          return p.value && p.value[0]
            ? '<b>' + p.value[0] + '</b><br/>' + formatM(p.value[1]) + games
            : '';
        },
        backgroundColor: BG_DARK, borderColor: BORDER,
        textStyle: { color: '#e0e0e0', fontSize: 12 },
        extraCssText: 'pointer-events:none;border-radius:8px;box-shadow:0 8px 32px rgba(0,0,0,0.4);'
      },
      visualMap: {
        min: 0, max: 120, type: 'piecewise',
        dimension: 1,
        orient: 'horizontal', left: 'center', bottom: 60,
        pieces: [
          { min: 0, max: 0, color: '#1e1e24', label: '0' },
          { min: 0.01, max: 30, color: '#9be9a8', label: '<30m' },
          { min: 30, max: 60, color: '#40c463', label: '30-60m' },
          { min: 60, max: 120, color: '#30a14e', label: '1-2h' },
          { min: 120, color: '#216e39', label: '2h+' }
        ],
        textStyle: { color: '#777', fontSize: 10 }, show: false
      },
      calendar: {
        top: layout.top, left: layout.left, right: layout.right, bottom: layout.bottom,
        width: layout.width, height: layout.height,
        range: range, cellSize: cellSize,
        yearLabel: { show: false },
        dayLabel: {
          firstDay: 1,
          nameMap: ['一', '二', '三', '四', '五', '六', '日'],
          color: '#777', fontSize: 10, margin: 8
        },
        monthLabel: {
          nameMap: ['1月','2月','3月','4月','5月','6月','7月','8月','9月','10月','11月','12月'],
          color: '#888', fontSize: 11, margin: 10
        },
        splitLine: {
          lineStyle: { color: 'rgba(255,255,255,0.04)', width: 1 }
        },
        itemStyle: {
          color: 'transparent',
          borderWidth: 0
        }
      },
      series: [{
        type: 'heatmap',
        coordinateSystem: 'calendar',
        data: data,
        encode: { value: 1, tooltip: [0, 1] },
        itemStyle: {
          borderColor: '#0f0f13',
          borderWidth: Math.max(1, squareGap),
          borderRadius: radius
        },
        emphasis: {
          itemStyle: {
            shadowBlur: 10,
            shadowColor: 'rgba(0,0,0,0.55)',
            borderColor: 'rgba(255,255,255,0.9)',
            borderWidth: 1
          }
        }
      }]
    };
  }

  function fillHeatmapRange(dailyData, range) {
    var byDate = {};
    dailyData.forEach(function (d) {
      byDate[d.Date] = {
        value: d.TotalMinutes,
        gameNames: d.GameNames || []
      };
    });

    var start, end;
    if (typeof range === 'string') {
      start = parseDate(range + '-01-01');
      end = parseDate(range + '-12-31');
    } else {
      start = parseDate(range[0]);
      end = parseDate(range[1]);
    }

    var data = [];
    for (var d = start; d <= end; d.setDate(d.getDate() + 1)) {
      var key = formatDate(d);
      var item = byDate[key] || {};
      data.push([key, item.value || 0, item.gameNames || []]);
    }
    return data;
  }

  function countCalendarColumns(range) {
    if (!range || typeof range === 'string') return 53;
    var start = parseDate(range[0]);
    var end = parseDate(range[1]);
    var startDow = (start.getDay() + 6) % 7;
    var days = Math.floor((end - start) / 86400000) + 1;
    return Math.ceil((startDow + days) / 7);
  }

  function parseDate(s) {
    var p = s.split('-');
    return new Date(Number(p[0]), Number(p[1]) - 1, Number(p[2]));
  }

  function formatDate(d) {
    return d.getFullYear() + '-' +
      String(d.getMonth() + 1).padStart(2, '0') + '-' +
      String(d.getDate()).padStart(2, '0');
  }

  // ── Bar ──
  function createBarOption(dailyData, showYear) {
    if (!dailyData.length) return emptyChart();
    var rawDates = dailyData.map(function (d) { return d.Date; });
    var gameNames = dailyData.map(function (d) { return d.GameNames || []; });
    var dates = dailyData.map(function (d) {
      var parts = d.Date.split('-');
      return showYear ? parts[0].slice(2) + '/' + parts[1] : parts[1] + '/' + parts[2];
    });
    var values = dailyData.map(function (d) { return d.TotalMinutes; });

    return {
      grid: { left: 55, right: 24, top: 20, bottom: 55 },
      tooltip: extend(tooltip(), {
        trigger: 'axis',
        formatter: function (params) {
          var idx = params[0].dataIndex;
          return '<b>' + rawDates[idx] + '</b><br/>' + formatM(params[0].value) + gameNamesLine(gameNames[idx]);
        }
      }),
      xAxis: {
        type: 'category', data: dates,
        axisLabel: { color: '#777', fontSize: 10, rotate: dailyData.length > 14 ? 45 : 0, interval: dailyData.length > 31 ? Math.max(1, Math.floor(dailyData.length / 16)) : 0 },
        axisLine: { lineStyle: { color: 'rgba(255,255,255,0.06)' } },
        axisTick: { show: false }, splitLine: { show: false }
      },
      yAxis: {
        type: 'value', name: '',
        axisLabel: { color: '#888', fontSize: 10, formatter: function (v) { return (v / 60).toFixed(1) + 'h'; } },
        splitLine: { lineStyle: { color: 'rgba(255,255,255,0.04)', type: 'dashed' } },
        axisLine: { show: false }
      },
      series: [{
        type: 'bar',
        data: values.map(function (v) {
          var ratio = v / Math.max.apply(null, values.concat([1]));
          var color;
          if (v <= 0) color = '#1e1e24';
          else if (ratio < 0.3) color = '#9be9a8';
          else if (ratio < 0.6) color = GREEN;
          else if (ratio < 0.85) color = GREEN_MID;
          else color = GREEN_DARK;
          return {
            value: v,
            itemStyle: {
              color: {
                type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
                colorStops: [
                  { offset: 0, color: color },
                  { offset: 1, color: adjustAlpha(color, 0.6) }
                ]
              },
              borderRadius: [10, 10, 0, 0]
            }
          };
        }),
        barWidth: dailyData.length > 60 ? '90%' : '60%',
        emphasis: {
          itemStyle: { shadowBlur: 12, shadowColor: 'rgba(64,196,99,0.3)' }
        }
      }]
    };
  }

  // ── Line ──
  function createLineOption(dailyData, showYear) {
    if (!dailyData.length) return emptyChart();
    var rawDates = dailyData.map(function (d) { return d.Date; });
    var gameNames = dailyData.map(function (d) { return d.GameNames || []; });
    var dates = dailyData.map(function (d) {
      var p = d.Date.split('-');
      return showYear ? p[0].slice(2) + '/' + p[1] : p[1] + '/' + p[2];
    });
    var values = dailyData.map(function (d) { return d.TotalMinutes; });
    var isLong = dailyData.length > 60;

    return {
      grid: { left: 55, right: 24, top: 24, bottom: 50 },
      tooltip: extend(tooltip(), {
        trigger: 'axis',
        formatter: function (params) {
          var idx = params[0].dataIndex;
          return '<b>' + rawDates[idx] + '</b><br/>' + formatM(params[0].value) + gameNamesLine(gameNames[idx]);
        }
      }),
      xAxis: {
        type: 'category', data: dates, boundaryGap: false,
        axisLabel: { color: '#777', fontSize: 10, rotate: dailyData.length > 14 ? 45 : 0, interval: isLong ? Math.max(1, Math.floor(dailyData.length / 16)) : 0 },
        axisLine: { lineStyle: { color: 'rgba(255,255,255,0.06)' } },
        axisTick: { show: false }, splitLine: { show: false }
      },
      yAxis: {
        type: 'value', name: '',
        axisLabel: { color: '#888', fontSize: 10, formatter: function (v) { return (v / 60).toFixed(1) + 'h'; } },
        splitLine: { lineStyle: { color: 'rgba(255,255,255,0.04)', type: 'dashed' } },
        axisLine: { show: false }
      },
      series: [{
        type: 'line', data: values, smooth: 0.35,
        symbol: isLong ? 'none' : 'circle', symbolSize: isLong ? 0 : 6,
        showSymbol: !isLong,
        lineStyle: {
          width: 3,
          shadowBlur: 10, shadowColor: 'rgba(64,196,99,0.3)',
          color: { type: 'linear', x: 0, y: 0, x2: 1, y2: 0,
            colorStops: [{ offset: 0, color: '#30a14e' }, { offset: 1, color: '#9be9a8' }] }
        },
        areaStyle: {
          color: { type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [
              { offset: 0, color: 'rgba(64,196,99,0.2)' },
              { offset: 0.5, color: 'rgba(64,196,99,0.05)' },
              { offset: 1, color: 'rgba(64,196,99,0)' }
            ] }
        },
        itemStyle: {
          color: GREEN,
          borderColor: '#0f0f13', borderWidth: 2
        }
      }]
    };
  }

  // ── Hourly Distribution ──
  function createHourlyOption(hourlyStats) {
    if (!hourlyStats || !hourlyStats.length) return emptyChart();
    var hours = ['0时','1','2','3','4','5','6','7','8','9','10','11','12','13','14','15','16','17','18','19','20','21','22','23'];
    var values = hourlyStats.map(function (h) { return h.TotalMinutes; });
    var gameNames = hourlyStats.map(function (h) { return h.GameNames || []; });

    return {
      grid: { left: 46, right: 18, top: 18, bottom: 32 },
      tooltip: extend(tooltip(), {
        trigger: 'axis',
        axisPointer: { type: 'line', lineStyle: { color: 'rgba(155,233,168,0.35)', width: 1 } },
        formatter: function (p) {
          var idx = p[0].dataIndex;
          return '<b>' + hours[idx] + '</b><br/>日均 ' + formatM(p[0].value) + gameNamesLine(gameNames[idx]);
        }
      }),
      xAxis: {
        type: 'category',
        data: hours,
        boundaryGap: false,
        axisLabel: { color: '#777', fontSize: 9, interval: 2 },
        axisLine: { lineStyle: { color: 'rgba(255,255,255,0.06)' } },
        axisTick: { show: false }, splitLine: { show: false }
      },
      yAxis: {
        type: 'value',
        min: 0,
        axisLabel: { color: '#888', fontSize: 9, formatter: function (v) { return formatM(v); } },
        splitLine: { lineStyle: { color: 'rgba(255,255,255,0.04)', type: 'dashed' } },
        axisLine: { show: false }
      },
      series: [{
        name: '日均分钟',
        type: 'line',
        data: values,
        smooth: 0.4,
        symbol: 'circle',
        symbolSize: 5,
        showSymbol: false,
        lineStyle: {
          width: 3,
          color: { type: 'linear', x: 0, y: 0, x2: 1, y2: 0,
            colorStops: [{ offset: 0, color: '#30a14e' }, { offset: 1, color: '#9be9a8' }] },
          shadowBlur: 10,
          shadowColor: 'rgba(64,196,99,0.25)'
        },
        areaStyle: {
          color: { type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [
              { offset: 0, color: 'rgba(64,196,99,0.28)' },
              { offset: 0.55, color: 'rgba(64,196,99,0.08)' },
              { offset: 1, color: 'rgba(64,196,99,0)' }
            ] }
        },
        itemStyle: {
          color: GREEN,
          borderColor: '#0f0f13',
          borderWidth: 2
        },
        emphasis: {
          focus: 'series',
          itemStyle: { shadowBlur: 8, shadowColor: 'rgba(64,196,99,0.3)' }
        }
      }]
    };
  }

  // ── Genre Radar ──
  function createGenreRadarOption(genreStats, title) {
    if (!genreStats || !genreStats.length) return emptyChart();
    var names = genreStats.map(function (g) { return g.GenreName; });
    var values = genreStats.map(function (g) { return Math.round(g.TotalMinutes / 60 * 10) / 10; });
    var maxV = Math.max.apply(null, values.concat([1]));

    return {
      tooltip: extend(tooltip(), {
        formatter: function (p) {
          return '<b>' + p.name + '</b><br/>' + p.value.toFixed(1) + ' 小时';
        }
      }),
      radar: {
        indicator: names.map(function (n) {
          return { name: n, max: maxV * 1.2 };
        }),
        center: ['50%', '50%'],
        radius: '66%',
        startAngle: 90,
        axisNameGap: 14,
        axisName: { color: '#aaa', fontSize: 11, borderRadius: 3, padding: [2, 4], overflow: 'truncate', width: 92 },
        splitArea: {
          areaStyle: { color: ['rgba(255,255,255,0.015)', 'rgba(255,255,255,0.005)'] }
        },
        splitLine: { lineStyle: { color: 'rgba(255,255,255,0.06)' } },
        axisLine: { lineStyle: { color: 'rgba(255,255,255,0.1)' } }
      },
      series: [{
        type: 'radar',
        data: [{
          value: values,
          name: title || '时长(小时)',
          areaStyle: { color: 'rgba(64,196,99,0.12)' },
          lineStyle: { color: GREEN, width: 2, shadowBlur: 8, shadowColor: 'rgba(64,196,99,0.3)' },
          itemStyle: { color: GREEN, borderColor: 'rgba(0,0,0,0.5)', borderWidth: 2 },
          symbol: 'circle', symbolSize: 5
        }],
        animationDuration: 800,
        animationEasing: 'cubicOut'
      }]
    };
  }

  function emptyChart() {
    return {
      title: { text: '暂无数据', left: 'center', top: 'center', textStyle: { color: '#555', fontSize: 13, fontWeight: 'normal' } }
    };
  }

  function adjustAlpha(hexOrRgb, alpha) {
    if (!hexOrRgb || typeof hexOrRgb !== 'string') return hexOrRgb;
    if (hexOrRgb.indexOf('rgba') === 0) {
      return hexOrRgb.replace(/[\d.]+\)$/, alpha + ')');
    }
    if (hexOrRgb.indexOf('rgb') === 0) {
      return hexOrRgb.replace(')', ', ' + alpha + ')').replace('rgb', 'rgba');
    }
    return hexOrRgb;
  }

  function extend(a, b) { for (var k in b) a[k] = b[k]; return a; }

  return {
    createHeatmapOption: createHeatmapOption,
    getHeatmapLayoutSize: getHeatmapLayoutSize,
    createBarOption: createBarOption,
    createLineOption: createLineOption,
    createHourlyOption: createHourlyOption,
    createGenreRadarOption: createGenreRadarOption
  };
})();
