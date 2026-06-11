using System;
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
	private readonly List<GameObject> zoneListRows = new();
	private readonly List<GameObject> facilitySummaryRows = new();

	private TextMeshProUGUI nameValue;
	private TextMeshProUGUI typeValue;
	private TextMeshProUGUI stateValue;
	private TextMeshProUGUI cellCountValue;
	private TextMeshProUGUI facilityCountValue;
	private TextMeshProUGUI cargoPortCountValue;
	private TextMeshProUGUI zoneCountValue;

	private TextMeshProUGUI facilitiesSummaryText;
	private RectTransform facilitiesSummaryRoot;
	private TextMeshProUGUI portsTodoText;
	private TextMeshProUGUI zoneStatusText;
	private Button zoneOpenControlsButton;
	private RectTransform zoneListRoot;
	private TextMeshProUGUI zoneEmptyText;

	private TextMeshProUGUI actionStateValue;
	private TextMeshProUGUI demolitionNoteText;
	private RectTransform actionRoot;

	private ZoneOverlayController zoneOverlayController;
	private ZoneControlWindow zoneControlWindow;
	private SelectionUIMaster selectionUIMaster;
	private bool listenersBound;
	private bool uiBuilt;
	private int currentTabIndex;

	private ZoneManager ZoneManager => GameContext.HasInstance ? GameContext.Instance.ZoneMgr : null;
	private InteractionContext Interaction => GameContext.HasInstance ? GameContext.Instance.InteractionCtx : null;

	protected override void AddListener()
	{
		BindListeners();
	}

	protected override void RemoveListeners()
	{
		UnbindListeners();

		foreach (Button actionButton in actionButtons)
		{
			if (actionButton != null)
				actionButton.onClick.RemoveAllListeners();
		}

		if (zoneOpenControlsButton != null)
			zoneOpenControlsButton.onClick.RemoveListener(HandleZoneControlsButtonClicked);

		ClearZoneListRows();
		ClearFacilitySummaryRows();
	}

	protected override void LinkData()
	{
		EnsureUi();
		BindListeners();
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
		TextMeshProUGUI facilitiesHeaderText = CreateRuntimeBodyText("FacilitiesHeaderText", facilitiesTab.transform);
		facilitiesHeaderText.text = "Building Facility Summary";
		facilitiesHeaderText.fontStyle = FontStyles.Bold;

		facilitiesSummaryText = CreateRuntimeBodyText("FacilitiesSummaryText", facilitiesTab.transform);
		facilitiesSummaryText.fontSize = 20f;

		facilitiesSummaryRoot = CreateRuntimeVerticalContainer("FacilitiesSummaryRoot", facilitiesTab.transform, 6f);
		tabRoots.Add(facilitiesTab);

		GameObject portsTab = CreateRuntimeVerticalContainer("PortsTab", bodyRoot, 6f).gameObject;
		portsTodoText = CreateRuntimeBodyText("PortsTodoText", portsTab.transform);
		portsTodoText.text = "TODO: Building-owned cargo ports list.";
		tabRoots.Add(portsTab);

		GameObject zonesTab = CreateRuntimeVerticalContainer("ZonesTab", bodyRoot, 8f).gameObject;
		zoneStatusText = CreateRuntimeBodyText("ZoneStatusText", zonesTab.transform);
		zoneStatusText.fontSize = 20f;
		zoneStatusText.text = "Use Zone Controls to create and inspect building-owned zones.";

		zoneOpenControlsButton = CreateRuntimeActionButton(zonesTab.transform, "Open Zone Controls", HandleZoneControlsButtonClicked);

		TextMeshProUGUI zoneListHeader = CreateRuntimeBodyText("ZoneListHeader", zonesTab.transform);
		zoneListHeader.text = "Zones In Building";
		zoneListHeader.fontStyle = FontStyles.Bold;

		zoneListRoot = CreateRuntimeVerticalContainer("ZoneListRoot", zonesTab.transform, 6f);
		zoneEmptyText = CreateRuntimeBodyText("ZoneEmptyText", zoneListRoot);
		zoneEmptyText.fontSize = 20f;
		zoneEmptyText.text = "No zones in this building yet.";
		tabRoots.Add(zonesTab);

		GameObject actionTab = CreateRuntimeVerticalContainer("ActionTab", bodyRoot, 8f).gameObject;
		actionStateValue = CreateInfoLine(actionTab.transform, "Current State");
		demolitionNoteText = CreateRuntimeBodyText("DemolitionNoteText", actionTab.transform);
		demolitionNoteText.text = "Demolition flow is not wired yet. Use the actions below only to mark intent.";
		actionRoot = CreateRuntimeVerticalContainer("ActionRoot", actionTab.transform, 6f);
		tabRoots.Add(actionTab);

		uiBuilt = true;
	}

	private void BindListeners()
	{
		if (zoneOpenControlsButton != null)
		{
			zoneOpenControlsButton.onClick.RemoveListener(HandleZoneControlsButtonClicked);
			zoneOpenControlsButton.onClick.AddListener(HandleZoneControlsButtonClicked);
		}

		if (listenersBound)
			return;

		if (ZoneManager != null)
		{
			ZoneManager.OnZoneAdded -= HandleZoneChanged;
			ZoneManager.OnZoneChanged -= HandleZoneChanged;
			ZoneManager.OnZoneRemoved -= HandleZoneChanged;
			ZoneManager.OnZonesRebuilt -= HandleZonesRebuilt;
			ZoneManager.OnZoneAdded += HandleZoneChanged;
			ZoneManager.OnZoneChanged += HandleZoneChanged;
			ZoneManager.OnZoneRemoved += HandleZoneChanged;
			ZoneManager.OnZonesRebuilt += HandleZonesRebuilt;
		}

		if (Interaction != null)
		{
			Interaction.OnZonePlacementChanged -= HandleZonePlacementChanged;
			Interaction.OnZonePlacementChanged += HandleZonePlacementChanged;
		}

		listenersBound = true;
	}

	private void UnbindListeners()
	{
		if (listenersBound == false)
			return;

		if (ZoneManager != null)
		{
			ZoneManager.OnZoneAdded -= HandleZoneChanged;
			ZoneManager.OnZoneChanged -= HandleZoneChanged;
			ZoneManager.OnZoneRemoved -= HandleZoneChanged;
			ZoneManager.OnZonesRebuilt -= HandleZonesRebuilt;
		}

		if (Interaction != null)
			Interaction.OnZonePlacementChanged -= HandleZonePlacementChanged;

		listenersBound = false;
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
		window.UpdateTabVisuals(currentTabIndex);
	}

	private void SetTab(int tabIndex)
	{
		currentTabIndex = tabIndex;

		for (int i = 0; i < tabRoots.Count; i++)
			tabRoots[i].SetActive(i == tabIndex);

		window?.UpdateTabVisuals(tabIndex);
		RefreshFacilitiesSection();
		RefreshZoneSection();
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
		RefreshFacilitiesSection();
		RefreshZoneSection();
	}

	private void RefreshFacilitiesSection()
	{
		if (facilitiesSummaryText == null || facilitiesSummaryRoot == null)
			return;

		ClearFacilitySummaryRows();

		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
		{
			facilitiesSummaryText.text = "Building context is unavailable.";
			return;
		}

		Building building = buildingProvider.Target.Building;
		IReadOnlyList<IFacility> facilities = building.OccupiedFacilities;
		facilitiesSummaryText.text = $"Total Facilities: {facilities.Count}";

		if (facilities.Count <= 0)
		{
			AddFacilitySummaryRow("No facilities in this building yet.");
			return;
		}

		SortedDictionary<string, int> counts = new(StringComparer.Ordinal);
		for (int i = 0; i < facilities.Count; ++i)
		{
			IFacility facility = facilities[i];
			if (facility == null)
				continue;

			string key = facility.GetType().Name;
			if (counts.TryGetValue(key, out int currentCount) == false)
				currentCount = 0;

			counts[key] = currentCount + 1;
		}

		foreach (KeyValuePair<string, int> pair in counts)
			AddFacilitySummaryRow($"{pair.Key}: {pair.Value}");
	}

	private void RefreshZoneSection()
	{
		if (zoneStatusText == null || zoneOpenControlsButton == null || zoneListRoot == null || zoneEmptyText == null)
			return;

		EnsureZoneOverlayController();
		ClearZoneListRows();

		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
		{
			zoneOpenControlsButton.interactable = false;
			zoneStatusText.text = "Building context is unavailable.";
			zoneEmptyText.gameObject.SetActive(true);
			zoneEmptyText.text = "No zones available.";
			return;
		}

		Building building = buildingProvider.Target.Building;
		IReadOnlyList<ZoneArea> buildingZones = ZoneManager != null
			? ZoneManager.GetZonesForBuilding(building.RuntimeBuildingId)
			: Array.Empty<ZoneArea>();
		bool isCreating = Interaction != null
			&& Interaction.Mode == InteractionContext.InteractionMode.BuildingZoneEdit
			&& zoneOverlayController != null
			&& zoneOverlayController.CurrentBuilding == building;

		zoneOpenControlsButton.interactable = true;
		zoneStatusText.text = isCreating
			? "Zone creation is active in Zone Controls. Left click start/end cells inside this building. Right click to cancel."
			: "Use Zone Controls to create and manage zones for this building.";

		if (buildingZones.Count <= 0)
		{
			zoneEmptyText.gameObject.SetActive(true);
			zoneEmptyText.text = "No zones in this building yet.";
			return;
		}

		zoneEmptyText.gameObject.SetActive(false);
		for (int i = 0; i < buildingZones.Count; ++i)
		{
			ZoneArea zone = buildingZones[i];
			if (zone == null)
				continue;

			CreateZoneListRow(zone);
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

	private void HandleZoneControlsButtonClicked()
	{
		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
			return;

		EnsureZoneControlWindow();
		zoneControlWindow?.OpenForBuilding(buildingProvider.Target.Building);
		RefreshZoneSection();
	}

	private void HandleViewZoneDetailsClicked(ZoneArea zone)
	{
		if (zone == null)
			return;

		EnsureZoneOverlayController();
		EnsureSelectionUIMaster();

		ZoneSelectionProxy proxy = zoneOverlayController?.GetSelectionProxy(zone);
		if (proxy == null)
			return;

		selectionUIMaster?.SelectAndShowDetail(proxy.gameObject);
	}

	private void HandleZonePlacementChanged(ZoneType zoneType)
	{
		RefreshZoneSection();
	}

	private void HandleZoneChanged(ZoneArea zone)
	{
		if (zone == null)
			return;

		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
			return;

		if (zone.RuntimeBuildingId != buildingProvider.Target.Building.RuntimeBuildingId)
			return;

		RefreshAll();
	}

	private void HandleZonesRebuilt()
	{
		RefreshAll();
	}

	private void EnsureZoneOverlayController()
	{
		if (zoneOverlayController == null)
			zoneOverlayController = FindFirstObjectByType<ZoneOverlayController>(FindObjectsInactive.Include);
	}

	private void EnsureZoneControlWindow()
	{
		if (zoneControlWindow == null)
			zoneControlWindow = FindFirstObjectByType<ZoneControlWindow>(FindObjectsInactive.Include);
	}

	private void EnsureSelectionUIMaster()
	{
		if (selectionUIMaster == null)
			selectionUIMaster = GetComponentInParent<SelectionUIMaster>(true);

		if (selectionUIMaster == null)
			selectionUIMaster = FindFirstObjectByType<SelectionUIMaster>(FindObjectsInactive.Include);
	}

	private void CreateZoneListRow(ZoneArea zone)
	{
		RectTransform row = CreateRuntimeHorizontalContainer(zone.DisplayName + "Row", zoneListRoot, 8f);
		zoneListRows.Add(row.gameObject);

		TextMeshProUGUI label = CreateRuntimeBodyText(zone.DisplayName + "Label", row);
		label.fontSize = 20f;

		RectInt bounds = zone.Bounds;
		label.text = $"{zone.DisplayName} ({zone.Type})  {bounds.width}x{bounds.height} @ {bounds.xMin}, {bounds.yMin}";

		Button button = CreateCompactActionButton(row, "View Details", () => HandleViewZoneDetailsClicked(zone));
	}

	private void ClearZoneListRows()
	{
		for (int i = 0; i < zoneListRows.Count; ++i)
		{
			GameObject row = zoneListRows[i];
			if (row != null)
			{
				row.SetActive(false);
				Destroy(row);
			}
		}

		zoneListRows.Clear();
	}

	private void AddFacilitySummaryRow(string text)
	{
		TextMeshProUGUI rowText = CreateRuntimeBodyText("FacilitySummaryRow", facilitiesSummaryRoot);
		rowText.fontSize = 20f;
		rowText.text = text;
		facilitySummaryRows.Add(rowText.gameObject);
	}

	private void ClearFacilitySummaryRows()
	{
		for (int i = 0; i < facilitySummaryRows.Count; ++i)
		{
			GameObject row = facilitySummaryRows[i];
			if (row != null)
			{
				row.SetActive(false);
				Destroy(row);
			}
		}

		facilitySummaryRows.Clear();
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

	private static Button CreateCompactActionButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
	{
		GameObject buttonRoot = new(label.Replace(" ", string.Empty) + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
		buttonRoot.transform.SetParent(parent, false);

		LayoutElement layout = buttonRoot.GetComponent<LayoutElement>();
		layout.preferredHeight = 34f;
		layout.minHeight = 34f;
		layout.preferredWidth = 130f;
		layout.minWidth = 130f;
		layout.flexibleWidth = 0f;

		Image image = buttonRoot.GetComponent<Image>();
		image.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);

		Button button = buttonRoot.GetComponent<Button>();
		button.onClick.AddListener(onClick);

		GameObject textRoot = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
		textRoot.transform.SetParent(buttonRoot.transform, false);

		TextMeshProUGUI text = textRoot.GetComponent<TextMeshProUGUI>();
		text.text = label;
		text.fontSize = 18f;
		text.alignment = TextAlignmentOptions.Center;
		text.color = Color.white;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.overflowMode = TextOverflowModes.Ellipsis;

		RectTransform textRect = text.rectTransform;
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = Vector2.zero;
		textRect.offsetMax = Vector2.zero;

		return button;
	}

	private static void SetTopStretch(RectTransform rect, float left, float right, float top)
	{
		if (rect == null)
			return;

		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.offsetMin = new Vector2(left, 0f);
		rect.offsetMax = new Vector2(-right, -top);
	}
}
