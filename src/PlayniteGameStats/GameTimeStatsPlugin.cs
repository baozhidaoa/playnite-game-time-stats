using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Timers;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;

namespace PlayniteGameStats;

public class GameTimeStatsPlugin : GenericPlugin
{
	private static readonly Guid PluginId = Guid.Parse("dc73bf2f-ffd7-40e0-acd4-08e2296a239e");

	private SessionStore _sessionStore;

	private StatsCalculator _statsCalculator;

	private PlaytimeAttributionStore _attributionStore;

	private WebViewManager _webViewManager;

	private PluginSettings _settings;

	private Timer _heartbeatTimer;

	public override Guid Id => PluginId;

	public GameTimeStatsPlugin(IPlayniteAPI api)
		: base(api)
	{
		PluginLocalization.Initialize(PlayniteApi);
		base.Properties = new GenericPluginProperties
		{
			HasSettings = true
		};
		string pluginDataPath = GetPluginUserDataPath();
		Directory.CreateDirectory(pluginDataPath);
		MigrateLegacyDataFiles(pluginDataPath);
		_settings = LoadPluginSettings<PluginSettings>() ?? new PluginSettings();
		_sessionStore = new SessionStore(pluginDataPath);
		_attributionStore = new PlaytimeAttributionStore(pluginDataPath);
		_statsCalculator = new StatsCalculator(PlayniteApi, _sessionStore, _attributionStore, new SteamDataProvider(_settings));
		_webViewManager = new WebViewManager(PlayniteApi, _statsCalculator);
	}

	public override ISettings GetSettings(bool firstRunSettings)
	{
		return _settings;
	}

	public override UserControl GetSettingsView(bool firstRunSettings)
	{
		return new SettingsView
		{
			DataContext = _settings
		};
	}

	public override IEnumerable<TopPanelItem> GetTopPanelItems()
	{
		string text = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "icon.png");
		TopPanelItem topPanelItem = new TopPanelItem();
		topPanelItem.Title = PluginLocalization.Get("TopPanelTitle", "Game stats");
		topPanelItem.Icon = (File.Exists(text) ? text : null);
		topPanelItem.Activated = delegate
		{
			_webViewManager.OpenOrFocus();
		};
		yield return topPanelItem;
	}

	public override void OnGameStarted(OnGameStartedEventArgs args)
	{
		if (args.Game != null)
		{
			_sessionStore.StartActiveSession(args.Game.Id, ExtractSteamAppId(args.Game.GameId));
			_statsCalculator.Invalidate();
		}
	}

	public override void OnGameStopped(OnGameStoppedEventArgs args)
	{
		if (args.Game != null)
		{
			_sessionStore.CompleteActiveSession(args.Game.Id, (long)args.ElapsedSeconds, DateTime.UtcNow, ExtractSteamAppId(args.Game.GameId));
			_webViewManager.InvalidateAndRefresh();
		}
	}

	public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
	{
		_sessionStore.Load();
		_attributionStore.Load();
		if (_sessionStore.ConsumeAttributionResetRequest())
		{
			_attributionStore.Reset();
			DeleteLegacySteamSnapshot();
		}
		int num = _sessionStore.RecoverAbandonedActiveSessions();
		_statsCalculator.Invalidate();
		StartHeartbeatTimer();
		if (num > 0)
		{
			_webViewManager.RefreshIfOpen();
		}
	}

	public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
	{
		StopHeartbeatTimer();
		_sessionStore.UpdateActiveHeartbeats();
		SavePluginSettings(_settings);
		_sessionStore.FlushIfDirty();
		_webViewManager.Dispose();
	}

	private void StartHeartbeatTimer()
	{
		StopHeartbeatTimer();
		_heartbeatTimer = new Timer(30000.0);
		_heartbeatTimer.Elapsed += delegate
		{
			_sessionStore.UpdateActiveHeartbeats();
		};
		_heartbeatTimer.AutoReset = true;
		_heartbeatTimer.Start();
	}

	private void StopHeartbeatTimer()
	{
		if (_heartbeatTimer == null)
		{
			return;
		}
		try
		{
			_heartbeatTimer.Stop();
			_heartbeatTimer.Dispose();
		}
		catch
		{
		}
		_heartbeatTimer = null;
	}

	private static void MigrateLegacyDataFiles(string pluginDataPath)
	{
		string legacyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		if (string.IsNullOrEmpty(legacyPath) || string.Equals(legacyPath, pluginDataPath, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		foreach (string fileName in new[] { "sessions.json", "steam-snapshots.json" })
		{
			string source = Path.Combine(legacyPath, fileName);
			string destination = Path.Combine(pluginDataPath, fileName);
			try
			{
				if (File.Exists(source) && !File.Exists(destination))
				{
					Directory.CreateDirectory(pluginDataPath);
					File.Copy(source, destination);
				}
			}
			catch
			{
			}
		}
	}

	private void DeleteLegacySteamSnapshot()
	{
		try
		{
			string snapshot = Path.Combine(GetPluginUserDataPath(), "steam-snapshots.json");
			if (File.Exists(snapshot))
			{
				File.Delete(snapshot);
			}
		}
		catch
		{
		}
	}

	private static string ExtractSteamAppId(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return null;
		}
		string text = "";
		string text2 = "";
		foreach (char c in value)
		{
			if (char.IsDigit(c))
			{
				text2 += c;
				if (text2.Length > text.Length)
				{
					text = text2;
				}
			}
			else
			{
				text2 = "";
			}
		}
		return text.Length > 0 ? text : null;
	}
}
