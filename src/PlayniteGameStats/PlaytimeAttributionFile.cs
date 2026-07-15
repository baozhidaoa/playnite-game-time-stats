using System;
using System.Collections.Generic;

namespace PlayniteGameStats;

public class PlaytimeAttributionFile
{
	public int SchemaVersion = 1;

	public long NextSequence = 1;

	public Dictionary<string, GameAttributionState> Games = new Dictionary<string, GameAttributionState>(StringComparer.OrdinalIgnoreCase);
}

public class GameAttributionState
{
	public string Seed;

	public bool Initialized;

	public DateTime FallbackAnchorLocal;

	public List<AttributedDay> Days = new List<AttributedDay>();
}

public class AttributedDay
{
	public string Date;

	public double Minutes;

	public long Sequence;
}
