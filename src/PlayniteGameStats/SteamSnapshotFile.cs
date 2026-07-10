using System;
using System.Collections.Generic;

namespace PlayniteGameStats;

public class SteamSnapshotFile
{
	public int SchemaVersion = 1;

	public DateTime CapturedUtc;

	public Dictionary<string, SteamAppSnapshot> Apps = new Dictionary<string, SteamAppSnapshot>(StringComparer.OrdinalIgnoreCase);
}

public class SteamAppSnapshot
{
	public string AppId;

	public double TotalMinutes;

	public double PlaytimeMinutes;

	public double PlaytimeDisconnectedMinutes;

	public double Playtime2WeeksMinutes;

	public DateTime LastPlayedUtc;

	public DateTime CapturedUtc;
}
