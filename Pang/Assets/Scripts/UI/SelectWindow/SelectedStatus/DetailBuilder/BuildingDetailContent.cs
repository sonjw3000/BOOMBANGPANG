using System;
using System.Collections.Generic;
using System.Text;
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
	private readonly Dictionary<ZoneType, Toggle> zoneTypeToggles = new();

	private TextMeshProUGUI nameValue;
	private TextMeshProUGUI typeValue;
	private TextMeshProUGUI stateValue;
	private TextMeshProUGUI cellCountValue;
	private TextMeshProUGUI facilityCountValue;
	private TextMeshProUGUI cargoPortCountValue;
	private TextMeshProUGUI zoneCountValue;

	private TextMeshProUGUI facilitiesTodoText;
	private TextMeshProUGUI portsTodoText;
	private TextMeshProUGUI zoneStatusText;
	private TextMeshProUGUI zoneListText;
	private Button zoneCreateButton;
	private TextMeshProUGUI zoneCreateButtonText;

	private TextMeshProUGUI actionStateValue;
	private TextMeshProUGUI demolitionNoteText;
	private RectTransform actionRoot;

	private ZoneOverlayController zoneOverlayController;
	private bool listenersBound;
	private bool uiBuilt;
	private int currentTabIndex;
	private ZoneType selectedZoneType = ZoneType.Storage;

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

		if (zoneCreateButton != null)
			zoneCreateButton.onClick.RemoveListener(HandleZoneCreateButtonClicked);
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
		facilitiesTodoText = CreateRuntimeBodyText("FacilitiesTodoText", facilitiesTab.transform);
		facilitiesTodoText.text = "TODO: Building-owned facilities list.";
		tabRoots.Add(facilitiesTab);

		GameObject portsTab = CreateRuntimeVerticalContainer("PortsTab", bodyRoot, 6f).gameObject;
		portsTodoText = CreateRuntimeBodyText("PortsTodoText", portsTab.transform);
		portsTodoText.text = "TODO: Building-owned cargo ports list.";
		tabRoots.Add(portsTab);

		GameObject zonesTab = CreateRuntimeVerticalContainer("ZonesTab", bodyRoot, 8f).gameObject;
		zoneStatusText = CreateRuntimeBodyText("ZoneStatusText", zonesTab.transform);
		zoneStatusText.fontSize = 20f;
		zoneStatusText.text = "Select a zone type to create a building-owned zone.";

		zoneCreateButton = CreateRuntimeActionButton(zonesTab.transform, "Create Zone", HandleZoneCreateButtonClicked);
		zoneCreateButtonText = zoneCreateButton.GetComponentInChildren<TextMeshProUGUI>();

		TextMeshProUGUI zoneTypeHeader = CreateRuntimeBodyText("ZoneTypeHeader", zonesTab.transform);
		zoneTypeHeader.text = "Zone Type";
		zoneTypeHeader.fontStyle = FontStyles.Bold;

		GameObject toggleRoot = new("ZoneTypeToggleRoot", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ToggleGroup));
		toggleRoot.transform.SetParent(zonesTab.transform, false);

		VerticalLayoutGroup toggleLayout = toggleRoot.GetComponent<VerticalLayoutGroup>();
		toggleLayout.spacing = 6f;
		toggleLayout.childForceExpandHeight = false;
		toggleLayout.childForceExpandWidth = true;
		toggleLayout.childControlHeight = true;
		toggleLayout.childControlWidth = true;

		ToggleGroup toggleGroup = toggleRoot.GetComponent<ToggleGroup>();
		foreach (ZoneType zoneType in Enum.GetValues(typeof(ZoneType)))
		{
			Toggle toggle = CreateZoneTypeToggle(zoneType, toggleRoot.transform, toggleGroup);
			zoneTypeToggles[zoneType] = toggle;
			toggle.isOn = zoneType == selectedZoneType;
		}

		TextMeshProUGUI zoneListHeader = CreateRuntimeBodyText("ZoneListHeader", zonesTab.transform);
		zoneListHeader.text = "Zones In Building";
		zoneListHeader.fontStyle = FontStyles.Bold;

		zoneListText = CreateRuntimeBodyText("ZoneListText", zonesTab.transform);
		zoneListText.fontSize = 20f;
		zoneListText.textWrappingMode = TextWrappingModes.Normal;
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
		if (zoneCreateButton != null)
		{
			zoneCreateButton.onClick.RemoveListener(HandleZoneCreateButtonClicked);
			zoneCreateButton.onClick.AddListener(HandleZoneCreateButtonClicked);
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
		RefreshZoneSection();
	}

	private void RefreshZoneSection()
	{
		if (zoneStatusText == null || zoneListText == null || zoneCreateButton == null)
			return;

		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
		{
			zoneCreateButton.interactable = false;
			if (zoneCreateButtonText != null)
				zoneCreateButtonText.text = "Create Zone";
			zoneStatusText.text = "Building context is unavailable.";
			zoneListText.text = string.Empty;
			return;
		}

		Building building = buildingProvider.Target.Building;
		IReadOnlyList<ZoneArea> buildingZones = ZoneManager != null
			? ZoneManager.GetZonesForBuilding(building.RuntimeBuildingId)
			: Array.Empty<ZoneArea>();
		bool isZoneTab = currentTabIndex == (int)BuildingDetailTab.Zones;
		bool isCreating = Interaction != null
			&& Interaction.Mode == InteractionContext.InteractionMode.BuildingZoneEdit
			&& zoneOverlayController != null
			&& zoneOverlayController.CurrentBuilding == building;

		zoneCreateButton.interactable = isZoneTab && isCreating == false;
		if (zoneCreateButtonText != null)
			zoneCreateButtonText.text = isCreating ? "Creating..." : "Create Zone";

		if (isZoneTab == false)
		{
			zoneStatusText.text = "Open the Zones tab to inspect or create zones in this building.";
		}
		else if (isCreating)
		{
			zoneStatusText.text = "Left click start/end cells inside this building. Right click to cancel.";
		}
		else
		{
			zoneStatusText.text = "Select a zone type and create a zone inside the building interior.";
		}

		if (buildingZones.Count <= 0)
		{
			zoneListText.text = "No zones in this building yet.";
			return;
		}

		StringBuilder builder = new();
		for (int i = 0; i < buildingZones.Count; ++i)
		{
			ZoneArea zone = buildingZones[i];
			if (zone == null)
				continue;

			RectInt bounds = zone.Bounds;
			builder.Append(zone.DisplayName);
			builder.Append(" (");
			builder.Append(zone.Type);
			builder.Append(")  ");
			builder.Append(bounds.width);
			builder.Append("x");
			builder.Append(bounds.height);
			builder.Append(" @ ");
			builder.Append(bounds.xMin);
			builder.Append(", ");
			builder.Append(bounds.yMin);
			builder.AppendLine();
		}

		zoneListText.text = builder.ToString().TrimEnd();
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

	private void HandleZoneCreateButtonClicked()
	{
		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
			return;

		EnsureZoneOverlayController();
		zoneOverlayController?.BeginCreate(selectedZoneType, buildingProvider.Target.Building);
		RefreshZoneSection();
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

	private Toggle CreateZoneTypeToggle(ZoneType zoneType, Transform parent, ToggleGroup group)
	{
		GameObject root = new(zoneType.ToString(), typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
		root.transform.SetParent(parent, false);

		LayoutElement layout = root.GetComponent<LayoutElement>();
		layout.preferredHeight = 34f;

		Image background = root.GetComponent<Image>();
		background.color = new Color(0.22f, 0.22f, 0.22f, 0.95f);

		Toggle toggle = root.GetComponent<Toggle>();
		toggle.group = group;
		toggle.targetGraphic = background;

		TextMeshProUGUI label = CreateRuntimeBodyText("Label", root.transform);
		label.text = zoneType.ToString();
		label.alignment = TextAlignmentOptions.MidlineLeft;
		label.margin = new Vector4(12f, 0f, 0f, 0f);
		label.fontSize = 18f;

		toggle.onValueChanged.AddListener(isOn =>
		{
			background.color = isOn ? new Color(0.26f, 0.45f, 0.72f, 1f) : new Color(0.22f, 0.22f, 0.22f, 0.95f);
			if (isOn)
				selectedZoneType = zoneType;
		});

		return toggle;
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
