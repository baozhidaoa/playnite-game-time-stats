using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace PlayniteGameStats;

public class SettingsView : UserControl
{
	public SettingsView()
	{
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(12.0),
			MaxWidth = 620.0
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = PluginLocalization.Get("SettingsSteamData", "Steam data"),
			FontWeight = FontWeights.Bold,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = SteamDataProvider.GetAutoConfigSummary(),
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		});
		CheckBox checkBox = new CheckBox
		{
			Content = PluginLocalization.Get("SettingsEnableOnlineSteamSync", "Enable online Steam sync (when left blank, existing Playnite/Steam configuration is used first)"),
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		checkBox.SetBinding(ToggleButton.IsCheckedProperty, new Binding("EnableOnlineSteamSync")
		{
			Mode = BindingMode.TwoWay
		});
		stackPanel.Children.Add(checkBox);
		stackPanel.Children.Add(new TextBlock
		{
			Text = PluginLocalization.Get("SettingsSteamApiKey", "Steam API Key"),
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
		});
		TextBox textBox = new TextBox
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		textBox.SetBinding(TextBox.TextProperty, new Binding("SteamApiKey")
		{
			Mode = BindingMode.TwoWay,
			UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
		});
		stackPanel.Children.Add(textBox);
		stackPanel.Children.Add(new TextBlock
		{
			Text = PluginLocalization.Get("SettingsSteamId64", "SteamId64 (leave blank to infer from local Steam userdata)"),
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
		});
		TextBox textBox2 = new TextBox
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		textBox2.SetBinding(TextBox.TextProperty, new Binding("SteamId64")
		{
			Mode = BindingMode.TwoWay,
			UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
		});
		stackPanel.Children.Add(textBox2);
		stackPanel.Children.Add(new TextBlock
		{
			Text = PluginLocalization.Get("SettingsPriorityNote", "Priority: values entered in this plugin > plaintext Steam API/SteamId from Playnite extension configuration > SteamId64 inferred from local Steam userdata. The API Key from Playnite's Steam integration page is stored in an encrypted token and is not exposed through the public SDK; without an API Key, the plugin will still read local Steam localconfig.vdf."),
			TextWrapping = TextWrapping.Wrap
		});
		base.Content = stackPanel;
	}
}
