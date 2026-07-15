using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ToolkitWindow = UniverseLogistics.UI.Toolkit.UIWindow;

namespace Assets.Scripts.UI
{
	public sealed class EventNoticeService : MonoBehaviour
	{
		[SerializeField] private VisualTreeAsset windowVisualTreeAsset;
		[SerializeField] private VisualTreeAsset contentTemplate;
		[SerializeField] private PanelSettings panelSettings;
		[SerializeField] private Sprite defaultIcon;
		[SerializeField] private string idleWindowTitle = "Event Notice";
		[SerializeField] private Vector2 defaultWindowSize = new(500f, 800f);
		[SerializeField] private int sortingOrder = 130;
		[SerializeField, Min(1)] private int initialPoolSize = 1;
		[SerializeField] private Vector2 initialWindowPosition = Vector2.zero;
		[SerializeField] private Vector2 stackedWindowOffset = new(32f, -32f);

		private readonly Stack<EventNoticeWindow> pooledWindows = new();
		private readonly List<EventNoticeWindow> activeWindows = new();
		private bool poolInitialized;

		private void Awake()
		{
			EnsurePoolInitialized();
		}

		private void EnsurePoolInitialized()
		{
			if (poolInitialized || HasRequiredAssets() == false)
				return;

			int targetCount = Mathf.Max(1, initialPoolSize);
			for (int i = 0; i < targetCount; ++i)
				ReleaseWindow(CreateWindowInstance());

			poolInitialized = true;
		}

		private bool HasRequiredAssets()
		{
			if (windowVisualTreeAsset != null && contentTemplate != null && panelSettings != null)
				return true;

			Debug.LogError("[EventNoticeService] Toolkit window, content template, or PanelSettings is missing.", this);
			return false;
		}

		private EventNoticeWindow CreateWindowInstance()
		{
			GameObject windowObject = new("EventNoticeWindowDocument");
			windowObject.SetActive(false);
			windowObject.transform.SetParent(transform, false);

			UIDocument document = windowObject.AddComponent<UIDocument>();
			document.panelSettings = panelSettings;
			document.visualTreeAsset = windowVisualTreeAsset;
			document.sortingOrder = sortingOrder + activeWindows.Count;

			ToolkitWindow toolkitWindow = windowObject.AddComponent<ToolkitWindow>();
			toolkitWindow.SetOpenOnEnable(false);
			toolkitWindow.SetDefaultSize(defaultWindowSize);

			EventNoticeWindow instance = windowObject.AddComponent<EventNoticeWindow>();
			instance.Configure(toolkitWindow, contentTemplate, idleWindowTitle, defaultIcon, defaultWindowSize);
			instance.Dismissed += HandleWindowDismissed;
			return instance;
		}

		private EventNoticeWindow AcquireWindow()
		{
			EnsurePoolInitialized();
			return pooledWindows.Count > 0 ? pooledWindows.Pop() : CreateWindowInstance();
		}

		private void ReleaseWindow(EventNoticeWindow window)
		{
			if (window == null)
				return;

			window.gameObject.SetActive(false);
			pooledWindows.Push(window);
		}

		private void HandleWindowDismissed(EventNoticeWindow window)
		{
			if (window == null)
				return;

			activeWindows.Remove(window);
			RepositionActiveWindows();
			ReleaseWindow(window);
		}

		private void RepositionActiveWindows()
		{
			for (int i = 0; i < activeWindows.Count; ++i)
				activeWindows[i].ShowPosition(GetWindowPosition(i));
		}

		private Vector2 GetWindowPosition(int index)
		{
			return initialWindowPosition + stackedWindowOffset * index;
		}

		public void ShowNotice(EventNoticeRequest request)
		{
			if (request == null || HasRequiredAssets() == false)
				return;

			EventNoticeWindow window = AcquireWindow();
			activeWindows.Add(window);
			window.Show(request, GetWindowPosition(activeWindows.Count - 1));
		}
	}
}
