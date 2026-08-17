using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class UIWindowFocusCoordinator : IDisposable
	{
		private readonly List<WindowEntry> registeredWindows = new();
		private readonly List<WindowEntry> focusedWindows = new();
		private readonly float firstSortingOrder;
		private readonly float lastSortingOrder;

		public UIWindowFocusCoordinator(float firstSortingOrder, float lastSortingOrder)
		{
			if (float.IsNaN(firstSortingOrder) || float.IsInfinity(firstSortingOrder))
				throw new ArgumentOutOfRangeException(nameof(firstSortingOrder));
			if (float.IsNaN(lastSortingOrder) || float.IsInfinity(lastSortingOrder) ||
				lastSortingOrder <= firstSortingOrder)
			{
				throw new ArgumentOutOfRangeException(nameof(lastSortingOrder));
			}

			this.firstSortingOrder = firstSortingOrder;
			this.lastSortingOrder = lastSortingOrder;
		}

		public void Register(UIWindow window, UIDocument document)
		{
			if (window == null)
				throw new ArgumentNullException(nameof(window));
			if (document == null)
				throw new ArgumentNullException(nameof(document));

			WindowEntry existing = FindEntry(window);
			if (existing != null)
			{
				if (existing.Document != document)
					throw new InvalidOperationException("A UIWindow cannot be registered with multiple UIDocuments.");
				return;
			}

			WindowEntry entry = new(window, document);
			entry.FocusRequested = () => BringToFront(window);
			entry.Closed = () => RemoveFromFocus(window);
			window.FocusRequested += entry.FocusRequested;
			window.Closed += entry.Closed;
			registeredWindows.Add(entry);
			document.sortingOrder = firstSortingOrder;
		}

		public void Unregister(UIWindow window)
		{
			WindowEntry entry = FindEntry(window);
			if (entry == null)
				return;

			Unbind(entry);
			focusedWindows.Remove(entry);
			registeredWindows.Remove(entry);
			ApplySortingOrders();
		}

		public void BringToFront(UIWindow window)
		{
			WindowEntry entry = FindEntry(window);
			if (entry == null)
				return;

			focusedWindows.Remove(entry);
			focusedWindows.Add(entry);
			ApplySortingOrders();
		}

		public void Dispose()
		{
			for (int i = 0; i < registeredWindows.Count; ++i)
				Unbind(registeredWindows[i]);

			focusedWindows.Clear();
			registeredWindows.Clear();
		}

		private void RemoveFromFocus(UIWindow window)
		{
			WindowEntry entry = FindEntry(window);
			if (entry == null || focusedWindows.Remove(entry) == false)
				return;

			ApplySortingOrders();
		}

		private WindowEntry FindEntry(UIWindow window)
		{
			for (int i = 0; i < registeredWindows.Count; ++i)
			{
				if (registeredWindows[i].Window == window)
					return registeredWindows[i];
			}

			return null;
		}

		private void ApplySortingOrders()
		{
			for (int i = 0; i < registeredWindows.Count; ++i)
			{
				UIDocument document = registeredWindows[i].Document;
				if (document != null)
					document.sortingOrder = firstSortingOrder;
			}

			if (focusedWindows.Count == 0)
				return;

			float step = focusedWindows.Count == 1
				? 0f
				: (lastSortingOrder - firstSortingOrder) / (focusedWindows.Count - 1);
			for (int i = 0; i < focusedWindows.Count; ++i)
			{
				UIDocument document = focusedWindows[i].Document;
				if (document != null)
					document.sortingOrder = firstSortingOrder + step * i;
			}
		}

		private static void Unbind(WindowEntry entry)
		{
			if (entry.Window == null)
				return;

			entry.Window.FocusRequested -= entry.FocusRequested;
			entry.Window.Closed -= entry.Closed;
		}

		private sealed class WindowEntry
		{
			public UIWindow Window { get; }
			public UIDocument Document { get; }
			public Action FocusRequested { get; set; }
			public Action Closed { get; set; }

			public WindowEntry(UIWindow window, UIDocument document)
			{
				Window = window;
				Document = document;
			}
		}
	}
}
