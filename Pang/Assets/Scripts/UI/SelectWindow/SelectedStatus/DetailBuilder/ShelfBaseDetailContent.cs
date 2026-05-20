using System.Collections.Generic;
using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract class ShelfBaseDetailContent<TShelf> : DetailContent<TShelf>
	where TShelf : ShelfBase
{
	protected override bool UseDefaultTabs => false;

	private enum ShelfBaseTab
	{
		Info,
		Items,
		Action,
	}

	private UIWindow window;
	private RectTransform bodyRoot;
	private readonly List<GameObject> tabRoots = new();
	private readonly List<Button> actionButtons = new();
	private readonly List<GameObject> itemRows = new();

	private TextMeshProUGUI nameValue;
	private TextMeshProUGUI typeValue;
	private TextMeshProUGUI capacityValue;
	private TextMeshProUGUI currentSizeValue;
	private TextMeshProUGUI filledValue;
	private RectTransform itemsRoot;
	private RectTransform actionRoot;
	private bool uiBuilt;

	protected virtual string InfoTabLabel => "Info";
	protected virtual string ItemsTabLabel => "Items";
	protected virtual string ActionTabLabel => "Action";

	protected override void RemoveListeners()
	{
		ClearItemRows();
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
		SetTab((int)ShelfBaseTab.Info);
		RefreshAll();
	}

	protected override void UpdateData()
	{
		RefreshAll();
	}

	protected virtual void RefreshExtraInfo(IShelfBaseUIProvider shelfProvider)
	{
	}

	protected virtual void BuildActionTab()
	{
		foreach (Button actionButton in actionButtons)
		{
			if (actionButton != null)
				Destroy(actionButton.gameObject);
		}

		actionButtons.Clear();
		actionButtons.Add(CreateDeleteActionButton(actionRoot));
	}

	protected Button AddActionButton(string label, UnityEngine.Events.UnityAction onClick)
	{
		Button button = CreateRuntimeActionButton(actionRoot, label, onClick);
		actionButtons.Add(button);
		return button;
	}

	protected TextMeshProUGUI AddInfoLine(string label)
	{
		return CreateInfoLine(tabRoots[(int)ShelfBaseTab.Info].transform, label);
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

		bodyRoot = CreateRuntimeVerticalContainer("ShelfBaseDetailBody", transform, 6f);
		SetTopStretch(bodyRoot, 12f, 12f, 4f);

		GameObject infoTab = CreateRuntimeVerticalContainer("InfoTab", bodyRoot, 6f).gameObject;
		nameValue = CreateInfoLine(infoTab.transform, "Name");
		typeValue = CreateInfoLine(infoTab.transform, "Type");
		capacityValue = CreateInfoLine(infoTab.transform, "Capacity");
		currentSizeValue = CreateInfoLine(infoTab.transform, "Current Size");
		filledValue = CreateInfoLine(infoTab.transform, "Filled");
		tabRoots.Add(infoTab);

		GameObject itemsTab = CreateRuntimeVerticalContainer("ItemsTab", bodyRoot, 6f).gameObject;
		itemsRoot = CreateRuntimeVerticalContainer("ItemsRoot", itemsTab.transform, 6f);
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
		window.AddTab(InfoTabLabel, SetTab);
		window.AddTab(ItemsTabLabel, SetTab);
		window.AddTab(ActionTabLabel, SetTab);
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
		if (provider is not IShelfBaseUIProvider shelfProvider)
			return;

		nameValue.text = shelfProvider.Name;
		typeValue.text = shelfProvider.Subtitle;
		capacityValue.text = shelfProvider.CapacityDisplay;
		currentSizeValue.text = shelfProvider.CurrentSizeDisplay;
		filledValue.text = shelfProvider.FilledPercentDisplay;
		RefreshExtraInfo(shelfProvider);
		RefreshItems(shelfProvider);
	}

	private void RefreshItems(IShelfBaseUIProvider shelfProvider)
	{
		ClearItemRows();

		bool hasAny = false;
		foreach (ItemDisplayInfo itemInfo in shelfProvider.GetItemDisplayInfos())
		{
			hasAny = true;
			itemRows.Add(CreateItemRow(itemInfo));
		}

		if (hasAny == false)
		{
			TextMeshProUGUI emptyText = CreateRuntimeBodyText("EmptyItemsText", itemsRoot);
			emptyText.text = "No items stored.";
			itemRows.Add(emptyText.gameObject);
		}
	}

	private GameObject CreateItemRow(ItemDisplayInfo itemInfo)
	{
		RectTransform row = CreateRuntimeHorizontalContainer(itemInfo.ItemName + "Row", itemsRoot, 8f);

		TextMeshProUGUI nameText = CreateRuntimeBodyText("ItemName", row);
		nameText.text = itemInfo.ItemName;
		LayoutElement nameLayout = nameText.GetComponent<LayoutElement>();
		nameLayout.preferredWidth = 220f;
		nameLayout.flexibleWidth = 1f;

		TextMeshProUGUI quantityText = CreateRuntimeBodyText("ItemQuantity", row);
		quantityText.text = itemInfo.Quantity.ToString();
		quantityText.alignment = TextAlignmentOptions.TopRight;
		LayoutElement quantityLayout = quantityText.GetComponent<LayoutElement>();
		quantityLayout.preferredWidth = 80f;
		quantityLayout.flexibleWidth = 0f;

		return row.gameObject;
	}

	private void ClearItemRows()
	{
		foreach (GameObject itemRow in itemRows)
		{
			if (itemRow != null)
				Destroy(itemRow);
		}

		itemRows.Clear();
	}

	protected static RectTransform CreateRuntimeVerticalContainer(string name, Transform parent, float spacing)
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

	protected static RectTransform CreateRuntimeHorizontalContainer(string name, Transform parent, float spacing)
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

	protected static TextMeshProUGUI CreateRuntimeBodyText(string name, Transform parent)
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

	protected static TextMeshProUGUI CreateInfoLine(Transform parent, string label)
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
