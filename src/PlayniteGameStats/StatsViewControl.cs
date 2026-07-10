using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using Playnite.SDK;

namespace PlayniteGameStats;

public class StatsViewControl : IDisposable
{
	private readonly IPlayniteAPI _api;

	private readonly StatsCalculator _statsCalculator;

	private Control _browserControl;

	private bool _disposed;

	private readonly object _dataLock = new object();

	private string _pendingDataScript;

	private string _webAssetPath;

	public StatsViewControl(IPlayniteAPI api, StatsCalculator statsCalculator)
	{
		_api = api;
		_statsCalculator = statsCalculator;
	}

	public Control CreateView()
	{
		_disposed = false;
		_webAssetPath = PrepareWebAssets();
		WriteEmptyDataJs();
		string text = "file:///" + Path.Combine(_webAssetPath, "index.html").Replace("\\", "/").Replace(" ", "%20");
		Type type = Type.GetType("CefSharp.Wpf.ChromiumWebBrowser, CefSharp.Wpf", throwOnError: false);
		if (type != null)
		{
			ConstructorInfo constructor = type.GetConstructor(new Type[1] { typeof(string) });
			if (constructor != null)
			{
				_browserControl = (Control)constructor.Invoke(new object[1] { text });
				_browserControl.HorizontalAlignment = HorizontalAlignment.Stretch;
				_browserControl.VerticalAlignment = VerticalAlignment.Stretch;
				AttachLoadingStateHandler(type);
				ContentControl result = new ContentControl
				{
					Content = _browserControl
				};
				QueueDataRefresh();
				return result;
			}
		}
		QueueDataRefresh();
		return CreateFallbackView();
	}

	public void RefreshData()
	{
		QueueDataRefresh();
	}

	private void WriteEmptyDataJs()
	{
		try
		{
			if (!string.IsNullOrEmpty(_webAssetPath))
			{
				string path = Path.Combine(_webAssetPath, "data.js");
				if (!File.Exists(path))
				{
					File.WriteAllText(path, "window.__STATS_DATA__ = null;");
				}
			}
		}
		catch
		{
		}
	}

	private void QueueDataRefresh()
	{
		if (string.IsNullOrEmpty(_webAssetPath))
		{
			return;
		}
		string webAssetPath = _webAssetPath;
		Task.Factory.StartNew(() => BuildDataScript()).ContinueWith(delegate(Task<string> t)
		{
			if (!_disposed && !t.IsFaulted && !t.IsCanceled)
			{
				string result = t.Result;
				if (!string.IsNullOrEmpty(result))
				{
					try
					{
						File.WriteAllText(Path.Combine(webAssetPath, "data.js"), result);
					}
					catch
					{
					}
					lock (_dataLock)
					{
						_pendingDataScript = result;
					}
					try
					{
						if (_browserControl != null)
						{
							_browserControl.Dispatcher.BeginInvoke((Action)delegate
							{
								PushData();
							});
						}
					}
					catch
					{
					}
				}
			}
		});
	}

	private void AttachLoadingStateHandler(Type browserType)
	{
		try
		{
			EventInfo @event = browserType.GetEvent("LoadingStateChanged");
			if (@event == null || @event.EventHandlerType == null)
			{
				return;
			}
			Delegate handler = CreateLoadingStateChangedHandler(@event.EventHandlerType);
			if (handler != null)
			{
				@event.AddEventHandler(_browserControl, handler);
			}
		}
		catch
		{
		}
	}

	private Delegate CreateLoadingStateChangedHandler(Type eventHandlerType)
	{
		try
		{
			MethodInfo invoke = eventHandlerType.GetMethod("Invoke");
			ParameterInfo[] parameters = invoke?.GetParameters();
			if (parameters == null || parameters.Length == 0)
			{
				return null;
			}
			ParameterExpression[] expressions = new ParameterExpression[parameters.Length];
			for (int i = 0; i < parameters.Length; i++)
			{
				expressions[i] = System.Linq.Expressions.Expression.Parameter(parameters[i].ParameterType, parameters[i].Name);
			}
			System.Linq.Expressions.Expression sender = System.Linq.Expressions.Expression.Convert(expressions[0], typeof(object));
			System.Linq.Expressions.Expression args = parameters.Length > 1 ? System.Linq.Expressions.Expression.Convert(expressions[1], typeof(object)) : System.Linq.Expressions.Expression.Constant(null, typeof(object));
			MethodInfo method = GetType().GetMethod("OnBrowserLoadingStateChanged", BindingFlags.Instance | BindingFlags.NonPublic);
			MethodCallExpression call = System.Linq.Expressions.Expression.Call(System.Linq.Expressions.Expression.Constant(this), method, sender, args);
			return System.Linq.Expressions.Expression.Lambda(eventHandlerType, call, expressions).Compile();
		}
		catch
		{
			return null;
		}
	}

	private void OnBrowserLoadingStateChanged(object sender, object args)
	{
		if (_disposed || IsBrowserLoading(args))
		{
			return;
		}
		QueueDataRefresh();
	}

	private static bool IsBrowserLoading(object args)
	{
		try
		{
			PropertyInfo property = args?.GetType().GetProperty("IsLoading");
			if (property != null && property.PropertyType == typeof(bool))
			{
				return (bool)property.GetValue(args, null);
			}
		}
		catch
		{
		}
		return false;
	}

	private string BuildDataScript()
	{
		try
		{
			string text = JsonConvert.SerializeObject(_statsCalculator.GetFullStats(), new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				Formatting = Formatting.None
			});
			return "window.__STATS_DATA__ = " + text + ";";
		}
		catch
		{
			return null;
		}
	}

	private void PushData()
	{
		try
		{
			if (_browserControl == null)
			{
				return;
			}
			string pendingDataScript;
			lock (_dataLock)
			{
				pendingDataScript = _pendingDataScript;
				_pendingDataScript = null;
			}
			if (!string.IsNullOrEmpty(pendingDataScript))
			{
				MethodInfo method = _browserControl.GetType().GetMethod("ExecuteScriptAsync", new Type[1] { typeof(string) });
				if (method != null)
				{
					method.Invoke(_browserControl, new object[1] { pendingDataScript + " if(window.GameStats&&window.GameStats.reload)window.GameStats.reload();" });
				}
			}
		}
		catch
		{
		}
	}

	private Control CreateFallbackView()
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = "无法加载统计视图，请检查 Playnite 版本。";
		textBlock.Foreground = Brushes.White;
		textBlock.FontSize = 14.0;
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		return new ContentControl
		{
			Content = textBlock
		};
	}

	private string PrepareWebAssets()
	{
		string sourceDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "web");
		string text = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "web-ui");
		string path = Path.Combine(text, ".version");
		string text2 = "3.5";
		bool flag = true;
		if (File.Exists(path))
		{
			try
			{
				flag = File.ReadAllText(path).Trim() != text2;
			}
			catch
			{
				flag = true;
			}
		}
		if (flag)
		{
			try
			{
				if (Directory.Exists(text))
				{
					Directory.Delete(text, recursive: true);
				}
			}
			catch
			{
			}
			CopyDirectory(sourceDir, text);
			File.WriteAllText(path, text2);
		}
		return text;
	}

	private static void CopyDirectory(string sourceDir, string destDir)
	{
		Directory.CreateDirectory(destDir);
		string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
		foreach (string text in files)
		{
			string path = text.Substring(sourceDir.Length).TrimStart('\\', '/');
			string text2 = Path.Combine(destDir, path);
			string directoryName = Path.GetDirectoryName(text2);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			File.Copy(text, text2, overwrite: true);
		}
	}

	public void Dispose()
	{
		_disposed = true;
		if (_browserControl != null)
		{
			try
			{
				(_browserControl as IDisposable).Dispose();
			}
			catch
			{
			}
			_browserControl = null;
		}
	}
}
