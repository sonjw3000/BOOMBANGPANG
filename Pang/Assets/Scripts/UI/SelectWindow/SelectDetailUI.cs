using System;
using Assets.Scripts.UI;
using UnityEngine;

public class SelectDetailUI : MonoBehaviour
{
	private UIWindow window;
	private DetailContentBase[] detailContents;
	private DetailContentBase currentContent;
	private GameObject currentTarget;

	public event Action<SelectDetailUI> WindowClosed;

	public bool IsOpen => window != null && window.IsOpen;
	public RectTransform SelfRect => transform as RectTransform;
	public RectTransform WindowRect => window != null ? window.RootRect : transform as RectTransform;
	public GameObject CurrentTarget => currentTarget;

	private void Awake()
	{
		window = GetComponentInChildren<UIWindow>(true);
		CacheDetailContents();

		if (window != null)
		{
			window.Closed -= HandleWindowClosed;
			window.Closed += HandleWindowClosed;
		}
	}

	private void OnDestroy()
	{
		if (window != null)
			window.Closed -= HandleWindowClosed;
	}

	public void RefreshDetailContentCache()
	{
		CacheDetailContents();
	}

	public bool ShowDetail(GameObject targetObj, UIProviderBase provider)
	{
		if (targetObj == null || provider == null)
			return false;

		CacheDetailContents();

		DetailContentBase nextContent = GetBestDetailContent(targetObj);
		if (nextContent == null)
		{
			Debug.LogWarning($"No suitable UI DetailBuilder found for the selected object, Target: {targetObj.name}");
			return false;
		}

		currentContent?.gameObject.SetActive(false);
		currentContent = nextContent;
		currentTarget = targetObj;

		if (window != null)
		{
			window.SetTitle(provider.Name);
			window.SetIcon(provider.Icon);
			window.Open();
		}

		currentContent.SetProvider(provider);
		BringToFront();
		return true;
	}

	public void BringToFront()
	{
		transform.SetAsLastSibling();
	}

	public void ReleaseToPool()
	{
		currentTarget = null;
		if (currentContent != null)
		{
			currentContent.gameObject.SetActive(false);
			currentContent = null;
		}

		if (window != null)
		{
			window.SetTitle(string.Empty);
			window.SetIcon(null);
			window.ClearTabs();
			if (window.IsOpen)
				window.Close();
		}
	}

	private void HandleWindowClosed()
	{
		currentTarget = null;
		if (currentContent != null)
		{
			currentContent.gameObject.SetActive(false);
			currentContent = null;
		}

		WindowClosed?.Invoke(this);
	}

	private void CacheDetailContents()
	{
		detailContents = GetComponentsInChildren<DetailContentBase>(true);
	}

	private DetailContentBase GetBestDetailContent(GameObject targetObj)
	{
		if (detailContents == null || detailContents.Length == 0 || targetObj == null)
			return null;

		DetailContentBase bestContent = null;
		int bestDistance = int.MaxValue;

		for (int i = 0; i < detailContents.Length; ++i)
		{
			DetailContentBase content = detailContents[i];
			if (content == null)
				continue;

			int distance = GetMatchDistance(targetObj, content.TargetType);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestContent = content;
			}
		}

		return bestContent;
	}

	private static int GetMatchDistance(GameObject targetObj, Type candidateType)
	{
		if (targetObj == null || candidateType == null)
			return int.MaxValue;

		Component matchedComponent = targetObj.GetComponent(candidateType);
		if (matchedComponent == null)
			return int.MaxValue;

		Type currentType = matchedComponent.GetType();
		int distance = 0;
		while (currentType != null)
		{
			if (currentType == candidateType)
				return distance;

			currentType = currentType.BaseType;
			distance++;
		}

		return int.MaxValue;
	}
}
