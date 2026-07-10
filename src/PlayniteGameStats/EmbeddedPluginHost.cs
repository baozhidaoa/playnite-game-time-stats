using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Playnite.SDK;

namespace PlayniteGameStats;

public class EmbeddedPluginHost
{
	private const string HostName = "PART_GameTimeStatsOverlayHost";

	private readonly IPlayniteAPI _api;

	private readonly Func<Control> _createStatsView;

	private string _activeView;

	private Action _currentClosed;

	public EmbeddedPluginHost(IPlayniteAPI api, Func<Control> createStatsView)
	{
		_api = api;
		_createStatsView = createStatsView;
	}

	public void ToggleGameStats()
	{
		ToggleView("gamestats", CreateGameStatsContainer(), null);
	}

	public void Clear()
	{
		ContentControl contentControl = FindHost();
		if (contentControl != null)
		{
			CloseCurrentView();
			contentControl.Content = null;
			contentControl.Visibility = Visibility.Collapsed;
			_activeView = null;
		}
	}

	private void ToggleView(string key, Control view, Action closed)
	{
		ContentControl contentControl = FindHost();
		if (contentControl == null)
		{
			_api.Dialogs.ShowErrorMessage("Current theme does not contain PART_GameTimeStatsOverlayHost.", "GameTimeStats");
			return;
		}
		if (_activeView == key && contentControl.Visibility == Visibility.Visible)
		{
			Clear();
			return;
		}
		CloseCurrentView();
		Stretch(view);
		contentControl.Content = view;
		contentControl.Visibility = Visibility.Visible;
		_currentClosed = closed;
		_activeView = key;
	}

	private Control CreateGameStatsContainer()
	{
		Control control = _createStatsView();
		Stretch(control);
		Grid grid = new Grid();
		grid.Background = Brushes.Transparent;
		grid.Children.Add(control);
		Button button = new Button
		{
			Content = "返回库",
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(12.0),
			Padding = new Thickness(12.0, 6.0, 12.0, 6.0),
			MinWidth = 72.0,
			MinHeight = 30.0,
			Background = new SolidColorBrush(Color.FromArgb(220, 24, 24, 24)),
			Foreground = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromArgb(160, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderThickness = new Thickness(1.0)
		};
		button.Click += delegate
		{
			Clear();
		};
		Panel.SetZIndex(button, 20);
		grid.Children.Add(button);
		return new ContentControl
		{
			Content = grid
		};
	}

	private ContentControl FindHost()
	{
		Window window = ((Application.Current == null) ? null : Application.Current.MainWindow);
		if (window == null)
		{
			return null;
		}
		return ((window.Template == null) ? null : (window.Template.FindName("PART_GameTimeStatsOverlayHost", window) as ContentControl)) ?? FindVisualChild<ContentControl>(window, "PART_GameTimeStatsOverlayHost");
	}

	private static T FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
	{
		if (parent == null)
		{
			return null;
		}
		int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is T val && val.Name == name)
			{
				return val;
			}
			T val2 = FindVisualChild<T>(child, name);
			if (val2 != null)
			{
				return val2;
			}
		}
		return null;
	}

	private static void Stretch(Control control)
	{
		if (control != null)
		{
			control.HorizontalAlignment = HorizontalAlignment.Stretch;
			control.VerticalAlignment = VerticalAlignment.Stretch;
		}
	}

	private void CloseCurrentView()
	{
		try
		{
			if (_currentClosed != null)
			{
				_currentClosed();
			}
		}
		catch
		{
		}
		_currentClosed = null;
	}
}
