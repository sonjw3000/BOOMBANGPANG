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
	[SerializeField] private ZoneOverlayController zoneOverlayController;
	[SerializeField] private CargoPortLinkModeController cargoPortLinkModeController;
	[SerializeField] private string windowTitle = "Building Control";
	[SerializeField] private BuildingControlWindowContentView contentPrefab;
	[SerializeField] private BuildingControlBuildingRowView buildingRowPrefab;

	private static readonly BuildingType[] BuildingTypeOptions =
	{
		BuildingType.Generic,
		BuildingType.Staging,
		BuildingType.Storage,
		BuildingType.Packing,
		BuildingType.Launch,
	};

	private static Font defaultFont;

	private bool initialized;
	private int currentTabIndex;
	private readonly List<GameObject> tabRoots = new();
	private readonly List<GameObject> buildingRows = new();
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
		cargoPortLinkModeController ??= GetComponent<CargoPortLinkModeController>();
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
		createButton = contentView.CreateButton;
		linkCargoPortButton = contentView.LinkCargoPortsButton;

		tabRoots.Add(contentView.OverviewTab);
		tabRoots.Add(contentView.OperationsTab);
		tabRoots.Add(contentView.ActionTab);

		if (createButton != null)
			createButton.Configure("Create Building", HandleCreateButtonClicked);

		if (linkCargoPortButton != null)
			linkCargoPortButton.Configure("Link Cargo Ports", HandleLinkCargoPortsButtonClicked);

		BuildActionTypeDropdown();
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

	private void HandleBuildingTypeChanged(int optionIndex)
	{
		if (optionIndex < 0 || optionIndex >= BuildingTypeOptions.Length)
			return;

		overlayController?.SetSelectedBuildingType(BuildingTypeOptions[optionIndex]);
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
		BuildingType selectedType = overlayController != null ? overlayController.SelectedBuildingType : BuildingType.Generic;

		overviewStatusText.text = isCreating
			? $"{BuildingTypeUtility.ToDisplayString(selectedType)} building placement is active. Left click start/end cells, right click to cancel."
			: "Use this window to create buildings and manage each building's worker scope.";
		overviewSummaryText.text = $"Registered Buildings: {buildingCount}\nSelected Build Type: {BuildingTypeUtility.ToDisplayString(selectedType)}";
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
				linkCargoPortButton.LabelText.text = "Link Cargo Ports";
			actionStatusText.text = "Interaction context is unavailable.";
			return;
		}

		bool isCreating = Interaction.Mode == InteractionContext.InteractionMode.BuildingPlacement;
		bool isLinkEditing = cargoPortLinkModeController != null && cargoPortLinkModeController.IsEditing;
		EnsureZoneOverlayController();
		Building activeBuilding = zoneOverlayController != null ? zoneOverlayController.CurrentBuilding : null;
		BuildingType selectedType = overlayController != null ? overlayController.SelectedBuildingType : BuildingType.Generic;

		if (buildingTypeDropdown != null)
		{
			int selectedIndex = Array.IndexOf(BuildingTypeOptions, selectedType);
			buildingTypeDropdown.SetValueWithoutNotify(Mathf.Max(0, selectedIndex));
			buildingTypeDropdown.interactable = isCreating == false && isLinkEditing == false;
		}

		if (createButton.Button != null)
			createButton.Button.interactable = isCreating == false && isLinkEditing == false;
		if (createButton.LabelText != null)
			createButton.LabelText.text = isCreating ? "Creating..." : "Create Building";

		if (linkCargoPortButton.Button != null)
			linkCargoPortButton.Button.interactable = isCreating == false && (isLinkEditing || activeBuilding != null);
		if (linkCargoPortButton.LabelText != null)
			linkCargoPortButton.LabelText.text = isLinkEditing ? "Cancel Linking" : "Link Cargo Ports";

		if (isCreating)
		{
			actionStatusText.text = $"Drag a rectangle to create a {BuildingTypeUtility.ToDisplayString(selectedType)} building footprint.";
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

		actionStatusText.text = activeBuilding != null
			? $"Selected build type: {BuildingTypeUtility.ToDisplayString(selectedType)}. Create a building or start linking outbound cargo ports to inbound cargo ports."
			: $"Selected build type: {BuildingTypeUtility.ToDisplayString(selectedType)}. Start a new building footprint creation, or select a building to link cargo ports.";
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

	private void EnsureZoneOverlayController()
	{
		if (zoneOverlayController == null)
			zoneOverlayController = FindFirstObjectByType<ZoneOverlayController>(FindObjectsInactive.Include);
	}

	private void BuildActionTypeDropdown()
	{
		if (contentView == null || contentView.ActionTab == null)
			return;

		buildingTypeDropdown = CreateDropdownRow(contentView.ActionTab.transform, "Building Type", HandleBuildingTypeChanged);
		if (buildingTypeDropdown == null)
			return;

		buildingTypeDropdown.ClearOptions();
		List<string> options = new();
		for (int i = 0; i < BuildingTypeOptions.Length; ++i)
			options.Add(BuildingTypeUtility.ToDisplayString(BuildingTypeOptions[i]));
		buildingTypeDropdown.AddOptions(options);

		if (actionStatusText != null && actionStatusText.transform.parent == contentView.ActionTab.transform)
			buildingTypeDropdown.transform.SetSiblingIndex(actionStatusText.transform.GetSiblingIndex() + 1);

		int selectedIndex = overlayController != null ? Array.IndexOf(BuildingTypeOptions, overlayController.SelectedBuildingType) : 0;
		buildingTypeDropdown.SetValueWithoutNotify(Mathf.Max(0, selectedIndex));
	}

	private static BuildingWorkScope GetNextWorkScope(BuildingWorkScope currentScope)
	{
		int enumCount = System.Enum.GetValues(typeof(BuildingWorkScope)).Length;
		int nextIndex = (((int)currentScope) + 1) % enumCount;
		return (BuildingWorkScope)nextIndex;
	}

	private static Dropdown CreateDropdownRow(Transform parent, string label, UnityEngine.Events.UnityAction<int> onChanged)
	{
		GameObject row = new($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
		row.transform.SetParent(parent, false);

		HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
		rowLayout.spacing = 12f;
		rowLayout.childAlignment = TextAnchor.MiddleLeft;
		rowLayout.childControlHeight = true;
		rowLayout.childControlWidth = true;
		rowLayout.childForceExpandWidth = false;
		rowLayout.childForceExpandHeight = false;

		row.GetComponent<LayoutElement>().preferredHeight = 42f;

		TextMeshProUGUI labelText = CreateText("Label", row.transform, label);
		labelText.fontSize = 19f;
		labelText.GetComponent<LayoutElement>().preferredWidth = 180f;

		GameObject dropdownObject = new("Dropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown), typeof(LayoutElement));
		dropdownObject.transform.SetParent(row.transform, false);

		Image dropdownImage = dropdownObject.GetComponent<Image>();
		dropdownImage.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

		LayoutElement dropdownLayout = dropdownObject.GetComponent<LayoutElement>();
		dropdownLayout.preferredHeight = 36f;
		dropdownLayout.preferredWidth = 320f;

		Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();

		Text captionText = CreateLegacyText("Label", dropdownObject.transform, "Option");
		captionText.alignment = TextAnchor.MiddleLeft;
		captionText.rectTransform.offsetMin = new Vector2(10f, 0f);
		captionText.rectTransform.offsetMax = new Vector2(-30f, 0f);

		Text arrowText = CreateLegacyText("Arrow", dropdownObject.transform, "v");
		arrowText.alignment = TextAnchor.MiddleCenter;
		arrowText.rectTransform.anchorMin = new Vector2(1f, 0f);
		arrowText.rectTransform.anchorMax = new Vector2(1f, 1f);
		arrowText.rectTransform.pivot = new Vector2(1f, 0.5f);
		arrowText.rectTransform.sizeDelta = new Vector2(24f, 0f);
		arrowText.rectTransform.anchoredPosition = new Vector2(-6f, 0f);

		GameObject templateObject = new("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
		templateObject.transform.SetParent(dropdownObject.transform, false);
		templateObject.SetActive(false);

		RectTransform templateRect = templateObject.GetComponent<RectTransform>();
		templateRect.anchorMin = new Vector2(0f, 0f);
		templateRect.anchorMax = new Vector2(1f, 0f);
		templateRect.pivot = new Vector2(0.5f, 1f);
		templateRect.anchoredPosition = new Vector2(0f, 2f);
		templateRect.sizeDelta = new Vector2(0f, 150f);

		Image templateImage = templateObject.GetComponent<Image>();
		templateImage.color = new Color(0.18f, 0.18f, 0.18f, 1f);

		ScrollRect scrollRect = templateObject.GetComponent<ScrollRect>();
		scrollRect.horizontal = false;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;

		GameObject viewportObject = new("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
		viewportObject.transform.SetParent(templateObject.transform, false);

		RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
		viewportRect.anchorMin = Vector2.zero;
		viewportRect.anchorMax = Vector2.one;
		viewportRect.offsetMin = Vector2.zero;
		viewportRect.offsetMax = Vector2.zero;

		Image viewportImage = viewportObject.GetComponent<Image>();
		viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
		Mask viewportMask = viewportObject.GetComponent<Mask>();
		viewportMask.showMaskGraphic = false;

		GameObject contentObject = new("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
		contentObject.transform.SetParent(viewportObject.transform, false);

		RectTransform contentRect = contentObject.GetComponent<RectTransform>();
		contentRect.anchorMin = new Vector2(0f, 1f);
		contentRect.anchorMax = new Vector2(1f, 1f);
		contentRect.pivot = new Vector2(0.5f, 1f);
		contentRect.offsetMin = Vector2.zero;
		contentRect.offsetMax = Vector2.zero;

		VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
		contentLayout.childForceExpandHeight = false;
		contentLayout.childForceExpandWidth = true;
		contentLayout.childControlHeight = true;
		contentLayout.childControlWidth = true;
		contentLayout.spacing = 2f;

		ContentSizeFitter contentFitter = contentObject.GetComponent<ContentSizeFitter>();
		contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

		GameObject itemObject = new("Item", typeof(RectTransform), typeof(Toggle), typeof(Image), typeof(LayoutElement));
		itemObject.transform.SetParent(contentObject.transform, false);

		LayoutElement itemLayout = itemObject.GetComponent<LayoutElement>();
		itemLayout.preferredHeight = 28f;

		Image itemImage = itemObject.GetComponent<Image>();
		itemImage.color = new Color(0.25f, 0.25f, 0.25f, 1f);

		Toggle itemToggle = itemObject.GetComponent<Toggle>();
		itemToggle.targetGraphic = itemImage;
		itemToggle.isOn = true;

		Text itemLabel = CreateLegacyText("Item Label", itemObject.transform, "Option");
		itemLabel.alignment = TextAnchor.MiddleLeft;
		itemLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
		itemLabel.rectTransform.offsetMax = new Vector2(-10f, 0f);

		scrollRect.viewport = viewportRect;
		scrollRect.content = contentRect;

		dropdown.targetGraphic = dropdownImage;
		dropdown.captionText = captionText;
		dropdown.template = templateRect;
		dropdown.itemText = itemLabel;

		dropdown.onValueChanged.RemoveAllListeners();
		if (onChanged != null)
			dropdown.onValueChanged.AddListener(onChanged);

		return dropdown;
	}

	private static TextMeshProUGUI CreateText(string objectName, Transform parent, string value)
	{
		GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
		textObject.transform.SetParent(parent, false);

		LayoutElement layout = textObject.GetComponent<LayoutElement>();
		layout.preferredHeight = 28f;

		TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
		text.text = value;
		text.fontSize = 20f;
		text.color = Color.white;
		text.alignment = TextAlignmentOptions.MidlineLeft;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.overflowMode = TextOverflowModes.Ellipsis;

		RectTransform rect = text.rectTransform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;

		return text;
	}

	private static Text CreateLegacyText(string objectName, Transform parent, string value)
	{
		GameObject textObject = new(objectName, typeof(RectTransform), typeof(Text));
		textObject.transform.SetParent(parent, false);

		Text text = textObject.GetComponent<Text>();
		text.font = GetDefaultFont();
		text.text = value;
		text.color = Color.white;
		text.alignment = TextAnchor.MiddleLeft;

		RectTransform rect = text.rectTransform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;

		return text;
	}

	private static Font GetDefaultFont()
	{
		if (defaultFont == null)
		{
			defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			if (defaultFont == null)
				defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
		}

		return defaultFont;
	}
}
