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
		Policy,
		Zones,
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
	private TextMeshProUGUI actionStateValue;

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
		RefreshSummaryValues();
		RefreshFacilitiesSection();
		RefreshZoneSection();
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
}
