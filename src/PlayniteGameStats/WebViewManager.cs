using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Events;

namespace PlayniteGameStats;

public class WebViewManager : IDisposable
{
	private readonly IPlayniteAPI _api;

	private readonly StatsCalculator _statsCalculator;

	private IWebView _webView;

	private bool _isOpen;

	private readonly object _refreshLock = new object();

	private int _refreshVersion;

	private bool _refreshRunning;

	private bool _disposed;

	public WebViewManager(IPlayniteAPI api, StatsCalculator statsCalculator)
	{
		_api = api;
		_statsCalculator = statsCalculator;
	}

	public void OpenOrFocus()
	{
		if (_isOpen && _webView != null)
		{
			RefreshIfOpen();
			try
			{
				_webView.WindowHost?.Activate();
			}
			catch
			{
			}
			return;
		}
		try
		{
			string webAssetPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "web");
			_webView = _api.WebViews.CreateView(1100, 750, Color.FromRgb(15, 15, 19));
			if (_webView.WindowHost != null)
			{
				_webView.WindowHost.Title = PluginLocalization.Get("WindowTitle", "Game Time Statistics");
				_webView.WindowHost.Background = new SolidColorBrush(Color.FromRgb(15, 15, 19));
				_webView.WindowHost.Closed += delegate
				{
					lock (_refreshLock)
					{
						_isOpen = false;
						_webView = null;
						_refreshVersion++;
					}
				};
			}
			_webView.LoadingChanged += delegate(object sender, WebViewLoadingChangedEventArgs args)
			{
				if (!args.IsLoading)
				{
					QueueDataRefresh();
				}
			};
			string url = new Uri(Path.Combine(webAssetPath, "index.html")).AbsoluteUri;
			_webView.Navigate(url);
			_webView.Open();
			_isOpen = true;
			QueueDataRefresh();
		}
		catch (Exception ex)
		{
			_isOpen = false;
			_webView = null;
			_api.Notifications.Add("stats-open-error", PluginLocalization.Format("OpenError", "Failed to open game time statistics: {0}", ex.Message), NotificationType.Error);
		}
	}

	public void RefreshIfOpen()
	{
		if (_isOpen && _webView != null)
		{
			QueueDataRefresh();
		}
	}

	public void InvalidateAndRefresh()
	{
		_statsCalculator.Invalidate();
		RefreshIfOpen();
	}

	private void QueueDataRefresh()
	{
		lock (_refreshLock)
		{
			if (_disposed || _webView == null)
			{
				return;
			}
			_refreshVersion++;
			if (_refreshRunning)
			{
				return;
			}
			_refreshRunning = true;
		}
		Task.Factory.StartNew(ProcessDataRefresh, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
	}

	private void ProcessDataRefresh()
	{
		while (true)
		{
			int version;
			lock (_refreshLock)
			{
				if (_disposed || _webView == null)
				{
					_refreshRunning = false;
					return;
				}
				version = _refreshVersion;
			}

			string script;
			try
			{
				script = BuildDataScript();
			}
			catch (Exception ex)
			{
				ReportError("stats-push-error", "Failed to prepare statistics: {0}", ex);
				script = null;
			}

			if (!string.IsNullOrEmpty(script))
			{
				DispatchDataScript(script);
			}

			lock (_refreshLock)
			{
				if (_disposed || _webView == null || version == _refreshVersion)
				{
					_refreshRunning = false;
					return;
				}
			}
		}
	}

	private string BuildDataScript()
	{
		string json = JsonConvert.SerializeObject(_statsCalculator.GetFullStats(), new JsonSerializerSettings
		{
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Formatting.None
		});
		string l10n = JsonConvert.SerializeObject(PluginLocalization.GetWebStrings(), new JsonSerializerSettings
		{
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Formatting.None
		});
		return "window.__GTS_I18N__ = " + l10n + "; if(window.I18n)window.I18n.set(window.__GTS_I18N__); window.__STATS_DATA__ = " + json + "; if(window.GameStats)window.GameStats.loadData(window.__STATS_DATA__);";
	}

	private void DispatchDataScript(string script)
	{
		try
		{
			if (_webView?.WindowHost?.Dispatcher == null)
			{
				return;
			}
			_webView.WindowHost.Dispatcher.BeginInvoke(new Action(delegate
			{
				try
				{
					if (!_disposed && _webView != null)
					{
						_webView.EvaluateScriptAsync(script);
					}
				}
				catch (Exception ex)
				{
					ReportError("stats-push-error", "Failed to push stats: {0}", ex);
				}
			}));
		}
		catch
		{
		}
	}

	private void ReportError(string id, string fallback, Exception ex)
	{
		Action action = delegate
		{
			try
			{
				_api.Notifications.Add(id, PluginLocalization.Format("PushError", fallback, ex.Message), NotificationType.Error);
			}
			catch
			{
			}
		};
		try
		{
			if (_webView?.WindowHost?.Dispatcher != null && !_webView.WindowHost.Dispatcher.CheckAccess())
			{
				_webView.WindowHost.Dispatcher.BeginInvoke(action);
				return;
			}
		}
		catch
		{
		}
		action();
	}

	public void Dispose()
	{
		IWebView webView;
		lock (_refreshLock)
		{
			_disposed = true;
			_isOpen = false;
			_refreshVersion++;
			webView = _webView;
			_webView = null;
		}
		if (webView != null)
		{
			webView.Dispose();
		}
	}
}
