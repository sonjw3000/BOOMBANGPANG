using System.Collections.Generic;
using Assets.Scripts.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public abstract class DetailContentBase : MonoBehaviour
{
	[SerializeField] protected Button deleteButton = null;
	[SerializeField] protected DetailDefaultTabsView defaultTabsPrefab = null;
	[SerializeField] protected TextButtonView actionButtonPrefab = null;
	[SerializeField] protected TextButtonView compactButtonPrefab = null;

	protected UIProviderBase provider = null;
	private UIWindow window;
	private RectTransform defaultBodyRoot;
	private RectTransform infoTabRoot;
	private RectTransform actionTabRoot;
	private readonly List<GameObject> defaultTabRoots = new();
	private readonly List<Button> runtimeActionButtons = new();
	private bool defaultTabsBuilt;

	public Button.ButtonClickedEvent DeleteButtonEvent => deleteButton.onClick;
	protected RectTransform InfoTabRoot => infoTabRoot;
	protected RectTransform ActionTabRoot => actionTabRoot;
	protected virtual bool UseDefaultTabs => true;
	protected virtual string DefaultInfoTabLabel => "Info";
	protected virtual string DefaultActionTabLabel => "Action";
	public abstract System.Type TargetType { get; }

	private void OnValidate()
	{
		if (UseDefaultTabs == false)
			return;

		if (deleteButton == null)
			Debug.LogError("Delete Button is not assigned!", this);
	}

	private void OnEnable()
	{
		if (UseDefaultTabs == false && deleteButton != null)
			DeleteButtonEvent.AddListener(() => provider?.DeleteObject());

		AddListener();
	}

	private void OnDisable()
	{
		if (deleteButton != null)
			DeleteButtonEvent.RemoveAllListeners();

		ClearRuntimeActionButtons();
		RemoveListeners();
	}

	protected virtual void AddListener() { }
	protected virtual void RemoveListeners() { }

	public abstract bool IsTargetType(GameObject obj);

	public void SetProvider(UIProviderBase provider)
	{
		this.provider = provider;
		EnsureDefaultTabs();
		if (UseDefaultTabs)
		{
			RebuildRuntimeActionButtons();
			SetupDefaultTabs();
			SetDefaultTab(0);
		}

		LinkData();
		gameObject.SetActive(true);
	}

	protected void HideLegacyVisuals()
	{
		DisableLegacyRootLayout();

		foreach (Transform child in transform)
			child.gameObject.SetActive(false);

		if (deleteButton != null)
			deleteButton.gameObject.SetActive(false);
	}

	protected void DisableLegacyRootLayout()
	{
		foreach (LayoutGroup layoutGroup in GetComponents<LayoutGroup>())
			layoutGroup.enabled = false;

		foreach (ContentSizeFitter fitter in GetComponents<ContentSizeFitter>())
			fitter.enabled = false;

		foreach (LayoutElement layoutElement in GetComponents<LayoutElement>())
			layoutElement.enabled = false;
	}

	protected Button CreateRuntimeActionButton(Transform parent, string label, UnityAction onClick)
	{
		TextButtonView buttonView = CreateActionButtonView(parent, label, onClick);
		return buttonView != null ? buttonView.Button : null;
	}

	protected Button CreateDeleteActionButton(Transform parent)
	{
		return CreateRuntimeActionButton(parent, "Delete", () => provider?.DeleteObject());
	}

	protected Button RegisterActionButton(Button button)
	{
		if (button != null)
			runtimeActionButtons.Add(button);

		return button;
	}

	protected virtual void BuildActionButtons(RectTransform actionRoot)
	{
		RegisterActionButton(CreateDeleteActionButton(actionRoot));
	}

	private void EnsureDefaultTabs()
	{
		if (UseDefaultTabs == false || defaultTabsBuilt)
			return;

		window = GetComponentInParent<UIWindow>(true);
		RectTransform selfRect = GetComponent<RectTransform>();
		if (selfRect != null)
		{
			selfRect.anchorMin = Vector2.zero;
			selfRect.anchorMax = Vector2.one;
			selfRect.offsetMin = Vector2.zero;
			selfRect.offsetMax = Vector2.zero;
			selfRect.pivot = new Vector2(0.5f, 0.5f);
		}

		if (defaultTabsPrefab == null)
		{
			Debug.LogError($"[{GetType().Name}] Default tabs prefab is missing.", this);
			return;
		}

		DetailDefaultTabsView tabsView = Instantiate(defaultTabsPrefab, transform);
		tabsView.name = "DefaultDetailTabs";
		defaultBodyRoot = tabsView.GetComponent<RectTransform>();
		SetTopStretch(defaultBodyRoot, 12f, 12f, 4f);

		infoTabRoot = tabsView.InfoTabRoot;
		actionTabRoot = tabsView.ActionTabRoot;
		defaultTabRoots.Add(infoTabRoot.gameObject);
		defaultTabRoots.Add(actionTabRoot.gameObject);

		List<Transform> childrenToMove = new();
		foreach (Transform child in transform)
		{
			if (child == defaultBodyRoot)
				continue;

			childrenToMove.Add(child);
		}

		foreach (Transform child in childrenToMove)
			child.SetParent(infoTabRoot, false);

		if (deleteButton != null)
			deleteButton.gameObject.SetActive(false);

		defaultTabsBuilt = true;
	}

	private void SetupDefaultTabs()
	{
		if (window == null)
			window = GetComponentInParent<UIWindow>(true);

		if (window == null)
			return;

		window.ClearTabs();
		window.AddTab(DefaultInfoTabLabel, SetDefaultTab);
		window.AddTab(DefaultActionTabLabel, SetDefaultTab);
		window.UpdateTabVisuals(0);
	}

	private void SetDefaultTab(int tabIndex)
	{
		for (int i = 0; i < defaultTabRoots.Count; i++)
			defaultTabRoots[i].SetActive(i == tabIndex);

		window?.UpdateTabVisuals(tabIndex);
	}

	private void RebuildRuntimeActionButtons()
	{
		ClearRuntimeActionButtons();
		if (actionTabRoot == null)
			return;

		BuildActionButtons(actionTabRoot);
	}

	private void ClearRuntimeActionButtons()
	{
		foreach (Button runtimeActionButton in runtimeActionButtons)
		{
			if (runtimeActionButton == null)
				continue;

			runtimeActionButton.onClick.RemoveAllListeners();
			Destroy(runtimeActionButton.gameObject);
		}

		runtimeActionButtons.Clear();
	}

	protected TextButtonView CreateActionButtonView(Transform parent, string label, UnityAction onClick)
	{
		return CreateButtonView(actionButtonPrefab, parent, label.Replace(" ", string.Empty) + "Button", label, onClick);
	}

	protected TextButtonView CreateCompactButtonView(Transform parent, string label, UnityAction onClick)
	{
		return CreateButtonView(compactButtonPrefab, parent, label.Replace(" ", string.Empty) + "Button", label, onClick);
	}

	private TextButtonView CreateButtonView(TextButtonView prefab, Transform parent, string objectName, string label, UnityAction onClick)
	{
		if (prefab == null)
		{
			Debug.LogError($"[{GetType().Name}] Missing button prefab for {objectName}", this);
			return null;
		}

		TextButtonView instance = Instantiate(prefab, parent);
		instance.name = objectName;
		instance.Configure(label, onClick);
		return instance;
	}

	protected static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = new Vector2(left, bottom);
		rect.offsetMax = new Vector2(-right, -top);
		rect.pivot = new Vector2(0.5f, 0.5f);
	}

	protected static void SetTopStretch(RectTransform rect, float left, float right, float top)
	{
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.offsetMin = new Vector2(left, 0f);
		rect.offsetMax = new Vector2(-right, -top);
		rect.sizeDelta = new Vector2(0f, 0f);
	}

	protected abstract void LinkData();
	protected virtual void UpdateData() { }
}

public abstract class DetailContent<T> : DetailContentBase
	where T : Component
{
	public override System.Type TargetType => typeof(T);
	public override bool IsTargetType(GameObject obj) => obj.TryGetComponent<T>(out _);

	private void Update()
	{
		UpdateData();
	}
}
