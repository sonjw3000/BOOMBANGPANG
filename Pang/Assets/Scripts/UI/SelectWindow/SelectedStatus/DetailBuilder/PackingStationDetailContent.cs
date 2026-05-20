using System.Collections.Generic;
using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PackingStationDetailContent : DetailContent<PackingStation>
{
	private const string BoxPanelPrefabPath = "UI/Select/DetailContents/ItemContainerPanelView";

	protected override bool UseDefaultTabs => false;

	private enum PackingStationTab
	{
		Info,
		Work,
		Items,
		Action,
	}

	private UIWindow window;
	private RectTransform bodyRoot;
	private readonly List<GameObject> tabRoots = new();
	private readonly List<Button> actionButtons = new();

	private TextMeshProUGUI nameValue;
	private TextMeshProUGUI typeValue;
	private TextMeshProUGUI currentWorkerValue;
	private TextMeshProUGUI incomingWorkerValue;
	private TextMeshProUGUI incomingStateValue;

	private TextMeshProUGUI workWorkerValue;
	private TextMeshProUGUI workStatusValue;
	private TextMeshProUGUI workStageValue;

	private ItemContainerPanelView waitingBoxPanel;
	private ItemContainerPanelView currentBoxPanel;
	private ItemContainerPanelView endBoxPanel;
	private RectTransform actionRoot;
	private bool uiBuilt;

	protected override void RemoveListeners()
	{
		foreach (Button actionButton in actionButtons)
		{
			if (actionButton != null)
				actionButton.onClick.RemoveAllListeners();
		}
	}

	protected override void LinkData()
	{
		EnsureUi();
		BuildActionTab();
		SetupTabs();
		SetTab((int)PackingStationTab.Info);
		RefreshAll();
	}

	protected override void UpdateData()
	{
		RefreshAll();
	}

	private void EnsureUi()
	{
		if (uiBuilt)
			return;

		HideLegacyVisuals();
		window = GetComponentInParent<UIWindow>(true);

		RectTransform selfRect = GetComponent<RectTransform>();
		if (selfRect != null)
		{
			selfRect.anchorMin = Vector2.zero;
			selfRect.anchorMax = Vector2.one;
			selfRect.offsetMin = Vector2.zero;
			selfRect.offsetMax = Vector2.zero;
		}

		bodyRoot = CreateRuntimeVerticalContainer("PackingStationDetailBody", transform, 6f);
		SetTopStretch(bodyRoot, 12f, 12f, 4f);

		GameObject infoTab = CreateRuntimeVerticalContainer("InfoTab", bodyRoot, 6f).gameObject;
		nameValue = CreateInfoLine(infoTab.transform, "Name");
		typeValue = CreateInfoLine(infoTab.transform, "Type");
		currentWorkerValue = CreateInfoLine(infoTab.transform, "Current Worker");
		incomingWorkerValue = CreateInfoLine(infoTab.transform, "Incoming Worker");
		incomingStateValue = CreateInfoLine(infoTab.transform, "Incoming Request");
		tabRoots.Add(infoTab);

		GameObject workTab = CreateRuntimeVerticalContainer("WorkTab", bodyRoot, 6f).gameObject;
		workWorkerValue = CreateInfoLine(workTab.transform, "Current Worker");
		workStatusValue = CreateInfoLine(workTab.transform, "Status");
		workStageValue = CreateInfoLine(workTab.transform, "Stage");
		tabRoots.Add(workTab);

		GameObject itemsTab = CreateRuntimeVerticalContainer("ItemsTab", bodyRoot, 10f).gameObject;
		CreateBoxSection(itemsTab.transform, "WaitingBox", out waitingBoxPanel);
		CreateBoxSection(itemsTab.transform, "CurrentBox", out currentBoxPanel);
		CreateBoxSection(itemsTab.transform, "EndBox", out endBoxPanel);
		tabRoots.Add(itemsTab);

		GameObject actionTab = CreateRuntimeVerticalContainer("ActionTab", bodyRoot, 6f).gameObject;
		actionRoot = CreateRuntimeVerticalContainer("ActionRoot", actionTab.transform, 6f);
		tabRoots.Add(actionTab);

		uiBuilt = true;
	}

	private void SetupTabs()
	{
		if (window == null)
			return;

		window.ClearTabs();
		window.AddTab("Info", SetTab);
		window.AddTab("Work", SetTab);
		window.AddTab("Items", SetTab);
		window.AddTab("Action", SetTab);
		window.UpdateTabVisuals(0);
	}

	private void SetTab(int tabIndex)
	{
		for (int i = 0; i < tabRoots.Count; i++)
		{
			tabRoots[i].SetActive(i == tabIndex);
		}

		window?.UpdateTabVisuals(tabIndex);
	}

	private void RefreshAll()
	{
		if (provider is not PackingStationUIProvider packingProvider)
			return;

		nameValue.text = packingProvider.Name;
		typeValue.text = packingProvider.Subtitle;
		currentWorkerValue.text = packingProvider.CurrentWorkerName;
		incomingWorkerValue.text = packingProvider.IncomingWorkerName;
		incomingStateValue.text = packingProvider.IncomingRequestDisplay;

		workWorkerValue.text = packingProvider.CurrentWorkerName;
		workStatusValue.text = packingProvider.WorkStatus;
		workStageValue.text = packingProvider.WorkStage;

		waitingBoxPanel.SetView(packingProvider.GetWaitingBoxDisplay());
		currentBoxPanel.SetView(packingProvider.GetCurrentBoxDisplay());
		endBoxPanel.SetView(packingProvider.GetEndBoxDisplay());
	}

	private void BuildActionTab()
	{
		foreach (Button actionButton in actionButtons)
		{
			if (actionButton != null)
				Destroy(actionButton.gameObject);
		}

		actionButtons.Clear();
		actionButtons.Add(CreateDeleteActionButton(actionRoot));
	}

	private static void CreateBoxSection(Transform parent, string label, out ItemContainerPanelView panelView)
	{
		TextMeshProUGUI labelText = CreateRuntimeBodyText(label + "Label", parent);
		labelText.text = label;
		labelText.fontStyle = FontStyles.Bold;

		ItemContainerPanelView prefab = Resources.Load<ItemContainerPanelView>(BoxPanelPrefabPath);
		if (prefab != null)
		{
			panelView = Instantiate(prefab, parent);
			return;
		}

		GameObject panelObject = new(label + "Panel", typeof(RectTransform), typeof(ItemContainerPanelView));
		panelObject.transform.SetParent(parent, false);
		panelView = panelObject.GetComponent<ItemContainerPanelView>();
	}

	private static RectTransform CreateRuntimeVerticalContainer(string name, Transform parent, float spacing)
	{
		GameObject root = new(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
		root.transform.SetParent(parent, false);

		VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
		layout.spacing = spacing;
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		LayoutElement layoutElement = root.GetComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.minWidth = 0f;

		ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

		return root.GetComponent<RectTransform>();
	}

	private static RectTransform CreateRuntimeHorizontalContainer(string name, Transform parent, float spacing)
	{
		GameObject root = new(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
		root.transform.SetParent(parent, false);

		HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
		layout.spacing = spacing;
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		LayoutElement layoutElement = root.GetComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.minWidth = 0f;

		ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

		return root.GetComponent<RectTransform>();
	}

	private static TextMeshProUGUI CreateRuntimeBodyText(string name, Transform parent)
	{
		GameObject textRoot = new(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
		textRoot.transform.SetParent(parent, false);

		TextMeshProUGUI text = textRoot.GetComponent<TextMeshProUGUI>();
		text.fontSize = 22f;
		text.color = Color.white;
		text.alignment = TextAlignmentOptions.TopLeft;
		text.textWrappingMode = TextWrappingModes.Normal;
		text.overflowMode = TextOverflowModes.Truncate;

		LayoutElement layout = textRoot.GetComponent<LayoutElement>();
		layout.flexibleWidth = 1f;
		layout.minWidth = 0f;

		return text;
	}

	private static TextMeshProUGUI CreateInfoLine(Transform parent, string label)
	{
		RectTransform row = CreateRuntimeHorizontalContainer(label + "Row", parent, 8f);

		TextMeshProUGUI labelText = CreateRuntimeBodyText(label + "Label", row);
		labelText.text = label + ":";
		labelText.fontStyle = FontStyles.Bold;
		labelText.textWrappingMode = TextWrappingModes.NoWrap;
		LayoutElement labelLayout = labelText.GetComponent<LayoutElement>();
		labelLayout.preferredWidth = 150f;
		labelLayout.flexibleWidth = 0f;

		TextMeshProUGUI valueText = CreateRuntimeBodyText(label + "Value", row);
		LayoutElement valueLayout = valueText.GetComponent<LayoutElement>();
		valueLayout.flexibleWidth = 1f;
		valueLayout.minWidth = 0f;
		return valueText;
	}
}
