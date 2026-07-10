using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace PlayniteGameStats;

public class SessionStore
{
	private const int CurrentSchemaVersion = 2;

	private readonly string _filePath;

	private readonly object _lock = new object();

	private SessionFile _data = new SessionFile();

	private DateTime _lastSave = DateTime.MinValue;

	private bool _dirty;

	public SessionStore(string pluginDataPath)
	{
		_filePath = Path.Combine(pluginDataPath, "sessions.json");
	}

	public void Load()
	{
		lock (_lock)
		{
			try
			{
				if (File.Exists(_filePath))
				{
					string value = File.ReadAllText(_filePath);
					_data = JsonConvert.DeserializeObject<SessionFile>(value) ?? new SessionFile();
				}
				else
				{
					_data = new SessionFile();
				}
				Migrate();
			}
			catch
			{
				_data = new SessionFile();
			}
		}
	}

	public void Save()
	{
		lock (_lock)
		{
			try
			{
				string directoryName = Path.GetDirectoryName(_filePath);
				if (!Directory.Exists(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				_data.SchemaVersion = CurrentSchemaVersion;
				string contents = JsonConvert.SerializeObject(_data, Formatting.Indented);
				File.WriteAllText(_filePath, contents);
				_dirty = false;
				_lastSave = DateTime.UtcNow;
			}
			catch
			{
			}
		}
	}

	public void StartActiveSession(Guid gameId, string externalAppId)
	{
		lock (_lock)
		{
			string id = gameId.ToString();
			DateTime utcNow = DateTime.UtcNow;
			ActiveSessionRecord activeSessionRecord = _data.ActiveSessions.FirstOrDefault((ActiveSessionRecord s) => s.GameId == id);
			if (activeSessionRecord == null)
			{
				_data.ActiveSessions.Add(new ActiveSessionRecord
				{
					GameId = id,
					StartUtc = utcNow,
					LastHeartbeatUtc = utcNow,
					ExternalAppId = externalAppId
				});
			}
			else
			{
				activeSessionRecord.LastHeartbeatUtc = utcNow;
				if (string.IsNullOrEmpty(activeSessionRecord.ExternalAppId))
				{
					activeSessionRecord.ExternalAppId = externalAppId;
				}
			}
			_dirty = true;
			Save();
		}
	}

	public void UpdateActiveHeartbeats()
	{
		lock (_lock)
		{
			if (_data.ActiveSessions.Count == 0)
			{
				return;
			}
			DateTime utcNow = DateTime.UtcNow;
			foreach (ActiveSessionRecord activeSession in _data.ActiveSessions)
			{
				activeSession.LastHeartbeatUtc = utcNow;
			}
			_dirty = true;
			if ((utcNow - _lastSave).TotalSeconds >= 25.0)
			{
				Save();
			}
		}
	}

	public void CompleteActiveSession(Guid gameId, long elapsedSeconds, DateTime endUtc, string externalAppId)
	{
		lock (_lock)
		{
			string id = gameId.ToString();
			ActiveSessionRecord activeSessionRecord = _data.ActiveSessions.FirstOrDefault((ActiveSessionRecord s) => s.GameId == id);
			DateTime normalizedEnd = ToUtc(endUtc);
			DateTime startUtc = normalizedEnd.AddSeconds(-Math.Max(elapsedSeconds, 0L));
			if (activeSessionRecord != null)
			{
				DateTime activeStart = ToUtc(activeSessionRecord.StartUtc);
				if (activeStart > DateTime.MinValue && activeStart < normalizedEnd)
				{
					startUtc = activeStart;
				}
				_data.ActiveSessions.Remove(activeSessionRecord);
			}
			long durationSeconds = Math.Max(0L, (long)(normalizedEnd - startUtc).TotalSeconds);
			if (elapsedSeconds >= 60 && Math.Abs(durationSeconds - elapsedSeconds) > 120)
			{
				durationSeconds = elapsedSeconds;
				startUtc = normalizedEnd.AddSeconds(-durationSeconds);
			}
			AddCompletedSessionCore(id, startUtc, normalizedEnd, durationSeconds, SessionSources.Playnite, SessionConfidence.Exact, externalAppId);
			Save();
		}
	}

	public int RecoverAbandonedActiveSessions()
	{
		lock (_lock)
		{
			int count = 0;
			if (_data.ActiveSessions.Count == 0)
			{
				return count;
			}
			List<ActiveSessionRecord> activeSessions = _data.ActiveSessions.ToList();
			foreach (ActiveSessionRecord activeSession in activeSessions)
			{
				DateTime startUtc = ToUtc(activeSession.StartUtc);
				DateTime endUtc = ToUtc(activeSession.LastHeartbeatUtc);
				if (endUtc <= startUtc)
				{
					endUtc = DateTime.UtcNow;
				}
				long durationSeconds = Math.Max(0L, (long)(endUtc - startUtc).TotalSeconds);
				if (durationSeconds >= 60)
				{
					AddCompletedSessionCore(activeSession.GameId, startUtc, endUtc, durationSeconds, SessionSources.Recovered, SessionConfidence.Recovered, activeSession.ExternalAppId);
					count++;
				}
			}
			_data.ActiveSessions.Clear();
			_dirty = true;
			Save();
			return count;
		}
	}

	public void AddImportedSession(SessionRecord record)
	{
		if (record == null || string.IsNullOrEmpty(record.GameId))
		{
			return;
		}
		lock (_lock)
		{
			NormalizeRecord(record);
			if (record.DurationSeconds < 60)
			{
				return;
			}
			_data.Sessions.Add(record);
			_dirty = true;
			if ((DateTime.UtcNow - _lastSave).TotalSeconds >= 30.0)
			{
				Save();
			}
		}
	}

	public void Record(Guid gameId, long elapsedSeconds, DateTime timestamp)
	{
		lock (_lock)
		{
			DateTime endUtc = ToUtc(timestamp);
			DateTime startUtc = endUtc.AddSeconds(-Math.Max(elapsedSeconds, 0L));
			AddCompletedSessionCore(gameId.ToString(), startUtc, endUtc, elapsedSeconds, SessionSources.Playnite, SessionConfidence.Exact, null);
			if ((DateTime.UtcNow - _lastSave).TotalSeconds >= 30.0)
			{
				Save();
			}
		}
	}

	public List<SessionRecord> GetSessionsForGame(Guid gameId)
	{
		string id = gameId.ToString();
		lock (_lock)
		{
			return _data.Sessions.Where((SessionRecord s) => s.GameId == id).ToList();
		}
	}

	public List<SessionRecord> GetAllSessions()
	{
		lock (_lock)
		{
			return _data.Sessions.ToList();
		}
	}

	public bool HasSessionsForGame(Guid gameId)
	{
		string id = gameId.ToString();
		lock (_lock)
		{
			return _data.Sessions.Any((SessionRecord s) => s.GameId == id);
		}
	}

	public bool HasAnySessions()
	{
		lock (_lock)
		{
			return _data.Sessions.Count > 0;
		}
	}

	public void FlushIfDirty()
	{
		lock (_lock)
		{
			if (_dirty)
			{
				Save();
			}
		}
	}

	private void Migrate()
	{
		if (_data.Sessions == null)
		{
			_data.Sessions = new List<SessionRecord>();
		}
		if (_data.ActiveSessions == null)
		{
			_data.ActiveSessions = new List<ActiveSessionRecord>();
		}
		foreach (SessionRecord session in _data.Sessions)
		{
			NormalizeRecord(session);
		}
		_data.Sessions = _data.Sessions.Where((SessionRecord s) => s.DurationSeconds >= 60 && !string.IsNullOrEmpty(s.GameId)).ToList();
		if (_data.SchemaVersion != CurrentSchemaVersion)
		{
			_data.SchemaVersion = CurrentSchemaVersion;
			_dirty = true;
			Save();
		}
	}

	private void AddCompletedSessionCore(string gameId, DateTime startUtc, DateTime endUtc, long durationSeconds, string source, string confidence, string externalAppId)
	{
		if (durationSeconds < 60 || string.IsNullOrEmpty(gameId))
		{
			return;
		}
		SessionRecord record = new SessionRecord
		{
			GameId = gameId,
			StartUtc = ToUtc(startUtc),
			EndUtc = ToUtc(endUtc),
			DurationSeconds = durationSeconds,
			ElapsedSeconds = durationSeconds,
			Timestamp = ToUtc(endUtc),
			Source = source,
			Confidence = confidence,
			ExternalAppId = externalAppId
		};
		NormalizeRecord(record);
		_data.Sessions.Add(record);
		_dirty = true;
	}

	private static void NormalizeRecord(SessionRecord session)
	{
		if (session == null)
		{
			return;
		}
		session.EndUtc = ToUtc(session.EndUtc);
		session.StartUtc = ToUtc(session.StartUtc);
		session.Timestamp = ToUtc(session.Timestamp);
		if (session.DurationSeconds <= 0 && session.ElapsedSeconds > 0)
		{
			session.DurationSeconds = session.ElapsedSeconds;
		}
		if (session.EndUtc <= DateTime.MinValue && session.Timestamp > DateTime.MinValue)
		{
			session.EndUtc = session.Timestamp;
		}
		if (session.EndUtc <= DateTime.MinValue && session.StartUtc > DateTime.MinValue && session.DurationSeconds > 0)
		{
			session.EndUtc = session.StartUtc.AddSeconds(session.DurationSeconds);
		}
		if (session.StartUtc <= DateTime.MinValue && session.EndUtc > DateTime.MinValue && session.DurationSeconds > 0)
		{
			session.StartUtc = session.EndUtc.AddSeconds(-session.DurationSeconds);
		}
		if (session.DurationSeconds <= 0 && session.StartUtc > DateTime.MinValue && session.EndUtc > session.StartUtc)
		{
			session.DurationSeconds = (long)(session.EndUtc - session.StartUtc).TotalSeconds;
		}
		if (session.ElapsedSeconds <= 0 && session.DurationSeconds > 0)
		{
			session.ElapsedSeconds = session.DurationSeconds;
		}
		if (session.Timestamp <= DateTime.MinValue && session.EndUtc > DateTime.MinValue)
		{
			session.Timestamp = session.EndUtc;
		}
		if (string.IsNullOrEmpty(session.Source))
		{
			session.Source = SessionSources.Playnite;
		}
		if (string.IsNullOrEmpty(session.Confidence))
		{
			session.Confidence = SessionConfidence.Exact;
		}
	}

	private static DateTime ToUtc(DateTime value)
	{
		if (value <= DateTime.MinValue)
		{
			return DateTime.MinValue;
		}
		if (value.Kind == DateTimeKind.Utc)
		{
			return value;
		}
		if (value.Kind == DateTimeKind.Local)
		{
			return value.ToUniversalTime();
		}
		return DateTime.SpecifyKind(value, DateTimeKind.Utc);
	}
}
