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

	private IWebView _webView;

	private bool _isOpen;

	private string _webAssetPath;

	public WebViewManager(IPlayniteAPI api, StatsCalculator statsCalculator)
	{
		_api = api;
		_statsCalculator = statsCalculator;
	}

	public void OpenOrFocus()
	{
		if (!_isOpen)
		{
			_webAssetPath = PrepareWebAssets();
			_webView = _api.WebViews.CreateView(1100, 750, Color.FromRgb(28, 28, 30));
			_webView.LoadingChanged += delegate
			{
				PushData();
			};
			string url = "file:///" + Path.Combine(_webAssetPath, "index.html").Replace("\\", "/").Replace(" ", "%20");
			_webView.Navigate(url);
			_webView.Open();
			_isOpen = true;
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
			string text = JsonConvert.SerializeObject(_statsCalculator.GetFullStats(), new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore
			}).Replace("\\", "\\\\").Replace("'", "\\'")
				.Replace("\r", "")
				.Replace("\n", "");
			string script = "if(window.GameStats)window.GameStats.loadData(JSON.parse('" + text + "'));";
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
		string text = Path.Combine(GetPluginDataPath(), "web-ui");
		string path = Path.Combine(text, ".version");
		string text2 = "1.1";
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
		return text;
	}

	private string GetPluginDataPath()
	{
		return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
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
