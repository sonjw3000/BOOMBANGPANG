using System;
using System.Collections.Generic;
using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingControlWindow : MonoBehaviour
{
	private enum BuildingControlTab
	{
		Overview,
		Operations,
		Action,
	}

	[SerializeField] private UIWindow window;
	[SerializeField] private BuildingPlacementOverlayController overlayController;
	[SerializeField] private CargoPortLinkModeController cargoPortLinkModeController;
	[SerializeField] private string windowTitle = "Building Control";
	[SerializeField] private BuildingControlWindowContentView contentPrefab;
	[SerializeField] private BuildingControlBuildingRowView buildingRowPrefab;

	private static readonly BuildingType[] BuildingTypeOptions =
	{
		BuildingType.Staging,
		BuildingType.Storage,
		BuildingType.Packing,
		BuildingType.Launch,
	};

	private bool initialized;
	private int currentTabIndex;
	private readonly List<GameObject> tabRoots = new();
	private readonly List<GameObject> buildingRows = new();
	private readonly List<BuildingFootprintPreset> footprintPresetOptions = new();
	private SelectionUIMaster selectionUIMaster;
	private BuildingControlWindowContentView contentView;

	private TextMeshProUGUI overviewStatusText;
	private TextMeshProUGUI overviewSummaryText;
	private TextMeshProUGUI operationsStatusText;
	private RectTransform buildingListRoot;
	private TextMeshProUGUI buildingListEmptyText;
	private TextMeshProUGUI actionStatusText;
	private TextButtonView createButton;
	private TextButtonView linkCargoPortButton;
	private Dropdown buildingTypeDropdown;
	private Dropdown footprintPresetDropdown;

	private InteractionContext Interaction => GameContext.Instance.InteractionCtx;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private BuildingFootprintService BuildingFootprintService => GameContext.HasInstance ? GameContext.Instance.BuildingFootprintService : null;

	private void Awake()
	{
		EnsureInitialized();
	}

	private void Update()
	{
		if (initialized == false || gameObject.activeInHierarchy == false || window == null || window.IsOpen == false)
			return;

		if (Time.frameCount % 30 == 0)
			RefreshAll();
	}

	private void OnDestroy()
	{
		if (window != null)
		{
			window.Opened -= HandleWindowOpened;
			window.Closed -= HandleWindowClosed;
		}

		if (GameContext.HasInstance && GameContext.Instance.InteractionCtx != null)
			Interaction.OnBuildingPlacementChanged -= HandleBuildingPlacementChanged;

		buildingTypeDropdown?.onValueChanged.RemoveListener(HandleBuildingTypeChanged);
		footprintPresetDropdown?.onValueChanged.RemoveListener(HandleFootprintPresetChanged);
	}

	public void ToggleWindow()
	{
		EnsureInitialized();
		if (window == null)
			return;

		bool shouldOpen = gameObject.activeSelf == false || window.IsOpen == false;
		if (shouldOpen)
		{
			EnsureHostActive();
			window.Open();
		}
		else
		{
			window.Close();
		}
	}

	public void Open()
	{
		EnsureInitialized();
		EnsureHostActive();
		window?.Open();
	}

	public void Close()
	{
		EnsureInitialized();
		window?.Close();
	}

	private void EnsureInitialized()
	{
		if (initialized)
			return;

		window ??= GetComponent<UIWindow>();
		window ??= GetComponentInChildren<UIWindow>(true);
		overlayController ??= GetComponent<BuildingPlacementOverlayController>();
		cargoPortLinkModeController ??= GetComponent<CargoPortLinkModeController>();

		if (window == null)
			return;

		window.SetTitle(windowTitle);
		BuildContent();
		SetupTabs();
		window.Opened -= HandleWindowOpened;
		window.Closed -= HandleWindowClosed;
		window.Opened += HandleWindowOpened;
		window.Closed += HandleWindowClosed;

		if (GameContext.HasInstance && GameContext.Instance.InteractionCtx != null)
		{
			Interaction.OnBuildingPlacementChanged -= HandleBuildingPlacementChanged;
			Interaction.OnBuildingPlacementChanged += HandleBuildingPlacementChanged;
		}

		window.Close();
		RefreshAll();
		initialized = true;
	}

	private void EnsureHostActive()
	{
		if (gameObject.activeSelf == false)
			gameObject.SetActive(true);
	}

	private void BuildContent()
	{
		RectTransform contentRoot = window.ContentRoot;
		if (contentRoot == null)
			return;

		contentRoot.DetachChildren();
		tabRoots.Clear();

		if (contentPrefab == null)
		{
			Debug.LogError("[BuildingControlWindow] Content prefab is missing.", this);
			return;
		}

		contentView = Instantiate(contentPrefab, contentRoot);
		contentView.name = "BuildingControlContent";

		overviewStatusText = contentView.OverviewStatusText;
		overviewSummaryText = contentView.OverviewSummaryText;
		operationsStatusText = contentView.OperationsStatusText;
		buildingListRoot = contentView.BuildingListRoot;
		buildingListEmptyText = contentView.BuildingListEmptyText;
		actionStatusText = contentView.ActionStatusText;
		buildingTypeDropdown = contentView.BuildingTypeDropdown;
		footprintPresetDropdown = contentView.FootprintPresetDropdown;
		createButton = contentView.CreateButton;
		linkCargoPortButton = contentView.LinkCargoPortsButton;

		tabRoots.Add(contentView.OverviewTab);
		tabRoots.Add(contentView.OperationsTab);
		tabRoots.Add(contentView.ActionTab);

		if (createButton != null)
			createButton.Configure("Create Building", HandleCreateButtonClicked);

		if (linkCargoPortButton != null)
			linkCargoPortButton.Configure("Link Buildings", HandleLinkCargoPortsButtonClicked);

		ConfigureActionDropdowns();
	}

	private void SetupTabs()
	{
		if (window == null)
			return;

		window.ClearTabs();
		window.AddTab("Overview", SetTab);
		window.AddTab("Operations", SetTab);
		window.AddTab("Action", SetTab);
		window.UpdateTabVisuals(currentTabIndex);
	}

	private void SetTab(int tabIndex)
	{
		currentTabIndex = tabIndex;
		for (int i = 0; i < tabRoots.Count; ++i)
			tabRoots[i].SetActive(i == tabIndex);

		window?.UpdateTabVisuals(tabIndex);
		RefreshAll();
	}

	private void HandleWindowOpened()
	{
		Interaction.EnterBuildingSelectMode();
		overlayController?.SetOverlayVisible(true);
		RefreshAll();
	}

	private void HandleWindowClosed()
	{
		overlayController?.SetOverlayVisible(false);
		cargoPortLinkModeController?.EndLinkEdit();
		Interaction.ExitBuildingMode();
		RefreshAll();
	}

	private void HandleBuildingPlacementChanged(int floor)
	{
		RefreshAll();
	}

	private void HandleCreateButtonClicked()
	{
		if (BuildingFootprintService == null || BuildingFootprintService.ActivePreset == null)
			return;

		cargoPortLinkModeController?.EndLinkEdit();
		overlayController?.BeginCreate();
		RefreshAll();
	}

	private void HandleLinkCargoPortsButtonClicked()
	{
		if (cargoPortLinkModeController == null)
			return;

		if (cargoPortLinkModeController.IsEditing)
		{
			cargoPortLinkModeController.EndLinkEdit();
			RefreshAll();
			return;
		}

		Building activeBuilding = GetSelectedBuilding();
		cargoPortLinkModeController.BeginLinkEdit(activeBuilding);
		RefreshAll();
	}

	private void HandleBuildingTypeChanged(int optionIndex)
	{
		if (optionIndex < 0 || optionIndex >= BuildingTypeOptions.Length)
			return;

		overlayController?.SetSelectedBuildingType(BuildingTypeOptions[optionIndex]);
		RefreshAll();
	}

	private void HandleFootprintPresetChanged(int optionIndex)
	{
		if (optionIndex < 0 || optionIndex >= footprintPresetOptions.Count || BuildingFootprintService == null)
			return;

		BuildingFootprintService.SetActivePreset(footprintPresetOptions[optionIndex]);
		RefreshAll();
	}

	private void HandleCycleBuildingScopeClicked(Building building)
	{
		if (building == null || BuildingManager == null)
			return;

		BuildingManager.SetBuildingWorkScope(building, GetNextWorkScope(building.WorkScope));
		RefreshAll();
	}

	private void HandleOpenBuildingDetailsClicked(Building building)
	{
		if (building == null)
			return;

		EnsureSelectionUIMaster();
		BuildingSelectionProxy proxy = overlayController?.GetSelectionProxy(building);
		if (proxy == null)
			return;

		selectionUIMaster?.ShowDetailForObject(proxy.gameObject);
	}

	private void RefreshAll()
	{
		RefreshOverview();
		RefreshOperations();
		RefreshAction();
	}

	private void RefreshOverview()
	{
		if (overviewStatusText == null || overviewSummaryText == null)
			return;

		if (GameContext.HasInstance == false || GameContext.Instance.InteractionCtx == null)
		{
			overviewStatusText.text = "Interaction context is unavailable.";
			overviewSummaryText.text = "Building systems are not ready.";
			return;
		}

		int buildingCount = BuildingManager != null ? BuildingManager.RegisteredBuildings.Count : 0;
		bool isCreating = Interaction.Mode == InteractionContext.InteractionMode.BuildingPlacement;
		BuildingType selectedType = overlayController != null ? overlayController.SelectedBuildingType : BuildingType.Staging;
		BuildingFootprintPreset selectedPreset = BuildingFootprintService != null ? BuildingFootprintService.ActivePreset : null;

		overviewStatusText.text = isCreating
			? $"{BuildingTypeUtility.ToDisplayString(selectedType)} building placement is active. Left click a center cell, right click to cancel."
			: "Use this window to create buildings and manage each building's worker scope.";
		overviewSummaryText.text =
			$"Registered Buildings: {buildingCount}\n" +
			$"Selected Build Type: {BuildingTypeUtility.ToDisplayString(selectedType)}\n" +
			$"Selected Footprint: {(selectedPreset != null ? selectedPreset.DisplayName : "None")}";
	}

	private void RefreshOperations()
	{
		if (operationsStatusText == null || buildingListRoot == null || buildingListEmptyText == null)
			return;

		ClearBuildingRows();
		operationsStatusText.text = "Adjust building work scope and open a building detail window from the list below.";

		if (BuildingManager == null || BuildingManager.RegisteredBuildings.Count <= 0)
		{
			buildingListEmptyText.gameObject.SetActive(true);
			buildingListEmptyText.text = "No buildings created yet.";
			return;
		}

		buildingListEmptyText.gameObject.SetActive(false);
		for (int i = 0; i < BuildingManager.RegisteredBuildings.Count; ++i)
		{
			Building building = BuildingManager.RegisteredBuildings[i];
			if (building == null)
				continue;

			CreateBuildingRow(building);
		}
	}

	private void RefreshAction()
	{
		if (actionStatusText == null || createButton == null || linkCargoPortButton == null)
			return;

		if (GameContext.HasInstance == false || GameContext.Instance.InteractionCtx == null)
		{
			if (createButton.Button != null)
				createButton.Button.interactable = false;
			if (linkCargoPortButton.Button != null)
				linkCargoPortButton.Button.interactable = false;
			if (createButton.LabelText != null)
				createButton.LabelText.text = "Create Building";
			if (linkCargoPortButton.LabelText != null)
				linkCargoPortButton.LabelText.text = "Link Buildings";
			actionStatusText.text = "Interaction context is unavailable.";
			return;
		}

		bool isCreating = Interaction.Mode == InteractionContext.InteractionMode.BuildingPlacement;
		bool isLinkEditing = cargoPortLinkModeController != null && cargoPortLinkModeController.IsEditing;
		Building activeBuilding = GetSelectedBuilding();
		BuildingType selectedType = overlayController != null ? overlayController.SelectedBuildingType : BuildingType.Staging;
		BuildingFootprintPreset selectedPreset = BuildingFootprintService != null ? BuildingFootprintService.ActivePreset : null;

		if (buildingTypeDropdown != null)
		{
			int selectedIndex = Array.IndexOf(BuildingTypeOptions, selectedType);
			buildingTypeDropdown.SetValueWithoutNotify(Mathf.Max(0, selectedIndex));
			buildingTypeDropdown.interactable = isCreating == false && isLinkEditing == false;
		}

		if (footprintPresetDropdown != null)
		{
			int selectedIndex = footprintPresetOptions.IndexOf(selectedPreset);
			footprintPresetDropdown.SetValueWithoutNotify(Mathf.Max(0, selectedIndex));
			footprintPresetDropdown.interactable = isCreating == false && isLinkEditing == false && footprintPresetOptions.Count > 0;
		}

		if (createButton.Button != null)
			createButton.Button.interactable = isCreating == false && isLinkEditing == false && selectedPreset != null;
		if (createButton.LabelText != null)
			createButton.LabelText.text = isCreating ? "Creating..." : "Create Building";

		if (linkCargoPortButton.Button != null)
			linkCargoPortButton.Button.interactable = isCreating == false && (isLinkEditing || activeBuilding != null);
		if (linkCargoPortButton.LabelText != null)
			linkCargoPortButton.LabelText.text = isLinkEditing ? "Cancel Linking" : "Link Buildings";

		if (isCreating)
		{
			actionStatusText.text = selectedPreset != null
				? $"Click a center cell to place a diameter {selectedPreset.Diameter} {BuildingTypeUtility.ToDisplayString(selectedType)} building."
				: "No valid building footprint preset is selected.";
			return;
		}

		if (isLinkEditing)
		{
			actionStatusText.text = cargoPortLinkModeController.StatusText;
			return;
		}

		if (cargoPortLinkModeController != null && cargoPortLinkModeController.HasStatusMessage)
		{
			actionStatusText.text = cargoPortLinkModeController.StatusText;
			return;
		}

		string footprintName = selectedPreset != null ? selectedPreset.DisplayName : "None";
		actionStatusText.text = activeBuilding != null
			? $"Selected: {BuildingTypeUtility.ToDisplayString(selectedType)}, {footprintName}. Create a building or link this building to another building."
			: $"Selected: {BuildingTypeUtility.ToDisplayString(selectedType)}, {footprintName}. Start building creation, or select a building to link buildings.";
	}

	private void CreateBuildingRow(Building building)
	{
		if (buildingRowPrefab == null)
		{
			Debug.LogError("[BuildingControlWindow] Building row prefab is missing.", this);
			return;
		}

		BuildingControlBuildingRowView row = Instantiate(buildingRowPrefab, buildingListRoot);
		row.name = building.DisplayName + "Row";
		buildingRows.Add(row.gameObject);

		if (row.LabelText != null)
			row.LabelText.text = $"{building.DisplayName} ({BuildingTypeUtility.ToDisplayString(building.Type)})";

		row.ScopeButton?.Configure(BuildingWorkScopeUtility.ToDisplayString(building.WorkScope), () => HandleCycleBuildingScopeClicked(building));
		row.DetailsButton?.Configure("Details", () => HandleOpenBuildingDetailsClicked(building));
	}

	private void ClearBuildingRows()
	{
		for (int i = 0; i < buildingRows.Count; ++i)
		{
			GameObject row = buildingRows[i];
			if (row == null)
				continue;

			row.SetActive(false);
			Destroy(row);
		}

		buildingRows.Clear();
	}

	private void EnsureSelectionUIMaster()
	{
		if (selectionUIMaster == null)
			selectionUIMaster = FindFirstObjectByType<SelectionUIMaster>(FindObjectsInactive.Include);
	}

	private Building GetSelectedBuilding()
	{
		if (Interaction.SelectedObject == null
			|| Interaction.SelectedObject.TryGetComponent<BuildingSelectionProxy>(out BuildingSelectionProxy proxy) == false)
		{
			return null;
		}

		return proxy.Building;
	}

	private void ConfigureActionDropdowns()
	{
		if (buildingTypeDropdown != null)
		{
			buildingTypeDropdown.onValueChanged.RemoveListener(HandleBuildingTypeChanged);
			buildingTypeDropdown.ClearOptions();
			List<string> typeOptions = new();
			for (int i = 0; i < BuildingTypeOptions.Length; ++i)
				typeOptions.Add(BuildingTypeUtility.ToDisplayString(BuildingTypeOptions[i]));
			buildingTypeDropdown.AddOptions(typeOptions);
			buildingTypeDropdown.onValueChanged.AddListener(HandleBuildingTypeChanged);

			int selectedIndex = overlayController != null
				? Array.IndexOf(BuildingTypeOptions, overlayController.SelectedBuildingType)
				: Array.IndexOf(BuildingTypeOptions, BuildingType.Staging);
			buildingTypeDropdown.SetValueWithoutNotify(Mathf.Max(0, selectedIndex));
		}

		footprintPresetOptions.Clear();
		if (BuildingFootprintService != null)
		{
			IReadOnlyList<BuildingFootprintPreset> availablePresets = BuildingFootprintService.AvailablePresets;
			for (int i = 0; i < availablePresets.Count; ++i)
			{
				BuildingFootprintPreset preset = availablePresets[i];
				if (preset != null && preset.IsValid && footprintPresetOptions.Contains(preset) == false)
					footprintPresetOptions.Add(preset);
			}
		}

		if (footprintPresetDropdown == null)
			return;

		footprintPresetDropdown.onValueChanged.RemoveListener(HandleFootprintPresetChanged);
		footprintPresetDropdown.ClearOptions();
		List<string> footprintOptions = new();
		for (int i = 0; i < footprintPresetOptions.Count; ++i)
			footprintOptions.Add(footprintPresetOptions[i].DisplayName);
		footprintPresetDropdown.AddOptions(footprintOptions);
		footprintPresetDropdown.onValueChanged.AddListener(HandleFootprintPresetChanged);

		BuildingFootprintPreset activePreset = BuildingFootprintService != null ? BuildingFootprintService.ActivePreset : null;
		footprintPresetDropdown.SetValueWithoutNotify(Mathf.Max(0, footprintPresetOptions.IndexOf(activePreset)));
	}

	private static BuildingWorkScope GetNextWorkScope(BuildingWorkScope currentScope)
	{
		int enumCount = System.Enum.GetValues(typeof(BuildingWorkScope)).Length;
		int nextIndex = (((int)currentScope) + 1) % enumCount;
		return (BuildingWorkScope)nextIndex;
	}

}
