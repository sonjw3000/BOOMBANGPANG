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
	[SerializeField] private ZoneOverlayController zoneOverlayController;
	[SerializeField] private CargoPortLinkModeController cargoPortLinkModeController;
	[SerializeField] private string windowTitle = "Building Control";

	private bool initialized;
	private int currentTabIndex;
	private RectTransform bodyRoot;
	private readonly List<GameObject> tabRoots = new();
	private readonly List<GameObject> buildingRows = new();
	private SelectionUIMaster selectionUIMaster;

	private TMP_Text overviewStatusText;
	private TMP_Text overviewSummaryText;
	private TMP_Text operationsStatusText;
	private RectTransform buildingListRoot;
	private TMP_Text buildingListEmptyText;
	private TMP_Text actionStatusText;
	private Button createButton;
	private TMP_Text createButtonText;
	private Button linkCargoPortButton;
	private TMP_Text linkCargoPortButtonText;

	private InteractionContext Interaction => GameContext.Instance.InteractionCtx;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;

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
		if (overlayController == null)
			overlayController = gameObject.AddComponent<BuildingPlacementOverlayController>();
		cargoPortLinkModeController ??= GetComponent<CargoPortLinkModeController>();
		if (cargoPortLinkModeController == null)
			cargoPortLinkModeController = gameObject.AddComponent<CargoPortLinkModeController>();
		zoneOverlayController ??= FindFirstObjectByType<ZoneOverlayController>(FindObjectsInactive.Include);

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

		bodyRoot = CreateVerticalContainer("BuildingControlBody", contentRoot, 8f);

		GameObject overviewTab = CreateVerticalContainer("OverviewTab", bodyRoot, 8f).gameObject;
		overviewStatusText = CreateText("OverviewStatusText", overviewTab.transform, 20f);
		overviewSummaryText = CreateText("OverviewSummaryText", overviewTab.transform, 20f);
		tabRoots.Add(overviewTab);

		GameObject operationsTab = CreateVerticalContainer("OperationsTab", bodyRoot, 8f).gameObject;
		operationsStatusText = CreateText("OperationsStatusText", operationsTab.transform, 20f);
		buildingListRoot = CreateVerticalContainer("BuildingListRoot", operationsTab.transform, 6f);
		buildingListEmptyText = CreateText("BuildingListEmptyText", buildingListRoot, 20f);
		buildingListEmptyText.text = "No buildings created yet.";
		tabRoots.Add(operationsTab);

		GameObject actionTab = CreateVerticalContainer("ActionTab", bodyRoot, 8f).gameObject;
		actionStatusText = CreateText("ActionStatusText", actionTab.transform, 20f);
		createButton = CreateButton("CreateButton", actionTab.transform, "Create Building", HandleCreateButtonClicked, out createButtonText);
		linkCargoPortButton = CreateButton("LinkCargoPortsButton", actionTab.transform, "Link Cargo Ports", HandleLinkCargoPortsButtonClicked, out linkCargoPortButtonText);
		tabRoots.Add(actionTab);
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
		overlayController?.SetOverlayVisible(false);
		zoneOverlayController?.SetBuildingModeActive(true);
		RefreshAll();
	}

	private void HandleWindowClosed()
	{
		overlayController?.SetOverlayVisible(false);
		cargoPortLinkModeController?.EndLinkEdit();
		zoneOverlayController?.SetBuildingModeActive(false);
		Interaction.ExitBuildingMode();
		RefreshAll();
	}

	private void HandleBuildingPlacementChanged(int floor)
	{
		RefreshAll();
	}

	private void HandleCreateButtonClicked()
	{
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

		EnsureZoneOverlayController();
		Building activeBuilding = zoneOverlayController != null ? zoneOverlayController.CurrentBuilding : null;
		cargoPortLinkModeController.BeginLinkEdit(activeBuilding);
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
		overlayController ??= FindFirstObjectByType<BuildingPlacementOverlayController>(FindObjectsInactive.Include);
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

		overviewStatusText.text = isCreating
			? "Building placement is active. Left click start/end cells, right click to cancel."
			: "Use this window to create buildings and manage each building's worker scope.";
		overviewSummaryText.text = $"Registered Buildings: {buildingCount}";
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
			createButton.interactable = false;
			linkCargoPortButton.interactable = false;
			if (createButtonText != null)
				createButtonText.text = "Create Building";
			if (linkCargoPortButtonText != null)
				linkCargoPortButtonText.text = "Link Cargo Ports";
			actionStatusText.text = "Interaction context is unavailable.";
			return;
		}

		bool isCreating = Interaction.Mode == InteractionContext.InteractionMode.BuildingPlacement;
		bool isLinkEditing = cargoPortLinkModeController != null && cargoPortLinkModeController.IsEditing;
		EnsureZoneOverlayController();
		Building activeBuilding = zoneOverlayController != null ? zoneOverlayController.CurrentBuilding : null;

		createButton.interactable = isCreating == false && isLinkEditing == false;
		if (createButtonText != null)
			createButtonText.text = isCreating ? "Creating..." : "Create Building";

		linkCargoPortButton.interactable = isCreating == false && (isLinkEditing || activeBuilding != null);
		if (linkCargoPortButtonText != null)
			linkCargoPortButtonText.text = isLinkEditing ? "Cancel Linking" : "Link Cargo Ports";

		if (isCreating)
		{
			actionStatusText.text = "Drag a rectangle to create building walls on the inside border.";
			return;
		}

		if (isLinkEditing)
		{
			actionStatusText.text = cargoPortLinkModeController.StatusText;
			return;
		}

		actionStatusText.text = activeBuilding != null
			? "Create a building or start linking outbound cargo ports to inbound cargo ports."
			: "Start a new building footprint creation, or select a building to link cargo ports.";
	}

	private void CreateBuildingRow(Building building)
	{
		RectTransform row = CreateHorizontalContainer(building.DisplayName + "Row", buildingListRoot, 8f);
		buildingRows.Add(row.gameObject);

		TMP_Text label = CreateText(building.DisplayName + "Label", row, 20f);
		label.text = $"{building.DisplayName} ({building.Type})";

		LayoutElement labelLayout = label.GetComponent<LayoutElement>();
		if (labelLayout != null)
		{
			labelLayout.flexibleWidth = 1f;
			labelLayout.minWidth = 0f;
		}

		CreateCompactButton("ScopeButton", row, BuildingWorkScopeUtility.ToDisplayString(building.WorkScope), () => HandleCycleBuildingScopeClicked(building), out _);
		CreateCompactButton("DetailsButton", row, "Details", () => HandleOpenBuildingDetailsClicked(building), out _);
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

	private void EnsureZoneOverlayController()
	{
		if (zoneOverlayController == null)
			zoneOverlayController = FindFirstObjectByType<ZoneOverlayController>(FindObjectsInactive.Include);
	}

	private static BuildingWorkScope GetNextWorkScope(BuildingWorkScope currentScope)
	{
		int enumCount = System.Enum.GetValues(typeof(BuildingWorkScope)).Length;
		int nextIndex = (((int)currentScope) + 1) % enumCount;
		return (BuildingWorkScope)nextIndex;
	}

	private static RectTransform CreateVerticalContainer(string name, Transform parent, float spacing)
	{
		GameObject root = new(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
		root.transform.SetParent(parent, false);

		VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
		layout.spacing = spacing;
		layout.childForceExpandHeight = false;
		layout.childForceExpandWidth = true;
		layout.childControlHeight = true;
		layout.childControlWidth = true;
		layout.childAlignment = TextAnchor.UpperLeft;

		LayoutElement layoutElement = root.GetComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.minWidth = 0f;

		ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

		return root.GetComponent<RectTransform>();
	}

	private static RectTransform CreateHorizontalContainer(string name, Transform parent, float spacing)
	{
		GameObject root = new(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
		root.transform.SetParent(parent, false);

		HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
		layout.spacing = spacing;
		layout.childForceExpandHeight = false;
		layout.childForceExpandWidth = true;
		layout.childControlHeight = true;
		layout.childControlWidth = true;
		layout.childAlignment = TextAnchor.MiddleLeft;

		LayoutElement layoutElement = root.GetComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.minWidth = 0f;

		ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

		return root.GetComponent<RectTransform>();
	}

	private static TMP_Text CreateText(string objectName, Transform parent, float fontSize)
	{
		GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
		textObject.transform.SetParent(parent, false);

		TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
		text.fontSize = fontSize;
		text.color = Color.white;
		text.alignment = TextAlignmentOptions.MidlineLeft;
		text.textWrappingMode = TextWrappingModes.Normal;

		LayoutElement layout = textObject.GetComponent<LayoutElement>();
		layout.flexibleWidth = 1f;
		layout.minWidth = 0f;

		return text;
	}

	private static Button CreateButton(string objectName, Transform parent, string label, UnityEngine.Events.UnityAction onClick, out TMP_Text labelText)
	{
		GameObject buttonObject = new(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
		buttonObject.transform.SetParent(parent, false);

		LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
		layout.preferredHeight = 38f;
		layout.minHeight = 38f;

		Image image = buttonObject.GetComponent<Image>();
		image.color = new Color(0.2f, 0.5f, 0.82f, 1f);

		Button button = buttonObject.GetComponent<Button>();
		button.onClick.AddListener(onClick);

		labelText = CreateText("Label", buttonObject.transform, 18f);
		labelText.alignment = TextAlignmentOptions.Center;
		labelText.text = label;

		return button;
	}

	private static Button CreateCompactButton(string objectName, Transform parent, string label, UnityEngine.Events.UnityAction onClick, out TMP_Text labelText)
	{
		GameObject buttonObject = new(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
		buttonObject.transform.SetParent(parent, false);

		LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
		layout.preferredHeight = 34f;
		layout.minHeight = 34f;
		layout.preferredWidth = 140f;
		layout.minWidth = 140f;
		layout.flexibleWidth = 0f;

		Image image = buttonObject.GetComponent<Image>();
		image.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);

		Button button = buttonObject.GetComponent<Button>();
		button.onClick.AddListener(onClick);

		labelText = CreateText("Label", buttonObject.transform, 16f);
		labelText.alignment = TextAlignmentOptions.Center;
		labelText.text = label;

		RectTransform textRect = labelText.rectTransform;
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = Vector2.zero;
		textRect.offsetMax = Vector2.zero;

		return button;
	}
}
