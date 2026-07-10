using System.Collections.Generic;
using System.ComponentModel;
using Playnite.SDK;

namespace PlayniteGameStats;

public class PluginSettings : ISettings, IEditableObject
{
	private PluginSettings _editingClone;

	public bool EnableOnlineSteamSync { get; set; }

	public string SteamApiKey { get; set; }

	public string SteamId64 { get; set; }

	public void BeginEdit()
	{
		_editingClone = (PluginSettings)MemberwiseClone();
	}

	public void CancelEdit()
	{
		if (_editingClone != null)
		{
			EnableOnlineSteamSync = _editingClone.EnableOnlineSteamSync;
			SteamApiKey = _editingClone.SteamApiKey;
			SteamId64 = _editingClone.SteamId64;
			_editingClone = null;
		}
	}

	public void EndEdit()
	{
		_editingClone = null;
	}

	public bool VerifySettings(out List<string> errors)
	{
		errors = new List<string>();
		return true;
	}
}
