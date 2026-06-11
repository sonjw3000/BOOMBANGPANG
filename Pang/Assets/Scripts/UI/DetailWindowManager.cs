using System.Collections.Generic;
using UnityEngine;

public class DetailWindowManager : MonoBehaviour
{
	[SerializeField] private SelectDetailUI windowTemplate;
	[SerializeField] private Vector2 spawnOffset = new(28f, -28f);
	[SerializeField] private int cascadeCount = 6;

	private readonly List<SelectDetailUI> pooledWindows = new();
	private RectTransform windowParent;
	private RectTransform templateSelfRect;
	private RectTransform templateWindowRect;
	private Vector2 templateAnchoredPosition;
	private bool initialized;
	private int openedWindowCount;

	public void Initialize(SelectDetailUI template)
	{
		if (initialized && windowTemplate == template && template != null)
			return;

		windowTemplate = template;
		if (windowTemplate == null)
			return;

		windowParent = windowTemplate.transform.parent as RectTransform;
		templateSelfRect = windowTemplate.SelfRect;
		templateWindowRect = windowTemplate.WindowRect;
		templateAnchoredPosition = windowTemplate.WindowRect != null
			? windowTemplate.WindowRect.anchoredPosition
			: Vector2.zero;
		windowTemplate.gameObject.SetActive(false);
		initialized = true;
	}

	public SelectDetailUI OpenDetail(GameObject targetObj, UIProviderBase provider)
	{
		if (targetObj == null || provider == null || windowTemplate == null)
			return null;

		SelectDetailUI openedWindow = FindOpenWindowForTarget(targetObj);
		if (openedWindow != null)
		{
			openedWindow.ShowDetail(targetObj, provider);
			openedWindow.BringToFront();
			return openedWindow;
		}

		SelectDetailUI detailWindow = GetOrCreateWindow();
		if (detailWindow == null)
			return null;

		ResetWindowLayout(detailWindow);
		PositionWindow(detailWindow);
		detailWindow.RefreshDetailContentCache();
		if (detailWindow.ShowDetail(targetObj, provider) == false)
			return null;

		openedWindowCount += 1;
		return detailWindow;
	}

	private SelectDetailUI GetOrCreateWindow()
	{
		for (int i = 0; i < pooledWindows.Count; ++i)
		{
			SelectDetailUI pooledWindow = pooledWindows[i];
			if (pooledWindow != null && pooledWindow.IsOpen == false)
				return pooledWindow;
		}

		return CreateWindowInstance();
	}

	private SelectDetailUI CreateWindowInstance()
	{
		if (windowTemplate == null)
			return null;

		SelectDetailUI instance = Instantiate(windowTemplate, windowParent != null ? windowParent : transform, false);
		instance.name = $"{windowTemplate.name}_{pooledWindows.Count + 1}";
		instance.gameObject.SetActive(true);
		ResetWindowLayout(instance);
		instance.RefreshDetailContentCache();
		instance.WindowClosed -= HandleWindowClosed;
		instance.WindowClosed += HandleWindowClosed;
		pooledWindows.Add(instance);
		return instance;
	}

	private SelectDetailUI FindOpenWindowForTarget(GameObject targetObj)
	{
		if (targetObj == null)
			return null;

		for (int i = 0; i < pooledWindows.Count; ++i)
		{
			SelectDetailUI pooledWindow = pooledWindows[i];
			if (pooledWindow == null || pooledWindow.IsOpen == false)
				continue;

			if (pooledWindow.CurrentTarget == targetObj)
				return pooledWindow;
		}

		return null;
	}

	private void HandleWindowClosed(SelectDetailUI closedWindow)
	{
		if (closedWindow == null)
			return;

		closedWindow.transform.SetAsFirstSibling();
	}

	private void PositionWindow(SelectDetailUI detailWindow)
	{
		if (detailWindow == null || detailWindow.WindowRect == null)
			return;

		int cascadeIndex = cascadeCount <= 0 ? 0 : openedWindowCount % cascadeCount;
		detailWindow.WindowRect.anchoredPosition = templateAnchoredPosition + (spawnOffset * cascadeIndex);
		detailWindow.BringToFront();
	}

	private void ResetWindowLayout(SelectDetailUI detailWindow)
	{
		if (detailWindow == null)
			return;

		CopyRectTransform(templateSelfRect, detailWindow.SelfRect);

		RectTransform targetWindowRect = detailWindow.WindowRect;
		if (templateWindowRect != null && targetWindowRect != null && targetWindowRect != detailWindow.SelfRect)
			CopyRectTransform(templateWindowRect, targetWindowRect);
	}

	private static void CopyRectTransform(RectTransform source, RectTransform target)
	{
		if (source == null || target == null)
			return;

		target.anchorMin = source.anchorMin;
		target.anchorMax = source.anchorMax;
		target.pivot = source.pivot;
		target.sizeDelta = source.sizeDelta;
		target.anchoredPosition = source.anchoredPosition;
		target.localScale = source.localScale;
		target.localRotation = source.localRotation;
	}
}
