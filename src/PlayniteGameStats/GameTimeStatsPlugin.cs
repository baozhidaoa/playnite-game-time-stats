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
	private static readonly Guid PluginId = Guid.Parse("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");

	private SessionStore _sessionStore;

	private StatsCalculator _statsCalculator;

	private StatsViewControl _statsView;

	private EmbeddedPluginHost _embeddedHost;

	private PluginSettings _settings;

	private Timer _heartbeatTimer;

	public override Guid Id => PluginId;

	public GameTimeStatsPlugin(IPlayniteAPI api)
		: base(api)
	{
		base.Properties = new GenericPluginProperties
		{
			HasSettings = true
		};
		string pluginDataPath = GetPluginDataPath();
		_settings = LoadPluginSettings<PluginSettings>() ?? new PluginSettings();
		_sessionStore = new SessionStore(pluginDataPath);
		_statsCalculator = new StatsCalculator(PlayniteApi, _sessionStore, new SteamDataProvider(_settings), new SteamDeltaImporter(pluginDataPath, _sessionStore));
		_statsView = new StatsViewControl(PlayniteApi, _statsCalculator);
		_embeddedHost = new EmbeddedPluginHost(PlayniteApi, () => _statsView.CreateView());
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
		topPanelItem.Title = "游戏统计";
		topPanelItem.Icon = (File.Exists(text) ? text : null);
		topPanelItem.Activated = delegate
		{
			_embeddedHost.ToggleGameStats();
		};
		yield return topPanelItem;
	}

	public override void OnGameStarted(OnGameStartedEventArgs args)
	{
		if (args.Game != null)
		{
			_sessionStore.StartActiveSession(args.Game.Id, ExtractSteamAppId(args.Game.GameId));
		}
	}

	public override void OnGameStopped(OnGameStoppedEventArgs args)
	{
		if (args.Game != null)
		{
			_sessionStore.CompleteActiveSession(args.Game.Id, (long)args.ElapsedSeconds, DateTime.UtcNow, ExtractSteamAppId(args.Game.GameId));
			_statsView.RefreshData();
		}
	}

	public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
	{
		_sessionStore.Load();
		int num = _sessionStore.RecoverAbandonedActiveSessions();
		StartHeartbeatTimer();
		if (num > 0)
		{
			_statsView.RefreshData();
		}
	}

	public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
	{
		StopHeartbeatTimer();
		_sessionStore.UpdateActiveHeartbeats();
		SavePluginSettings(_settings);
		_sessionStore.FlushIfDirty();
		_embeddedHost.Clear();
		_statsView.Dispose();
	}

	private string GetPluginDataPath()
	{
		return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
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
