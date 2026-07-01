using System;
using System.Collections.Generic;
using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class BuildingDetailContent : DetailContent<BuildingSelectionProxy>
{
	protected override bool UseDefaultTabs => false;

	private enum BuildingDetailTab
	{
		Overview,
		Facilities,
		Policy,
		Zones,
		Settings,
		Action,
	}

	[SerializeField] private BuildingDetailLayoutView layoutPrefab = null;
	[SerializeField] private DetailInfoRowView infoRowPrefab = null;
	[SerializeField] private TextRowView summaryRowPrefab = null;
	[SerializeField] private LabelButtonRowView zoneRowPrefab = null;

	private UIWindow window;
	private BuildingDetailLayoutView layoutView;
	private readonly List<GameObject> tabRoots = new();
	private readonly List<GameObject> zoneListRows = new();
	private readonly List<GameObject> facilitySummaryRows = new();

	private TextMeshProUGUI nameValue;
	private TextMeshProUGUI typeValue;
	private TextMeshProUGUI stateValue;
	private TextMeshProUGUI workScopeValue;
	private TextMeshProUGUI cellCountValue;
	private TextMeshProUGUI facilityCountValue;
	private TextMeshProUGUI cargoPortCountValue;
	private TextMeshProUGUI zoneCountValue;
	private TextMeshProUGUI thresholdValueText;
	private TextMeshProUGUI settingsStatusText;
	private TextMeshProUGUI actionStateValue;
	private Toggle overrideThresholdToggle;
	private Slider thresholdSlider;
	private GameObject settingsTabRoot;

	private ZoneOverlayController zoneOverlayController;
	private ZoneControlWindow zoneControlWindow;
	private SelectionUIMaster selectionUIMaster;
	private BuildingPlacementOverlayController buildingOverlayController;
	private bool listenersBound;
	private bool uiBuilt;
	private int currentTabIndex;

	private ZoneManager ZoneManager => GameContext.HasInstance ? GameContext.Instance.ZoneMgr : null;
	private InteractionContext Interaction => GameContext.HasInstance ? GameContext.Instance.InteractionCtx : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;

	protected override void AddListener()
	{
		BindListeners();
	}

	protected override void RemoveListeners()
	{
		UnbindListeners();
		ClearZoneListRows();
		ClearFacilitySummaryRows();
	}

	protected override void LinkData()
	{
		EnsureUi();
		BindListeners();
		SetupTabs();
		SetTab((int)BuildingDetailTab.Overview);
		RefreshAll();
	}

	protected override void UpdateData()
	{
		RefreshSummaryValues();
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

		if (layoutPrefab == null)
		{
			Debug.LogError("[BuildingDetailContent] Layout prefab is missing.", this);
			return;
		}

		layoutView = Instantiate(layoutPrefab, transform);
		layoutView.name = "BuildingDetailLayout";
		SetTopStretch(layoutView.GetComponent<RectTransform>(), 12f, 12f, 4f);

		tabRoots.Clear();
		tabRoots.Add(layoutView.OverviewTab);
		tabRoots.Add(layoutView.FacilitiesTab);
		tabRoots.Add(layoutView.PolicyTab);
		tabRoots.Add(layoutView.ZonesTab);
		settingsTabRoot = CreateSettingsTabRoot(layoutView.transform);
		tabRoots.Add(settingsTabRoot);
		tabRoots.Add(layoutView.ActionTab);

		nameValue = CreateInfoLine(layoutView.OverviewTab.transform, "Name");
		typeValue = CreateInfoLine(layoutView.OverviewTab.transform, "Type");
		stateValue = CreateInfoLine(layoutView.OverviewTab.transform, "State");
		workScopeValue = CreateInfoLine(layoutView.OverviewTab.transform, "Work Scope");
		cellCountValue = CreateInfoLine(layoutView.OverviewTab.transform, "Cells");
		facilityCountValue = CreateInfoLine(layoutView.OverviewTab.transform, "Facilities");
		cargoPortCountValue = CreateInfoLine(layoutView.OverviewTab.transform, "Cargo Ports");
		zoneCountValue = CreateInfoLine(layoutView.OverviewTab.transform, "Zones");
		actionStateValue = CreateInfoLine(layoutView.ActionTab.transform, "Current State");

		if (layoutView.FacilitiesSummaryText != null)
			layoutView.FacilitiesSummaryText.fontSize = 20f;

		if (layoutView.PolicyHelpText != null)
		{
			layoutView.PolicyHelpText.fontSize = 20f;
			layoutView.PolicyHelpText.text = "Change how far workers assigned to this building are allowed to operate.";
		}

		if (layoutView.ZoneStatusText != null)
		{
			layoutView.ZoneStatusText.fontSize = 20f;
			layoutView.ZoneStatusText.text = "Use Zone Controls to create and inspect building-owned zones.";
		}

		if (layoutView.ZoneEmptyText != null)
		{
			layoutView.ZoneEmptyText.fontSize = 20f;
			layoutView.ZoneEmptyText.text = "No zones in this building yet.";
		}

		if (layoutView.DemolitionNoteText != null)
			layoutView.DemolitionNoteText.text = "Demolition flow is not wired yet. Use the actions below only to mark intent.";

		BuildSettingsSection();
		layoutView.WorkScopeButton?.Configure("Cycle Work Scope", HandleWorkScopeButtonClicked);
		layoutView.ZoneOpenControlsButton?.Configure("Open Zone Controls", HandleZoneControlsButtonClicked);
		layoutView.PendingDemolitionButton?.Configure("Mark Pending Demolition", HandleMarkPendingDemolitionClicked);
		layoutView.RestoreActiveButton?.Configure("Restore Active State", HandleRestoreActiveClicked);
		uiBuilt = true;
	}

	private void BindListeners()
	{
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
		window.AddTab("Policy", SetTab);
		window.AddTab("Zones", SetTab);
		window.AddTab("Settings", SetTab);
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
		RefreshSettingsSection();
	}

	private void RefreshAll()
	{
		RefreshSummaryValues();
		RefreshFacilitiesSection();
		RefreshZoneSection();
		RefreshSettingsSection();
	}

	private void RefreshSummaryValues()
	{
		if (provider is not BuildingUIProvider buildingProvider)
			return;

		nameValue.text = buildingProvider.Name;
		typeValue.text = buildingProvider.Subtitle;
		stateValue.text = buildingProvider.StateDisplay;
		workScopeValue.text = buildingProvider.WorkScopeDisplay;
		cellCountValue.text = buildingProvider.CellCount.ToString();
		facilityCountValue.text = buildingProvider.FacilityCount.ToString();
		cargoPortCountValue.text = buildingProvider.CargoPortCount.ToString();
		zoneCountValue.text = buildingProvider.ZoneCount.ToString();
		actionStateValue.text = buildingProvider.StateDisplay;

		if (layoutView != null && layoutView.WorkScopeButton?.LabelText != null)
			layoutView.WorkScopeButton.LabelText.text = buildingProvider.WorkScopeDisplay;
		if (layoutView != null && layoutView.WorkScopeButton?.Button != null)
			layoutView.WorkScopeButton.Button.interactable = buildingProvider.Target?.Building != null;

		RefreshSettingsSection();
	}

	private void BuildSettingsSection()
	{
		if (settingsTabRoot == null)
			return;

		settingsStatusText = CreateInlineText("SettingsStatus", settingsTabRoot.transform, string.Empty, 20f, TextAlignmentOptions.Left);
		settingsStatusText.textWrappingMode = TextWrappingModes.Normal;

		GameObject overrideRow = new("OverrideThresholdRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
		overrideRow.transform.SetParent(settingsTabRoot.transform, false);

		HorizontalLayoutGroup overrideLayout = overrideRow.GetComponent<HorizontalLayoutGroup>();
		overrideLayout.spacing = 10f;
		overrideLayout.childAlignment = TextAnchor.MiddleLeft;
		overrideLayout.childControlWidth = false;
		overrideLayout.childControlHeight = true;
		overrideLayout.childForceExpandWidth = false;
		overrideLayout.childForceExpandHeight = false;

		LayoutElement overrideElement = overrideRow.GetComponent<LayoutElement>();
		overrideElement.preferredHeight = 34f;

		overrideThresholdToggle = CreateInlineToggle(overrideRow.transform);
		TextMeshProUGUI overrideLabel = CreateInlineText("Label", overrideRow.transform, "Override Threshold", 20f, TextAlignmentOptions.MidlineLeft);
		overrideLabel.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;

		GameObject thresholdRow = new("ThresholdRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
		thresholdRow.transform.SetParent(settingsTabRoot.transform, false);

		HorizontalLayoutGroup thresholdLayout = thresholdRow.GetComponent<HorizontalLayoutGroup>();
		thresholdLayout.spacing = 10f;
		thresholdLayout.childAlignment = TextAnchor.MiddleLeft;
		thresholdLayout.childControlWidth = false;
		thresholdLayout.childControlHeight = true;
		thresholdLayout.childForceExpandWidth = false;
		thresholdLayout.childForceExpandHeight = false;

		LayoutElement thresholdElement = thresholdRow.GetComponent<LayoutElement>();
		thresholdElement.preferredHeight = 36f;

		CreateInlineText("Label", thresholdRow.transform, "Threshold", 20f, TextAlignmentOptions.MidlineLeft)
			.gameObject.GetComponent<LayoutElement>().preferredWidth = 116f;
		thresholdSlider = CreateInlineSlider(thresholdRow.transform);
		thresholdValueText = CreateInlineText("Value", thresholdRow.transform, "80%", 20f, TextAlignmentOptions.MidlineRight);
		thresholdValueText.gameObject.GetComponent<LayoutElement>().preferredWidth = 64f;

		if (overrideThresholdToggle != null)
			overrideThresholdToggle.onValueChanged.AddListener(HandleOverrideThresholdChanged);
		if (thresholdSlider != null)
			thresholdSlider.onValueChanged.AddListener(HandleThresholdSliderChanged);
	}

	private void RefreshSettingsSection()
	{
		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
			return;

		Building building = buildingProvider.Target.Building;
		bool supportsCapsuleThreshold = SupportsCapsuleThreshold(building);
		if (settingsStatusText != null)
		{
			settingsStatusText.text = supportsCapsuleThreshold
				? "Outbound capsule threshold controls when OBStandby capsules become OB."
				: "This building does not use outbound capsule threshold.";
		}

		if (overrideThresholdToggle != null)
		{
			overrideThresholdToggle.SetIsOnWithoutNotify(building.OverrideCapsuleThreshold);
			overrideThresholdToggle.interactable = supportsCapsuleThreshold;
		}

		if (thresholdSlider != null)
		{
			thresholdSlider.SetValueWithoutNotify(building.CapsuleThresholdPercent);
			thresholdSlider.interactable = supportsCapsuleThreshold && building.OverrideCapsuleThreshold;
		}

		if (thresholdValueText != null)
			thresholdValueText.text = $"{Mathf.RoundToInt(building.CapsuleThresholdPercent)}%";
	}

	private void HandleOverrideThresholdChanged(bool isOn)
	{
		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
			return;

		buildingProvider.Target.Building.SetOverrideCapsuleThreshold(isOn);
		RefreshSettingsSection();
	}

	private void HandleThresholdSliderChanged(float value)
	{
		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
			return;

		buildingProvider.Target.Building.SetCapsuleThresholdPercent(value);
		RefreshSettingsSection();
	}

	private void RefreshFacilitiesSection()
	{
		if (layoutView == null || layoutView.FacilitiesSummaryText == null || layoutView.SummaryRoot == null)
			return;

		ClearFacilitySummaryRows();

		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
		{
			layoutView.FacilitiesSummaryText.text = "Building context is unavailable.";
			return;
		}

		Building building = buildingProvider.Target.Building;
		IReadOnlyList<IFacility> facilities = building.OccupiedFacilities;
		layoutView.FacilitiesSummaryText.text = $"Total Facilities: {facilities.Count}";

		if (facilities.Count <= 0)
		{
			AddFacilitySummaryRow("No facilities in this building yet.");
		}
		else
		{
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

		AddFacilitySectionHeader("InputBuilding");
		AddConnectedBuildingRows(building, isInputSection: true);
		AddFacilitySectionHeader("OutputBuilding");
		AddConnectedBuildingRows(building, isInputSection: false);
	}

	private void RefreshZoneSection()
	{
		if (layoutView == null || layoutView.ZoneStatusText == null || layoutView.ZoneOpenControlsButton == null || layoutView.ZoneListRoot == null || layoutView.ZoneEmptyText == null)
			return;

		EnsureZoneOverlayController();
		ClearZoneListRows();

		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
		{
			if (layoutView.ZoneOpenControlsButton.Button != null)
				layoutView.ZoneOpenControlsButton.Button.interactable = false;
			layoutView.ZoneStatusText.text = "Building context is unavailable.";
			layoutView.ZoneEmptyText.gameObject.SetActive(true);
			layoutView.ZoneEmptyText.text = "No zones available.";
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

		if (layoutView.ZoneOpenControlsButton.Button != null)
			layoutView.ZoneOpenControlsButton.Button.interactable = true;
		layoutView.ZoneStatusText.text = isCreating
			? "Zone creation is active in Zone Controls. Left click start/end cells inside this building. Right click to cancel."
			: "Use Zone Controls to create and manage zones for this building.";

		if (buildingZones.Count <= 0)
		{
			layoutView.ZoneEmptyText.gameObject.SetActive(true);
			layoutView.ZoneEmptyText.text = "No zones in this building yet.";
			return;
		}

		layoutView.ZoneEmptyText.gameObject.SetActive(false);
		for (int i = 0; i < buildingZones.Count; ++i)
		{
			ZoneArea zone = buildingZones[i];
			if (zone == null)
				continue;

			CreateZoneListRow(zone);
		}
	}

	private void HandleZoneControlsButtonClicked()
	{
		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
			return;

		EnsureZoneControlWindow();
		zoneControlWindow?.OpenForBuilding(buildingProvider.Target.Building);
		RefreshZoneSection();
	}

	private void HandleWorkScopeButtonClicked()
	{
		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
			return;

		Building building = buildingProvider.Target.Building;
		BuildingWorkScope nextScope = GetNextWorkScope(building.WorkScope);
		buildingProvider.Target.BuildingManager?.SetBuildingWorkScope(building, nextScope);
		RefreshSummaryValues();
	}

	private void HandleMarkPendingDemolitionClicked()
	{
		if (provider is BuildingUIProvider buildingProvider && buildingProvider.Target?.Building != null)
			buildingProvider.Target.BuildingManager?.SetBuildingState(buildingProvider.Target.Building, BuildingState.PendingDemolition);
	}

	private void HandleRestoreActiveClicked()
	{
		if (provider is BuildingUIProvider buildingProvider && buildingProvider.Target?.Building != null)
			buildingProvider.Target.BuildingManager?.SetBuildingState(buildingProvider.Target.Building, BuildingState.Active);
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

	private void EnsureBuildingOverlayController()
	{
		if (buildingOverlayController == null)
			buildingOverlayController = FindFirstObjectByType<BuildingPlacementOverlayController>(FindObjectsInactive.Include);
	}

	private void CreateZoneListRow(ZoneArea zone)
	{
		if (zoneRowPrefab == null || layoutView == null)
		{
			Debug.LogError("[BuildingDetailContent] Zone row prefab is missing.", this);
			return;
		}

		LabelButtonRowView row = Instantiate(zoneRowPrefab, layoutView.ZoneListRoot);
		row.name = zone.DisplayName + "Row";
		zoneListRows.Add(row.gameObject);

		RectInt bounds = zone.Bounds;
		if (row.LabelText != null)
		{
			row.LabelText.fontSize = 20f;
			row.LabelText.text = $"{zone.DisplayName} ({zone.Type})  {bounds.width}x{bounds.height} @ {bounds.xMin}, {bounds.yMin}";
		}

		row.ActionButton?.Configure("View Details", () => HandleViewZoneDetailsClicked(zone));
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
		if (summaryRowPrefab == null || layoutView == null)
		{
			Debug.LogError("[BuildingDetailContent] Summary row prefab is missing.", this);
			return;
		}

		TextRowView row = Instantiate(summaryRowPrefab, layoutView.SummaryRoot);
		row.name = "FacilitySummaryRow";
		if (row.Text != null)
		{
			row.Text.fontSize = 20f;
			row.Text.text = text;
		}

		facilitySummaryRows.Add(row.gameObject);
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

	private void AddFacilitySectionHeader(string text)
	{
		if (summaryRowPrefab == null || layoutView == null)
		{
			Debug.LogError("[BuildingDetailContent] Summary row prefab is missing.", this);
			return;
		}

		TextRowView row = Instantiate(summaryRowPrefab, layoutView.SummaryRoot);
		row.name = text.Replace(" ", string.Empty) + "Header";
		if (row.Text != null)
		{
			row.Text.fontSize = 21f;
			row.Text.fontStyle = FontStyles.Bold;
			row.Text.text = text;
		}

		facilitySummaryRows.Add(row.gameObject);
	}

	private void AddConnectedBuildingRows(Building currentBuilding, bool isInputSection)
	{
		if (currentBuilding == null)
			return;

		if (BuildingManager == null)
		{
			AddFacilitySummaryRow("Building manager is unavailable.");
			return;
		}

		List<Building> linkedBuildings = new();
		bool hasLinks = isInputSection
			? BuildingManager.TryGetInputBuildings(currentBuilding, linkedBuildings)
			: BuildingManager.TryGetOutputBuildings(currentBuilding, linkedBuildings);

		if (hasLinks == false)
		{
			AddFacilitySummaryRow("None");
			return;
		}

		for (int i = 0; i < linkedBuildings.Count; ++i)
		{
			Building linkedBuilding = linkedBuildings[i];
			if (linkedBuilding == null)
				continue;

			CreateConnectedBuildingRow(currentBuilding, linkedBuilding, isInputSection);
		}
	}

	private void CreateConnectedBuildingRow(Building currentBuilding, Building linkedBuilding, bool isInputSection)
	{
		if (layoutView == null || layoutView.SummaryRoot == null || linkedBuilding == null)
			return;

		GameObject rowObject = new($"{linkedBuilding.DisplayName}LinkRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
		rowObject.transform.SetParent(layoutView.SummaryRoot, false);
		facilitySummaryRows.Add(rowObject);

		HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
		rowLayout.spacing = 10f;
		rowLayout.childAlignment = TextAnchor.MiddleLeft;
		rowLayout.childControlWidth = false;
		rowLayout.childControlHeight = true;
		rowLayout.childForceExpandWidth = false;
		rowLayout.childForceExpandHeight = false;

		LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
		rowElement.preferredHeight = 34f;

		TextMeshProUGUI label = CreateInlineText("Label", rowObject.transform, linkedBuilding.DisplayName, 20f, TextAlignmentOptions.MidlineLeft);
		LayoutElement labelLayout = label.gameObject.GetComponent<LayoutElement>();
		labelLayout.flexibleWidth = 1f;
		labelLayout.minWidth = 200f;

		CreateInlineButton(rowObject.transform, "Details", () => HandleOpenLinkedBuildingDetailsClicked(linkedBuilding));
		CreateInlineButton(rowObject.transform, "Disconnect", () => HandleDisconnectBuildingLinkClicked(currentBuilding, linkedBuilding, isInputSection));
	}

	private void HandleOpenLinkedBuildingDetailsClicked(Building building)
	{
		if (building == null)
			return;

		EnsureBuildingOverlayController();
		EnsureSelectionUIMaster();
		BuildingSelectionProxy proxy = buildingOverlayController?.GetSelectionProxy(building);
		if (proxy == null)
			return;

		selectionUIMaster?.SelectAndShowDetail(proxy.gameObject);
	}

	private void HandleDisconnectBuildingLinkClicked(Building currentBuilding, Building linkedBuilding, bool isInputSection)
	{
		if (currentBuilding == null || linkedBuilding == null || BuildingManager == null)
			return;

		if (isInputSection)
			BuildingManager.TryUnlinkBuildings(linkedBuilding, currentBuilding);
		else
			BuildingManager.TryUnlinkBuildings(currentBuilding, linkedBuilding);

		RefreshAll();
	}

	private TextMeshProUGUI CreateInfoLine(Transform parent, string label)
	{
		if (infoRowPrefab == null)
		{
			Debug.LogError("[BuildingDetailContent] Info row prefab is missing.", this);
			return null;
		}

		DetailInfoRowView row = Instantiate(infoRowPrefab, parent);
		row.name = label.Replace(" ", string.Empty) + "Row";
		row.SetLabel(label + ":");
		if (row.LabelText != null)
		{
			row.LabelText.fontStyle = FontStyles.Bold;
			row.LabelText.textWrappingMode = TextWrappingModes.NoWrap;
		}

		return row.ValueText;
	}

	private static BuildingWorkScope GetNextWorkScope(BuildingWorkScope currentScope)
	{
		int enumCount = Enum.GetValues(typeof(BuildingWorkScope)).Length;
		int nextIndex = (((int)currentScope) + 1) % enumCount;
		return (BuildingWorkScope)nextIndex;
	}

	private static bool SupportsCapsuleThreshold(Building building)
	{
		return building != null && (building.Type == BuildingType.Storage || building.Type == BuildingType.Packing);
	}

	private static GameObject CreateSettingsTabRoot(Transform parent)
	{
		GameObject root = new("SettingsTab", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
		root.transform.SetParent(parent, false);

		VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
		layout.spacing = 8f;
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		LayoutElement element = root.GetComponent<LayoutElement>();
		element.flexibleWidth = 1f;

		RectTransform rect = root.GetComponent<RectTransform>();
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		return root;
	}

	private static Toggle CreateInlineToggle(Transform parent)
	{
		GameObject toggleObject = new("Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
		toggleObject.transform.SetParent(parent, false);

		Image image = toggleObject.GetComponent<Image>();
		image.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

		LayoutElement layout = toggleObject.GetComponent<LayoutElement>();
		layout.preferredWidth = 28f;
		layout.preferredHeight = 28f;

		Toggle toggle = toggleObject.GetComponent<Toggle>();
		toggle.targetGraphic = image;

		GameObject checkmarkObject = new("Checkmark", typeof(RectTransform), typeof(Image));
		checkmarkObject.transform.SetParent(toggleObject.transform, false);
		Image checkmark = checkmarkObject.GetComponent<Image>();
		checkmark.color = new Color(0.35f, 0.82f, 0.48f, 1f);

		RectTransform checkmarkRect = checkmarkObject.GetComponent<RectTransform>();
		checkmarkRect.anchorMin = new Vector2(0.22f, 0.22f);
		checkmarkRect.anchorMax = new Vector2(0.78f, 0.78f);
		checkmarkRect.offsetMin = Vector2.zero;
		checkmarkRect.offsetMax = Vector2.zero;

		toggle.graphic = checkmark;
		return toggle;
	}

	private static Slider CreateInlineSlider(Transform parent)
	{
		GameObject sliderObject = new("ThresholdSlider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
		sliderObject.transform.SetParent(parent, false);

		LayoutElement layout = sliderObject.GetComponent<LayoutElement>();
		layout.flexibleWidth = 1f;
		layout.preferredHeight = 30f;

		GameObject backgroundObject = new("Background", typeof(RectTransform), typeof(Image));
		backgroundObject.transform.SetParent(sliderObject.transform, false);
		Image background = backgroundObject.GetComponent<Image>();
		background.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

		RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
		backgroundRect.anchorMin = new Vector2(0f, 0.35f);
		backgroundRect.anchorMax = new Vector2(1f, 0.65f);
		backgroundRect.offsetMin = Vector2.zero;
		backgroundRect.offsetMax = Vector2.zero;

		GameObject fillAreaObject = new("Fill Area", typeof(RectTransform));
		fillAreaObject.transform.SetParent(sliderObject.transform, false);
		RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
		fillAreaRect.anchorMin = new Vector2(0f, 0.35f);
		fillAreaRect.anchorMax = new Vector2(1f, 0.65f);
		fillAreaRect.offsetMin = Vector2.zero;
		fillAreaRect.offsetMax = Vector2.zero;

		GameObject fillObject = new("Fill", typeof(RectTransform), typeof(Image));
		fillObject.transform.SetParent(fillAreaObject.transform, false);
		Image fill = fillObject.GetComponent<Image>();
		fill.color = new Color(0.35f, 0.82f, 0.48f, 1f);

		RectTransform fillRect = fillObject.GetComponent<RectTransform>();
		fillRect.anchorMin = Vector2.zero;
		fillRect.anchorMax = Vector2.one;
		fillRect.offsetMin = Vector2.zero;
		fillRect.offsetMax = Vector2.zero;

		GameObject handleAreaObject = new("Handle Slide Area", typeof(RectTransform));
		handleAreaObject.transform.SetParent(sliderObject.transform, false);
		RectTransform handleAreaRect = handleAreaObject.GetComponent<RectTransform>();
		handleAreaRect.anchorMin = Vector2.zero;
		handleAreaRect.anchorMax = Vector2.one;
		handleAreaRect.offsetMin = new Vector2(10f, 0f);
		handleAreaRect.offsetMax = new Vector2(-10f, 0f);

		GameObject handleObject = new("Handle", typeof(RectTransform), typeof(Image));
		handleObject.transform.SetParent(handleAreaObject.transform, false);
		Image handle = handleObject.GetComponent<Image>();
		handle.color = Color.white;

		RectTransform handleRect = handleObject.GetComponent<RectTransform>();
		handleRect.sizeDelta = new Vector2(18f, 26f);

		Slider slider = sliderObject.GetComponent<Slider>();
		slider.minValue = 0f;
		slider.maxValue = 100f;
		slider.wholeNumbers = true;
		slider.targetGraphic = handle;
		slider.fillRect = fillRect;
		slider.handleRect = handleRect;
		slider.direction = Slider.Direction.LeftToRight;
		return slider;
	}

	private static TextMeshProUGUI CreateInlineText(string objectName, Transform parent, string value, float fontSize, TextAlignmentOptions alignment)
	{
		GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
		textObject.transform.SetParent(parent, false);

		LayoutElement layout = textObject.GetComponent<LayoutElement>();
		layout.preferredHeight = 28f;

		TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
		text.text = value;
		text.fontSize = fontSize;
		text.color = Color.white;
		text.alignment = alignment;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.overflowMode = TextOverflowModes.Ellipsis;

		RectTransform rect = text.rectTransform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;

		return text;
	}

	private static Button CreateInlineButton(Transform parent, string label, UnityAction onClick)
	{
		GameObject buttonObject = new($"{label}Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
		buttonObject.transform.SetParent(parent, false);

		Image image = buttonObject.GetComponent<Image>();
		image.color = new Color(0.19f, 0.19f, 0.19f, 0.96f);

		LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
		layout.preferredWidth = label == "Disconnect" ? 120f : 96f;
		layout.preferredHeight = 30f;

		Button button = buttonObject.GetComponent<Button>();
		button.targetGraphic = image;
		button.onClick.RemoveAllListeners();
		if (onClick != null)
			button.onClick.AddListener(onClick);

		TextMeshProUGUI text = CreateInlineText("Label", buttonObject.transform, label, 18f, TextAlignmentOptions.Center);
		text.margin = new Vector4(6f, 2f, 6f, 2f);
		return button;
	}
}
