using System.Collections.Generic;
using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BoxPoolDetailContent : DetailContent<BoxPool>
{
	[SerializeField] private Button addBoxButton;
	protected override bool UseDefaultTabs => false;

	private enum BoxPoolTab
	{
		Info,
		Boxes,
		Action,
	}

	private UIWindow window;
	private RectTransform bodyRoot;
	private readonly List<GameObject> tabRoots = new();
	private readonly List<GameObject> boxRows = new();
	private readonly List<Button> actionButtons = new();

	private TextMeshProUGUI nameValue;
	private TextMeshProUGUI typeValue;
	private TextMeshProUGUI currentBoxesValue;
	private TextMeshProUGUI maxBoxesValue;
	private RectTransform boxesRoot;
	private RectTransform actionRoot;
	private bool uiBuilt;

	private static BoxPoolManager BoxPoolManager => GameContext.Instance.WMSys.BoxPoolManager;

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
		SetTab((int)BoxPoolTab.Info);
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

		bodyRoot = CreateRuntimeVerticalContainer("BoxPoolDetailBody", transform, 6f);
		SetTopStretch(bodyRoot, 12f, 12f, 4f);

		GameObject infoTab = CreateRuntimeVerticalContainer("InfoTab", bodyRoot, 6f).gameObject;
		nameValue = CreateInfoLine(infoTab.transform, "Name");
		typeValue = CreateInfoLine(infoTab.transform, "Type");
		currentBoxesValue = CreateInfoLine(infoTab.transform, "Current Boxes");
		maxBoxesValue = CreateInfoLine(infoTab.transform, "Max Boxes");
		tabRoots.Add(infoTab);

		GameObject boxesTab = CreateRuntimeVerticalContainer("BoxesTab", bodyRoot, 6f).gameObject;
		boxesRoot = CreateRuntimeVerticalContainer("BoxesRoot", boxesTab.transform, 6f);
		tabRoots.Add(boxesTab);

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
		window.AddTab("Boxes", SetTab);
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
		if (provider is not BoxPoolUIProvider prov)
			return;

		addBoxButton?.gameObject.SetActive(false);
		deleteButton?.gameObject.SetActive(false);

		nameValue.text = prov.Name;
		typeValue.text = prov.Subtitle;
		currentBoxesValue.text = prov.CurrentBoxCount.ToString();
		maxBoxesValue.text = prov.MaxBoxCount.ToString();
		RefreshBoxes(prov);
	}

	private void RefreshBoxes(BoxPoolUIProvider prov)
	{
		foreach (GameObject boxRow in boxRows)
		{
			if (boxRow != null)
				Destroy(boxRow);
		}

		boxRows.Clear();
		bool hasAny = false;
		foreach (string summary in prov.GetBoxSummaries())
		{
			hasAny = true;
			TextMeshProUGUI text = CreateRuntimeBodyText("BoxSummary", boxesRoot);
			text.text = summary;
			boxRows.Add(text.gameObject);
		}

		if (hasAny == false)
		{
			TextMeshProUGUI emptyText = CreateRuntimeBodyText("EmptyBoxes", boxesRoot);
			emptyText.text = "No boxes available.";
			boxRows.Add(emptyText.gameObject);
		}
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
		actionButtons.Add(CreateRuntimeActionButton(actionRoot, "Add Personal Box", () =>
		{
			BoxPoolManager.GiveNewBox(((BoxPoolUIProvider)provider).Target, BoxType.Personal);
		}));
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
		return valueText;
	}
}
