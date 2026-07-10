using System;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using Newtonsoft.Json;
using Playnite.SDK;

namespace PlayniteGameStats;

public class WebViewManager : IDisposable
{
	private readonly IPlayniteAPI _api;

	private readonly StatsCalculator _statsCalculator;

	private readonly string _pluginDataPath;

	private IWebView _webView;

	private bool _isOpen;

	private string _webAssetPath;

	public WebViewManager(IPlayniteAPI api, StatsCalculator statsCalculator, string pluginDataPath)
	{
		_api = api;
		_statsCalculator = statsCalculator;
		_pluginDataPath = pluginDataPath;
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
			_webAssetPath = PrepareWebAssets();
			_webView = _api.WebViews.CreateView(1100, 750, Color.FromRgb(28, 28, 30));
			if (_webView.WindowHost != null)
			{
				_webView.WindowHost.Title = "Game Time Statistics";
				_webView.WindowHost.Closed += delegate
				{
					_isOpen = false;
					_webView = null;
				};
			}
			_webView.LoadingChanged += delegate
			{
				PushData();
			};
			string url = "file:///" + Path.Combine(_webAssetPath, "index.html").Replace("\\", "/").Replace(" ", "%20");
			_webView.Navigate(url);
			_webView.Open();
			_isOpen = true;
			PushData();
		}
		catch (Exception ex)
		{
			_isOpen = false;
			_webView = null;
			_api.Notifications.Add("stats-open-error", "Failed to open game time statistics: " + ex.Message, NotificationType.Error);
		}
	}

	public void RefreshIfOpen()
	{
		if (_isOpen && _webView != null)
		{
			PushData();
		}
	}

	private void PushData()
	{
		try
		{
			if (_webView == null)
			{
				return;
			}
			string json = JsonConvert.SerializeObject(_statsCalculator.GetFullStats(), new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				Formatting = Formatting.None
			});
			string script = "window.__STATS_DATA__ = " + json + "; if(window.GameStats)window.GameStats.loadData(window.__STATS_DATA__);";
			_webView.EvaluateScriptAsync(script);
		}
		catch (Exception ex)
		{
			_api.Notifications.Add("stats-push-error", "Failed to push stats: " + ex.Message, NotificationType.Error);
		}
	}

	private string PrepareWebAssets()
	{
		string sourceDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "web");
		string text = Path.Combine(_pluginDataPath, "web-ui");
		string path = Path.Combine(text, ".version");
		string text2 = GetSourceWebVersion(sourceDir);
		bool flag = true;
		if (File.Exists(path))
		{
			try
			{
				flag = File.ReadAllText(path).Trim() != text2;
			}
			catch
			{
				flag = true;
			}
		}
		if (flag)
		{
			try
			{
				if (Directory.Exists(text))
				{
					Directory.Delete(text, recursive: true);
				}
			}
			catch
			{
			}
			CopyDirectory(sourceDir, text);
			File.WriteAllText(path, text2);
		}
		File.WriteAllText(Path.Combine(text, "data.js"), "window.__STATS_DATA__ = null;");
		return text;
	}

	private static string GetSourceWebVersion(string sourceDir)
	{
		try
		{
			string sourceVersion = Path.Combine(sourceDir, ".version");
			if (File.Exists(sourceVersion))
			{
				return File.ReadAllText(sourceVersion).Trim();
			}
		}
		catch
		{
		}
		return typeof(WebViewManager).Assembly.GetName().Version.ToString();
	}

	private static void CopyDirectory(string sourceDir, string destDir)
	{
		Directory.CreateDirectory(destDir);
		string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
		foreach (string text in files)
		{
			string path = text.Substring(sourceDir.Length).TrimStart('\\', '/');
			string text2 = Path.Combine(destDir, path);
			string directoryName = Path.GetDirectoryName(text2);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			File.Copy(text, text2, overwrite: true);
		}
	}

	public void Dispose()
	{
		_isOpen = false;
		if (_webView != null)
		{
			_webView.Dispose();
			_webView = null;
		}
	}
}
