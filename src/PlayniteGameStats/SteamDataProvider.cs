using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;

namespace PlayniteGameStats;

public class SteamDataProvider
{
	private sealed class SteamWebClient : WebClient
	{
		protected override WebRequest GetWebRequest(Uri address)
		{
			WebRequest request = base.GetWebRequest(address);
			request.Timeout = 10000;
			return request;
		}
	}

	private class SteamConfigDiscovery
	{
		public string ApiKey;

		public string SteamId64;
	}

	private readonly PluginSettings _settings;

	private readonly object _cacheLock = new object();

	private Dictionary<string, SteamAppStats> _cachedStats;

	private DateTime _cacheExpiresUtc = DateTime.MinValue;

	private string _cachedSettingsKey;

	private SteamConfigDiscovery _cachedConfig;

	private DateTime _configExpiresUtc = DateTime.MinValue;

	public SteamDataProvider(PluginSettings settings)
	{
		_settings = settings ?? new PluginSettings();
	}

	public Dictionary<string, SteamAppStats> Load()
	{
		string settingsKey = BuildSettingsKey();
		lock (_cacheLock)
		{
			if (_cachedStats != null && DateTime.UtcNow < _cacheExpiresUtc && string.Equals(settingsKey, _cachedSettingsKey, StringComparison.Ordinal))
			{
				return Clone(_cachedStats);
			}
		}
		Dictionary<string, SteamAppStats> dictionary = LoadLocal();
		if (_settings.EnableOnlineSteamSync)
		{
			string steamApiKey = GetSteamApiKey();
			string steamId = GetSteamId64();
			if (!string.IsNullOrEmpty(steamApiKey) && !string.IsNullOrEmpty(steamId))
			{
				Merge(dictionary, LoadOnline(steamApiKey, steamId));
			}
		}
		lock (_cacheLock)
		{
			_cachedStats = dictionary;
			_cachedSettingsKey = settingsKey;
			_cacheExpiresUtc = DateTime.UtcNow.AddSeconds(_settings.EnableOnlineSteamSync ? 300.0 : 30.0);
			return Clone(dictionary);
		}
	}

	public string GetSteamApiKey()
	{
		if (!string.IsNullOrEmpty(_settings.SteamApiKey))
		{
			return _settings.SteamApiKey;
		}
		return GetConfigDiscovery().ApiKey;
	}

	public string GetSteamId64()
	{
		if (!string.IsNullOrEmpty(_settings.SteamId64))
		{
			return _settings.SteamId64;
		}
		string steamId = GetConfigDiscovery().SteamId64;
		if (!string.IsNullOrEmpty(steamId))
		{
			return steamId;
		}
		string userDataPath = GetUserDataPath();
		if (string.IsNullOrEmpty(userDataPath) || !Directory.Exists(userDataPath))
		{
			return null;
		}
		string[] directories = Directory.GetDirectories(userDataPath);
		for (int i = 0; i < directories.Length; i++)
		{
			if (long.TryParse(Path.GetFileName(directories[i]), out var result))
			{
				return (76561197960265728L + result).ToString();
			}
		}
		return null;
	}

	private string BuildSettingsKey()
	{
		return (_settings.EnableOnlineSteamSync ? "1" : "0") + "|" + (_settings.SteamApiKey ?? "").Trim() + "|" + (_settings.SteamId64 ?? "").Trim();
	}

	private SteamConfigDiscovery GetConfigDiscovery()
	{
		lock (_cacheLock)
		{
			if (_cachedConfig != null && DateTime.UtcNow < _configExpiresUtc)
			{
				return _cachedConfig;
			}
		}
		SteamConfigDiscovery config = DiscoverPlayniteSteamConfig();
		lock (_cacheLock)
		{
			_cachedConfig = config;
			_configExpiresUtc = DateTime.UtcNow.AddMinutes(5.0);
			return config;
		}
	}

	public static string GetAutoConfigSummary()
	{
		SteamConfigDiscovery steamConfigDiscovery = DiscoverPlayniteSteamConfig();
		string userDataPath = GetUserDataPath();
		string text = null;
		if (!string.IsNullOrEmpty(userDataPath) && Directory.Exists(userDataPath))
		{
			string[] directories = Directory.GetDirectories(userDataPath);
			for (int i = 0; i < directories.Length; i++)
			{
				if (long.TryParse(Path.GetFileName(directories[i]), out var result))
				{
					text = (76561197960265728L + result).ToString();
					break;
				}
			}
		}
		string apiKeyStatus = string.IsNullOrEmpty(steamConfigDiscovery.ApiKey)
			? PluginLocalization.Get("AutoConfigApiKeyMissing", "not found")
			: PluginLocalization.Get("AutoConfigApiKeyFound", "found");
		string steamIdStatus;
		if (!string.IsNullOrEmpty(steamConfigDiscovery.SteamId64))
		{
			steamIdStatus = PluginLocalization.Get("AutoConfigSteamIdFromPlaynite", "from Playnite configuration");
		}
		else if (!string.IsNullOrEmpty(text))
		{
			steamIdStatus = PluginLocalization.Format("AutoConfigSteamIdFromLocal", "can be inferred from local Steam userdata as {0}", text);
		}
		else
		{
			steamIdStatus = PluginLocalization.Get("AutoConfigSteamIdMissing", "not found");
		}
		return PluginLocalization.Format("SettingsAutoSummary", "Auto-detection: Playnite plaintext Steam API Key {0}; SteamId64 {1}. The API Key from Playnite's Steam integration page is stored in an encrypted token and is not exposed through the public plugin SDK; enter an API Key here if online Steam sync is needed.", apiKeyStatus, steamIdStatus);
	}

	private Dictionary<string, SteamAppStats> LoadLocal()
	{
		Dictionary<string, SteamAppStats> result = new Dictionary<string, SteamAppStats>(StringComparer.OrdinalIgnoreCase);
		string userDataPath = GetUserDataPath();
		if (string.IsNullOrEmpty(userDataPath) || !Directory.Exists(userDataPath))
		{
			return result;
		}
		string[] files = Directory.GetFiles(userDataPath, "localconfig.vdf", SearchOption.AllDirectories);
		foreach (string file in files)
		{
			ParseLocalConfig(file, result);
		}
		return result;
	}

	private static Dictionary<string, SteamAppStats> Clone(Dictionary<string, SteamAppStats> source)
	{
		Dictionary<string, SteamAppStats> result = new Dictionary<string, SteamAppStats>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<string, SteamAppStats> item in source)
		{
			SteamAppStats value = item.Value;
			result[item.Key] = new SteamAppStats
			{
				AppId = value.AppId,
				PlaytimeMinutes = value.PlaytimeMinutes,
				Playtime2WeeksMinutes = value.Playtime2WeeksMinutes,
				PlaytimeDisconnectedMinutes = value.PlaytimeDisconnectedMinutes,
				LastPlayed = value.LastPlayed,
				Source = value.Source
			};
		}
		return result;
	}

	private void ParseLocalConfig(string file, Dictionary<string, SteamAppStats> result)
	{
		try
		{
			string[] array = File.ReadAllLines(file);
			bool flag = false;
			int num = 0;
			string text = null;
			string text2 = null;
			int num2 = 0;
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				string text3 = array2[i].Trim();
				if (!flag && GetFirstQuoted(text3) == "apps")
				{
					flag = true;
				}
				else
				{
					if (!flag)
					{
						continue;
					}
					if (text3 == "{")
					{
						num++;
						if (text2 != null && text == null)
						{
							text = text2;
							text2 = null;
							num2 = num;
							if (!result.ContainsKey(text))
							{
								result[text] = new SteamAppStats
								{
									AppId = text,
									Source = "Steam"
								};
							}
						}
					}
					else if (text3 == "}")
					{
						if (text != null && num == num2)
						{
							text = null;
						}
						num--;
						if (num <= 0)
						{
							flag = false;
						}
					}
					else if (text == null && num == 1)
					{
						string firstQuoted = GetFirstQuoted(text3);
						if (int.TryParse(firstQuoted, out var _))
						{
							text2 = firstQuoted;
						}
					}
					else
					{
						if (text == null)
						{
							continue;
						}
						List<string> quotedParts = GetQuotedParts(text3);
						if (quotedParts.Count >= 2)
						{
							SteamAppStats steamAppStats = result[text];
							if (quotedParts[0] == "Playtime")
							{
								steamAppStats.PlaytimeMinutes = ParseDouble(quotedParts[1]);
							}
							else if (quotedParts[0] == "Playtime2wks")
							{
								steamAppStats.Playtime2WeeksMinutes = ParseDouble(quotedParts[1]);
							}
							else if (quotedParts[0] == "PlaytimeDisconnected")
							{
								steamAppStats.PlaytimeDisconnectedMinutes = ParseDouble(quotedParts[1]);
							}
							else if (quotedParts[0] == "LastPlayed")
							{
								steamAppStats.LastPlayed = UnixToUtc(ParseLong(quotedParts[1]));
							}
						}
					}
				}
			}
		}
		catch
		{
		}
	}

	private Dictionary<string, SteamAppStats> LoadOnline(string apiKey, string steamId)
	{
		Dictionary<string, SteamAppStats> result = new Dictionary<string, SteamAppStats>(StringComparer.OrdinalIgnoreCase);
		try
		{
			using WebClient webClient = new SteamWebClient();
			string address = "https://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key=" + Uri.EscapeDataString(apiKey) + "&steamid=" + Uri.EscapeDataString(steamId) + "&format=json&include_appinfo=false";
			ParseOwnedGames(webClient.DownloadString(address), result);
			string address2 = "https://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v0001/?key=" + Uri.EscapeDataString(apiKey) + "&steamid=" + Uri.EscapeDataString(steamId) + "&format=json";
			ParseRecentGames(webClient.DownloadString(address2), result);
		}
		catch
		{
		}
		return result;
	}

	private void ParseOwnedGames(string json, Dictionary<string, SteamAppStats> result)
	{
		JToken jToken = JObject.Parse(json)["response"];
		JArray jArray = jToken?["games"] as JArray;
		if (jArray == null)
		{
			return;
		}
		foreach (JToken item in jArray)
		{
			string text = Convert.ToString(item["appid"]);
			if (!string.IsNullOrEmpty(text))
			{
				if (!result.TryGetValue(text, out var value))
				{
					SteamAppStats steamAppStats = new SteamAppStats();
					steamAppStats.AppId = text;
					steamAppStats.Source = "Steam";
					SteamAppStats steamAppStats3 = (result[text] = steamAppStats);
					value = steamAppStats3;
				}
				value.PlaytimeMinutes = Math.Max(value.PlaytimeMinutes, ParseDouble(Convert.ToString(item["playtime_forever"])));
				value.Playtime2WeeksMinutes = Math.Max(value.Playtime2WeeksMinutes, ParseDouble(Convert.ToString(item["playtime_2weeks"])));
				value.LastPlayed = MaxDate(value.LastPlayed, UnixToUtc(ParseLong(Convert.ToString(item["rtime_last_played"]))));
			}
		}
	}

	private void ParseRecentGames(string json, Dictionary<string, SteamAppStats> result)
	{
		JToken jToken = JObject.Parse(json)["response"];
		JArray jArray = jToken?["games"] as JArray;
		if (jArray == null)
		{
			return;
		}
		foreach (JToken item in jArray)
		{
			string text = Convert.ToString(item["appid"]);
			if (!string.IsNullOrEmpty(text))
			{
				if (!result.TryGetValue(text, out var value))
				{
					SteamAppStats steamAppStats = new SteamAppStats();
					steamAppStats.AppId = text;
					steamAppStats.Source = "Steam";
					SteamAppStats steamAppStats3 = (result[text] = steamAppStats);
					value = steamAppStats3;
				}
				value.Playtime2WeeksMinutes = Math.Max(value.Playtime2WeeksMinutes, ParseDouble(Convert.ToString(item["playtime_2weeks"])));
				value.PlaytimeMinutes = Math.Max(value.PlaytimeMinutes, ParseDouble(Convert.ToString(item["playtime_forever"])));
			}
		}
	}

	private static void Merge(Dictionary<string, SteamAppStats> target, Dictionary<string, SteamAppStats> source)
	{
		foreach (KeyValuePair<string, SteamAppStats> item in source)
		{
			if (!target.TryGetValue(item.Key, out var value))
			{
				target[item.Key] = item.Value;
				continue;
			}
			value.PlaytimeMinutes = Math.Max(value.PlaytimeMinutes, item.Value.PlaytimeMinutes);
			value.Playtime2WeeksMinutes = Math.Max(value.Playtime2WeeksMinutes, item.Value.Playtime2WeeksMinutes);
			value.PlaytimeDisconnectedMinutes = Math.Max(value.PlaytimeDisconnectedMinutes, item.Value.PlaytimeDisconnectedMinutes);
			value.LastPlayed = MaxDate(value.LastPlayed, item.Value.LastPlayed);
		}
	}

	private static string GetUserDataPath()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
		string[] array = new string[2]
		{
			Path.Combine(folderPath, "Steam", "userdata"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "userdata")
		};
		foreach (string text in array)
		{
			if (Directory.Exists(text))
			{
				return text;
			}
		}
		return null;
	}

	private static SteamConfigDiscovery DiscoverPlayniteSteamConfig()
	{
		SteamConfigDiscovery steamConfigDiscovery = new SteamConfigDiscovery();
		try
		{
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string[] array = new string[2]
			{
				Path.Combine(folderPath, "Playnite", "ExtensionsData"),
				Path.Combine(folderPath, "Playnite")
			};
			foreach (string path in array)
			{
				if (!Directory.Exists(path))
				{
					continue;
				}
				foreach (string item in Directory.GetFiles(path, "*.json", SearchOption.AllDirectories).Where(delegate(string f)
				{
					string text = Path.GetFileName(f).ToLowerInvariant();
					string text2 = f.ToLowerInvariant();
					return (text.Contains("config") || text.Contains("settings")) && !text2.Contains("\\cache\\") && !text2.Contains("\\catalogcache\\") && !text2.EndsWith("\\tokens.json");
				}).Take(250)
					.ToList())
				{
					TryReadSteamConfigFile(item, steamConfigDiscovery);
					if (!string.IsNullOrEmpty(steamConfigDiscovery.ApiKey) && !string.IsNullOrEmpty(steamConfigDiscovery.SteamId64))
					{
						return steamConfigDiscovery;
					}
				}
			}
		}
		catch
		{
		}
		return steamConfigDiscovery;
	}

	private static void TryReadSteamConfigFile(string file, SteamConfigDiscovery result)
	{
		try
		{
			string text = File.ReadAllText(file);
			if (!string.IsNullOrWhiteSpace(text) && text.Length <= 1048576)
			{
				ScanTokenForSteamConfig(JToken.Parse(text), result);
			}
		}
		catch
		{
		}
	}

	private static void ScanTokenForSteamConfig(JToken token, SteamConfigDiscovery result)
	{
		if (token == null)
		{
			return;
		}
		JObject jObject = (JObject)((token is JObject) ? token : null);
		if (jObject != null)
		{
			foreach (JProperty item in jObject.Properties())
			{
				string obj = item.Name ?? "";
				string text = ((item.Value != null && item.Value.Type != JTokenType.Object && item.Value.Type != JTokenType.Array) ? Convert.ToString(item.Value) : null);
				string text2 = obj.ToLowerInvariant();
				if (string.IsNullOrEmpty(result.ApiKey) && text2.Contains("steam") && text2.Contains("api") && text2.Contains("key") && IsLikelySteamApiKey(text))
				{
					result.ApiKey = text.Trim();
				}
				if (string.IsNullOrEmpty(result.SteamId64) && text2.Contains("steam") && (text2.Contains("id") || text2.Contains("account") || text2.Contains("user")))
				{
					result.SteamId64 = NormalizeSteamId(text);
				}
				ScanTokenForSteamConfig(item.Value, result);
			}
			return;
		}
		JArray jArray = (JArray)((token is JArray) ? token : null);
		if (jArray == null)
		{
			return;
		}
		foreach (JToken item2 in jArray)
		{
			ScanTokenForSteamConfig(item2, result);
		}
	}

	private static bool IsLikelySteamApiKey(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return false;
		}
		value = value.Trim();
		if (value.Length >= 20 && value.Length <= 64)
		{
			return value.All((char c) => char.IsLetterOrDigit(c));
		}
		return false;
	}

	private static string NormalizeSteamId(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return null;
		}
		string text = new string(value.Where(char.IsDigit).ToArray());
		if (text.Length == 17 && text.StartsWith("7656119"))
		{
			return text;
		}
		if (text.Length >= 8 && text.Length <= 12 && long.TryParse(text, out var result))
		{
			return (76561197960265728L + result).ToString();
		}
		return null;
	}

	private static List<string> GetQuotedParts(string line)
	{
		List<string> list = new List<string>();
		int num = -1;
		for (int i = 0; i < line.Length; i++)
		{
			if (line[i] == '"')
			{
				if (num < 0)
				{
					num = i + 1;
					continue;
				}
				list.Add(line.Substring(num, i - num));
				num = -1;
			}
		}
		return list;
	}

	private static string GetFirstQuoted(string line)
	{
		List<string> quotedParts = GetQuotedParts(line);
		if (quotedParts.Count <= 0)
		{
			return null;
		}
		return quotedParts[0];
	}

	private static double ParseDouble(string value)
	{
		if (!double.TryParse(value, out var result))
		{
			return 0.0;
		}
		return result;
	}

	private static long ParseLong(string value)
	{
		if (!long.TryParse(value, out var result))
		{
			return 0L;
		}
		return result;
	}

	private static DateTime UnixToUtc(long seconds)
	{
		if (seconds <= 0)
		{
			return DateTime.MinValue;
		}
		return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
	}

	private static DateTime MaxDate(DateTime a, DateTime b)
	{
		if (!(a > b))
		{
			return b;
		}
		return a;
	}
}
