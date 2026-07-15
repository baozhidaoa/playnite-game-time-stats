using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace PlayniteGameStats;

public class PlaytimeAttributionInput
{
	public string GameId;

	public double TotalMinutes;

	public DateTime ReleaseDateLocal;

	public DateTime SteamLastPlayedLocal;

	public DateTime PlayniteLastActivityLocal;

	public Dictionary<string, double> ExactMinutesByDate = new Dictionary<string, double>(StringComparer.Ordinal);
}

public class AttributedGameDay
{
	public string GameId;

	public string Date;

	public double Minutes;
}

public class PlaytimeAttributionStore
{
	private const double DailyCapacityMinutes = 1440.0;

	private readonly string _filePath;

	private readonly object _lock = new object();

	private PlaytimeAttributionFile _data = new PlaytimeAttributionFile();

	private bool _loaded;

	private bool _dirty;

	public PlaytimeAttributionStore(string pluginDataPath)
	{
		_filePath = Path.Combine(pluginDataPath, "attributions.json");
	}

	public void Load()
	{
		lock (_lock)
		{
			if (_loaded)
			{
				return;
			}
			try
			{
				if (File.Exists(_filePath))
				{
					_data = JsonConvert.DeserializeObject<PlaytimeAttributionFile>(File.ReadAllText(_filePath)) ?? new PlaytimeAttributionFile();
				}
			}
			catch
			{
				_data = new PlaytimeAttributionFile();
			}
			Normalize();
			_loaded = true;
		}
	}

	public void Reset()
	{
		lock (_lock)
		{
			_data = new PlaytimeAttributionFile();
			_loaded = true;
			_dirty = true;
			SaveCore();
		}
	}

	public List<AttributedGameDay> Synchronize(List<PlaytimeAttributionInput> inputs, Dictionary<string, double> exactMinutesByDate)
	{
		lock (_lock)
		{
			if (!_loaded)
			{
				Load();
			}
			inputs = inputs ?? new List<PlaytimeAttributionInput>();
			HashSet<string> activeGames = new HashSet<string>(inputs.Where((PlaytimeAttributionInput item) => !string.IsNullOrEmpty(item.GameId)).Select((PlaytimeAttributionInput item) => item.GameId), StringComparer.OrdinalIgnoreCase);
			Dictionary<string, double> usedByDate = new Dictionary<string, double>(exactMinutesByDate ?? new Dictionary<string, double>(StringComparer.Ordinal), StringComparer.Ordinal);
			foreach (KeyValuePair<string, GameAttributionState> pair in _data.Games)
			{
				if (!activeGames.Contains(pair.Key))
				{
					continue;
				}
				foreach (AttributedDay day in pair.Value.Days)
				{
					AddMinutes(usedByDate, day.Date, day.Minutes);
				}
			}

			foreach (PlaytimeAttributionInput input in inputs.OrderBy((PlaytimeAttributionInput item) => item.GameId, StringComparer.OrdinalIgnoreCase))
			{
				if (string.IsNullOrEmpty(input.GameId))
				{
					continue;
				}
				if (!_data.Games.TryGetValue(input.GameId, out GameAttributionState state))
				{
					state = new GameAttributionState { Seed = input.GameId };
					_data.Games[input.GameId] = state;
					_dirty = true;
				}
				NormalizeState(state);
				if (string.Equals(state.Seed, "legacy", StringComparison.Ordinal))
				{
					state.Seed = input.GameId;
					_dirty = true;
				}
				double exactMinutes = (input.ExactMinutesByDate ?? new Dictionary<string, double>()).Values.Sum();
				double targetMinutes = Math.Max(0.0, input.TotalMinutes - exactMinutes);
				double currentMinutes = state.Days.Sum((AttributedDay day) => day.Minutes);
				if (currentMinutes > targetMinutes + 0.0001)
				{
					Trim(state, currentMinutes - targetMinutes, input.ExactMinutesByDate, usedByDate);
					currentMinutes = state.Days.Sum((AttributedDay day) => day.Minutes);
					_dirty = true;
				}
				if (targetMinutes > currentMinutes + 0.0001)
				{
					DateTime syncDate = DateTime.Today;
					List<DateTime> anchors = GetAnchors(state, input, syncDate);
					Allocate(state, targetMinutes - currentMinutes, anchors, input.ReleaseDateLocal, syncDate, usedByDate, !state.Initialized);
					_dirty = true;
				}
				if (!state.Initialized)
				{
					state.Initialized = true;
					_dirty = true;
				}
			}
			if (_dirty)
			{
				SaveCore();
			}
			return GetDaysFor(activeGames);
		}
	}

	private List<AttributedGameDay> GetDaysFor(HashSet<string> activeGames)
	{
		List<AttributedGameDay> result = new List<AttributedGameDay>();
		foreach (KeyValuePair<string, GameAttributionState> pair in _data.Games)
		{
			if (!activeGames.Contains(pair.Key))
			{
				continue;
			}
			foreach (IGrouping<string, AttributedDay> group in pair.Value.Days.Where((AttributedDay day) => day.Minutes > 0.0001).GroupBy((AttributedDay day) => day.Date, StringComparer.Ordinal))
			{
				result.Add(new AttributedGameDay
				{
					GameId = pair.Key,
					Date = group.Key,
					Minutes = group.Sum((AttributedDay day) => day.Minutes)
				});
			}
		}
		return result;
	}

	private List<DateTime> GetAnchors(GameAttributionState state, PlaytimeAttributionInput input, DateTime syncDate)
	{
		List<DateTime> anchors = new List<DateTime>();
		AddAnchor(anchors, input.SteamLastPlayedLocal);
		AddAnchor(anchors, input.PlayniteLastActivityLocal);
		if (anchors.Count == 0)
		{
			if (state.FallbackAnchorLocal <= DateTime.MinValue)
			{
				state.FallbackAnchorLocal = syncDate;
			}
			AddAnchor(anchors, state.FallbackAnchorLocal);
		}
		return anchors;
	}

	private static void AddAnchor(List<DateTime> anchors, DateTime value)
	{
		if (value <= DateTime.MinValue)
		{
			return;
		}
		DateTime date = value.Date;
		if (!anchors.Contains(date))
		{
			anchors.Add(date);
		}
	}

	private void Allocate(GameAttributionState state, double minutes, List<DateTime> anchors, DateTime releaseDate, DateTime syncDate, Dictionary<string, double> usedByDate, bool initial)
	{
		if (minutes <= 0.0001 || anchors == null || anchors.Count == 0)
		{
			return;
		}
		for (int i = 0; i < anchors.Count; i++)
		{
			double share = i == anchors.Count - 1 ? minutes : Math.Floor(minutes / anchors.Count * 100.0) / 100.0;
			minutes -= share;
			AllocateForAnchor(state, share, anchors[i], releaseDate, syncDate, usedByDate, initial);
		}
	}

	private void AllocateForAnchor(GameAttributionState state, double minutes, DateTime anchor, DateTime releaseDate, DateTime syncDate, Dictionary<string, double> usedByDate, bool initial)
	{
		if (minutes <= 0.0001)
		{
			return;
		}
		DateTime lowerBound = releaseDate > DateTime.MinValue ? releaseDate.Date : DateTime.MinValue.Date;
		DateTime date = anchor.Date;
		bool first = true;
		int offset = 0;
		while (minutes > 0.0001)
		{
			if (date < lowerBound)
			{
				DateTime forwardStart = anchor.Date.AddDays(1);
				if (forwardStart <= syncDate)
				{
					date = forwardStart;
				}
				else
				{
					date = (syncDate > anchor.Date ? syncDate : anchor.Date).AddDays(1);
				}
				lowerBound = DateTime.MinValue.Date;
				offset = 0;
			}
			string key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			double used = usedByDate.TryGetValue(key, out double existing) ? existing : 0.0;
			double available = Math.Max(0.0, DailyCapacityMinutes - used);
			if (available > 0.0001)
			{
				double requested = first && !initial ? minutes : GetChunkMinutes(state.Seed, key, offset, minutes);
				double assigned = Math.Min(minutes, Math.Min(available, requested));
				AddAllocation(state, key, assigned);
				AddMinutes(usedByDate, key, assigned);
				minutes -= assigned;
			}
			first = false;
			offset++;
			if (date > anchor.Date && lowerBound == DateTime.MinValue.Date)
			{
				date = date.AddDays(1);
			}
			else
			{
				date = date.AddDays(-1);
			}
		}
	}

	private static double GetChunkMinutes(string seed, string date, int offset, double remaining)
	{
		uint hash = 2166136261;
		foreach (char c in (seed ?? "") + "|" + date)
		{
			hash = (hash ^ c) * 16777619;
		}
		hash = (hash ^ (uint)offset) * 16777619;
		return Math.Min(remaining, 45.0 + hash % 316);
	}

	private void AddAllocation(GameAttributionState state, string date, double minutes)
	{
		if (minutes <= 0.0001)
		{
			return;
		}
		state.Days.Add(new AttributedDay
		{
			Date = date,
			Minutes = minutes,
			Sequence = _data.NextSequence++
		});
	}

	private static void Trim(GameAttributionState state, double minutes, Dictionary<string, double> exactMinutesByDate, Dictionary<string, double> usedByDate)
	{
		IEnumerable<AttributedDay> preferred = state.Days.Where((AttributedDay day) => exactMinutesByDate != null && exactMinutesByDate.ContainsKey(day.Date)).OrderByDescending((AttributedDay day) => day.Sequence);
		IEnumerable<AttributedDay> remainder = state.Days.Where((AttributedDay day) => exactMinutesByDate == null || !exactMinutesByDate.ContainsKey(day.Date)).OrderByDescending((AttributedDay day) => day.Sequence);
		foreach (AttributedDay day in preferred.Concat(remainder).ToList())
		{
			if (minutes <= 0.0001)
			{
				break;
			}
			double removed = Math.Min(minutes, day.Minutes);
			day.Minutes -= removed;
			minutes -= removed;
			AddMinutes(usedByDate, day.Date, -removed);
		}
		state.Days.RemoveAll((AttributedDay day) => day.Minutes <= 0.0001);
	}

	private static void AddMinutes(Dictionary<string, double> values, string key, double minutes)
	{
		if (string.IsNullOrEmpty(key) || Math.Abs(minutes) <= 0.0001)
		{
			return;
		}
		values[key] = (values.TryGetValue(key, out double current) ? current : 0.0) + minutes;
	}

	private void Normalize()
	{
		if (_data == null)
		{
			_data = new PlaytimeAttributionFile();
		}
		if (_data.Games == null)
		{
			_data.Games = new Dictionary<string, GameAttributionState>(StringComparer.OrdinalIgnoreCase);
		}
		foreach (GameAttributionState state in _data.Games.Values)
		{
			NormalizeState(state);
		}
		if (_data.NextSequence <= 0)
		{
			_data.NextSequence = 1;
		}
	}

	private static void NormalizeState(GameAttributionState state)
	{
		if (string.IsNullOrEmpty(state.Seed))
		{
			state.Seed = "legacy";
		}
		if (state.Days == null)
		{
			state.Days = new List<AttributedDay>();
		}
		state.Days = state.Days.Where((AttributedDay day) => day != null && !string.IsNullOrEmpty(day.Date) && day.Minutes > 0.0001).ToList();
	}

	private void SaveCore()
	{
		try
		{
			string directory = Path.GetDirectoryName(_filePath);
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			File.WriteAllText(_filePath, JsonConvert.SerializeObject(_data, Formatting.Indented));
			_dirty = false;
		}
		catch
		{
		}
	}
}
