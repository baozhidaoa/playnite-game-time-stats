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
			Text = "Steam 数据",
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
			Content = "启用在线 Steam 同步（留空时优先读取 Playnite/Steam 已有配置）",
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		checkBox.SetBinding(ToggleButton.IsCheckedProperty, new Binding("EnableOnlineSteamSync")
		{
			Mode = BindingMode.TwoWay
		});
		stackPanel.Children.Add(checkBox);
		stackPanel.Children.Add(new TextBlock
		{
			Text = "Steam API Key",
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
			Text = "SteamId64（留空时自动从本机 Steam userdata 推断）",
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
			Text = "优先级：本插件填写的值 > Playnite 扩展配置中的明文 Steam API/SteamId > 本机 Steam userdata 推断 SteamId64。Playnite Steam 集成页的 API Key 存在加密令牌中，插件无法通过公共 SDK 直接读取；无 API Key 时仍会读取本机 Steam localconfig.vdf。",
			TextWrapping = TextWrapping.Wrap
		});
		base.Content = stackPanel;
	}
}
