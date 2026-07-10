using System;

namespace PlayniteGameStats;

public class SessionRecord
{
	public string GameId;

	public long ElapsedSeconds;

	public DateTime Timestamp;

	public DateTime StartUtc;

	public DateTime EndUtc;

	public long DurationSeconds;

	public string Source;

	public string Confidence;

	public string ExternalAppId;
}
