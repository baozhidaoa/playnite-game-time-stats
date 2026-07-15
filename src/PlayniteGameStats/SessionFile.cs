using System;
using System.Collections.Generic;

namespace PlayniteGameStats;

public class SessionFile
{
	public int SchemaVersion = 3;

	public List<SessionRecord> Sessions = new List<SessionRecord>();

	public List<ActiveSessionRecord> ActiveSessions = new List<ActiveSessionRecord>();
}

public class ActiveSessionRecord
{
	public string GameId;

	public DateTime StartUtc;

	public DateTime LastHeartbeatUtc;

	public string ExternalAppId;
}
