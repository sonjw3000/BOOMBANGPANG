using System.Collections.Generic;
using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BuildingDetailContent : DetailContent<BuildingSelectionProxy>
{
	protected override bool UseDefaultTabs => false;

	private enum BuildingDetailTab
	{
		Overview,
		Facilities,
		Ports,
		Zones,
		Action,
	}

	private UIWindow window;
	private RectTransform bodyRoot;
	private readonly List<GameObject> tabRoots = new();
	private readonly List<Button> actionButtons = new();

	private TextMeshProUGUI nameValue;
	private TextMeshProUGUI typeValue;
	private TextMeshProUGUI stateValue;
	private TextMeshProUGUI cellCountValue;
	private TextMeshProUGUI facilityCountValue;
	private TextMeshProUGUI cargoPortCountValue;
	private TextMeshProUGUI zoneCountValue;

	private TextMeshProUGUI facilitiesTodoText;
	private TextMeshProUGUI portsTodoText;
	private TextMeshProUGUI zonesTodoText;

	private TextMeshProUGUI actionStateValue;
	private TextMeshProUGUI demolitionNoteText;
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
		SetTab((int)BuildingDetailTab.Overview);
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

		bodyRoot = CreateRuntimeVerticalContainer("BuildingDetailBody", transform, 6f);
		SetTopStretch(bodyRoot, 12f, 12f, 4f);

		GameObject overviewTab = CreateRuntimeVerticalContainer("OverviewTab", bodyRoot, 6f).gameObject;
		nameValue = CreateInfoLine(overviewTab.transform, "Name");
		typeValue = CreateInfoLine(overviewTab.transform, "Type");
		stateValue = CreateInfoLine(overviewTab.transform, "State");
		cellCountValue = CreateInfoLine(overviewTab.transform, "Cells");
		facilityCountValue = CreateInfoLine(overviewTab.transform, "Facilities");
		cargoPortCountValue = CreateInfoLine(overviewTab.transform, "Cargo Ports");
		zoneCountValue = CreateInfoLine(overviewTab.transform, "Zones");
		tabRoots.Add(overviewTab);

		GameObject facilitiesTab = CreateRuntimeVerticalContainer("FacilitiesTab", bodyRoot, 6f).gameObject;
		facilitiesTodoText = CreateRuntimeBodyText("FacilitiesTodoText", facilitiesTab.transform);
		facilitiesTodoText.text = "TODO: Building-owned facilities list.";
		tabRoots.Add(facilitiesTab);

		GameObject portsTab = CreateRuntimeVerticalContainer("PortsTab", bodyRoot, 6f).gameObject;
		portsTodoText = CreateRuntimeBodyText("PortsTodoText", portsTab.transform);
		portsTodoText.text = "TODO: Building-owned cargo ports list.";
		tabRoots.Add(portsTab);

		GameObject zonesTab = CreateRuntimeVerticalContainer("ZonesTab", bodyRoot, 6f).gameObject;
		zonesTodoText = CreateRuntimeBodyText("ZonesTodoText", zonesTab.transform);
		zonesTodoText.text = "TODO: Building-owned zones list.";
		tabRoots.Add(zonesTab);

		GameObject actionTab = CreateRuntimeVerticalContainer("ActionTab", bodyRoot, 8f).gameObject;
		actionStateValue = CreateInfoLine(actionTab.transform, "Current State");
		demolitionNoteText = CreateRuntimeBodyText("DemolitionNoteText", actionTab.transform);
		demolitionNoteText.text = "Demolition flow is not wired yet. Use the actions below only to mark intent.";
		actionRoot = CreateRuntimeVerticalContainer("ActionRoot", actionTab.transform, 6f);
		tabRoots.Add(actionTab);

		uiBuilt = true;
	}

	private void SetupTabs()
	{
		if (window == null)
			return;

		window.ClearTabs();
		window.AddTab("Overview", SetTab);
		window.AddTab("Facilities", SetTab);
		window.AddTab("Ports", SetTab);
		window.AddTab("Zones", SetTab);
		window.AddTab("Action", SetTab);
		window.UpdateTabVisuals(0);
	}

	private void SetTab(int tabIndex)
	{
		for (int i = 0; i < tabRoots.Count; i++)
			tabRoots[i].SetActive(i == tabIndex);

		window?.UpdateTabVisuals(tabIndex);
	}

	private void RefreshAll()
	{
		if (provider is not BuildingUIProvider buildingProvider)
			return;

		nameValue.text = buildingProvider.Name;
		typeValue.text = buildingProvider.Subtitle;
		stateValue.text = buildingProvider.StateDisplay;
		cellCountValue.text = buildingProvider.CellCount.ToString();
		facilityCountValue.text = buildingProvider.FacilityCount.ToString();
		cargoPortCountValue.text = buildingProvider.CargoPortCount.ToString();
		zoneCountValue.text = buildingProvider.ZoneCount.ToString();
		actionStateValue.text = buildingProvider.StateDisplay;
	}

	private void BuildActionTab()
	{
		foreach (Button actionButton in actionButtons)
		{
			if (actionButton != null)
				Destroy(actionButton.gameObject);
		}

		actionButtons.Clear();

		Button markPendingButton = CreateRuntimeActionButton(actionRoot, "Mark Pending Demolition", () =>
		{
			if (provider is BuildingUIProvider buildingProvider && buildingProvider.Target?.Building != null)
				buildingProvider.Target.BuildingManager?.SetBuildingState(buildingProvider.Target.Building, BuildingState.PendingDemolition);
		});
		actionButtons.Add(markPendingButton);

		Button restoreActiveButton = CreateRuntimeActionButton(actionRoot, "Restore Active State", () =>
		{
			if (provider is BuildingUIProvider buildingProvider && buildingProvider.Target?.Building != null)
				buildingProvider.Target.BuildingManager?.SetBuildingState(buildingProvider.Target.Building, BuildingState.Active);
		});
		actionButtons.Add(restoreActiveButton);
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
		labelLayout.preferredWidth = 170f;
		labelLayout.flexibleWidth = 0f;

		TextMeshProUGUI valueText = CreateRuntimeBodyText(label + "Value", row);
		return valueText;
	}
}
