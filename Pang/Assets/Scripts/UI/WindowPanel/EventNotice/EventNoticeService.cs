using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.UI
{
	public sealed class EventNoticeService : MonoBehaviour
	{
		[SerializeField] private EventNoticeWindow eventNoticeWindowPrefab;
		[SerializeField] private Transform windowParent;
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
			if (poolInitialized || eventNoticeWindowPrefab == null)
				return;

			int targetCount = Mathf.Max(1, initialPoolSize);
			for (int i = 0; i < targetCount; ++i)
			{
				EventNoticeWindow instance = CreateWindowInstance();
				ReleaseWindow(instance);
			}

			poolInitialized = true;
		}

		private EventNoticeWindow CreateWindowInstance()
		{
			Transform parent = windowParent != null ? windowParent : transform;
			EventNoticeWindow instance = Instantiate(eventNoticeWindowPrefab, parent);
			instance.name = eventNoticeWindowPrefab.name;
			instance.gameObject.SetActive(false);
			instance.Dismissed -= HandleWindowDismissed;
			instance.Dismissed += HandleWindowDismissed;
			return instance;
		}

		private EventNoticeWindow AcquireWindow()
		{
			EnsurePoolInitialized();

			if (pooledWindows.Count > 0)
				return pooledWindows.Pop();

			return CreateWindowInstance();
		}

		private void ReleaseWindow(EventNoticeWindow window)
		{
			if (window == null)
				return;

			window.transform.SetParent(windowParent != null ? windowParent : transform, false);
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
			{
				RectTransform rect = activeWindows[i].GetComponent<RectTransform>();
				if (rect == null)
					continue;

				rect.anchoredPosition = GetWindowPosition(i);
			}
		}

		private Vector2 GetWindowPosition(int index)
		{
			return initialWindowPosition + stackedWindowOffset * index;
		}

		public void ShowNotice(EventNoticeRequest request)
		{
			if (request == null)
				return;

			EventNoticeWindow window = AcquireWindow();
			activeWindows.Add(window);
			window.Show(request, GetWindowPosition(activeWindows.Count - 1));
		}
	}
}
