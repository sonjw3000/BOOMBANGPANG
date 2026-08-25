using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class BuildManagementWindow : MonoBehaviour
	{
		private enum RoutingSourceKind
		{
			None,
			LandingArea,
			Building,
		}

		private const string SelectedTabClass = "build-tab-button--selected";
		private const string SelectedCategoryClass = "build-category-button--selected";
		private const string ExpandedRoutingSourceClass = "build-routing-source--expanded";
		private const string SelectedRoutingConnectionClass = "build-routing-connection--selected";
		private static readonly ItemStatus[] RuleItemStatuses = { ItemStatus.NotDefined, ItemStatus.None, ItemStatus.Labeled, ItemStatus.Packed };
		private static readonly WorkerKind[] RuleWorkerKinds = { WorkerKind.None, WorkerKind.Human, WorkerKind.Robot };
		private static readonly CapsuleBufferStateRequirement[] RuleCapsuleBufferStates =
		{
			CapsuleBufferStateRequirement.Any,
			CapsuleBufferStateRequirement.Inside,
			CapsuleBufferStateRequirement.Empty,
		};
		private static readonly CargoProcessStage[] RuleCargoProcessStages =
		{
			CargoProcessStage.None,
			CargoProcessStage.Unlabeled,
			CargoProcessStage.Labeled,
			CargoProcessStage.Picked,
			CargoProcessStage.Packed,
			CargoProcessStage.LaunchReady,
		};

		private UIWindow window;
		private VisualTreeAsset contentTemplate;
		private VisualTreeAsset placeableRowTemplate;
		private VisualTreeAsset ruleRowTemplate;
		private BuildingPlacementOverlayController buildingPlacementOverlay;
		private RoutingConnectivityOverlayController routingOverlay;
		private CargoPortLinkModeController buildingLinkController;
		private WorkflowDestinationLinkModeController workflowDestinationController;
		private Button buildingsButton;
		private Button facilitiesButton;
		private Button rulesButton;
		private Button routingButton;
		private VisualElement buildingsTab;
		private VisualElement facilitiesTab;
		private VisualElement rulesTab;
		private VisualElement routingTab;
		private Label routingSummary;
		private Label routingMessage;
		private Button routingLinkButton;
		private Button routingLandingLinkButton;
		private ScrollView routingSourceList;
		private Label routingEmpty;
		private DropdownField footprintField;
		private Label buildingSelectionName;
		private Label buildingSelectionDetails;
		private Label buildingMessage;
		private Button createBuildingButton;
		private ScrollView categoryList;
		private Label catalogTitle;
		private Label catalogMessage;
		private ScrollView placeableList;
		private Label placeableEmpty;
		private Button createRuleButton;
		private Button createColdChainRuleButton;
		private ScrollView ruleList;
		private Label ruleEmpty;
		private Label ruleMessage;
		private VisualElement ruleLibrary;
		private ScrollView ruleEditor;
		private Label ruleEditorTitle;
		private TextField ruleNameField;
		private IntegerField rulePriorityField;
		private Slider ruleRedSlider;
		private Slider ruleGreenSlider;
		private Slider ruleBlueSlider;
		private VisualElement ruleColorPreview;
		private DropdownField ruleCapsuleBufferStateField;
		private DropdownField ruleCargoProcessStageField;
		private DropdownField ruleItemStatusField;
		private DropdownField ruleWorkerKindField;
		private DropdownField ruleItemField;
		private Label whiteListSummary;
		private Label blackListSummary;
		private readonly List<ItemDefinition> ruleItems = new();
		private FacilityRule workingRule = new();
		private Color workingRuleColor = Color.white;
		private uint editingRuleId;
		private bool suppressRuleEditorEvents;
		private bool applyModeActive;
		private uint applyingRuleId;
		private readonly List<BuildingFootprintPreset> footprintPresets = new();
		private BuildPlaceableSection selectedSection;
		private BuildPlaceableCatalog catalog;
		private BuildingFootprintService footprintService;
		private EconomyService economyService;
		private FacilityRuleManager ruleManager;
		private ResearchService researchService;
		[System.NonSerialized] private bool initialized;
		private bool started;
		private int selectedTabIndex;
		private RoutingSourceKind selectedRoutingSourceKind;
		private Area selectedRoutingSourceArea;
		private uint selectedRoutingSourceBuildingId;
		private RoutingConnectionKey? selectedRoutingConnection;
		private int configuredRoutingConnectionCount;

		private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
		private AreaManager AreaManager => GameContext.HasInstance ? GameContext.Instance.AreaMgr : null;

		public void Configure(UIWindow targetWindow, VisualTreeAsset targetContentTemplate,
			VisualTreeAsset targetPlaceableRowTemplate, VisualTreeAsset targetRuleRowTemplate,
			BuildingPlacementOverlayController targetBuildingPlacementOverlay,
			RoutingConnectivityOverlayController targetRoutingOverlay,
			CargoPortLinkModeController targetBuildingLinkController,
			WorkflowDestinationLinkModeController targetWorkflowDestinationController)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
			placeableRowTemplate = targetPlaceableRowTemplate;
			ruleRowTemplate = targetRuleRowTemplate;
			buildingPlacementOverlay = targetBuildingPlacementOverlay;
			routingOverlay = targetRoutingOverlay;
			buildingLinkController = targetBuildingLinkController;
			workflowDestinationController = targetWorkflowDestinationController;
		}

		private void OnEnable()
		{
			InitializeView();
			if (started) BindServices();
		}

		private void Start()
		{
			started = true;
			BindServices();
		}

		private void Update()
		{
			if (applyModeActive && Input.GetMouseButtonDown(1)) EndApplyMode();
		}

		private void OnDisable()
		{
			EndApplyMode();
			buildingLinkController?.EndLinkEdit();
			workflowDestinationController?.EndSelection();
			workflowDestinationController?.SetRoutingVisible(false);
			routingOverlay?.SetVisible(false);
			UnbindControls();
			UnbindServices();
			initialized = false;
		}

		public void Open()
		{
			if (InitializeView() == false) return;
			EndApplyMode();
			if (catalog == null || footprintService == null) BindServices();
			RefreshAll();
			window.Open();
		}

		public void OpenRoutingAndBeginInboundDestinationSelection()
		{
			OpenRoutingForDestinationSelection(true);
		}

		public void OpenRoutingAndBeginOutboundDestinationSelection()
		{
			OpenRoutingForDestinationSelection(false);
		}

		private bool InitializeView()
		{
			if (initialized) return true;
			if (window == null || contentTemplate == null || placeableRowTemplate == null || ruleRowTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[BuildManagementWindow] Window or VisualTreeAsset references are missing.", this);
				return false;
			}

			TemplateContainer content = contentTemplate.CloneTree();
			buildingsButton = content.Q<Button>("build-buildings-button");
			facilitiesButton = content.Q<Button>("build-facilities-button");
			rulesButton = content.Q<Button>("build-rules-button");
			routingButton = content.Q<Button>("build-routing-button");
			buildingsTab = content.Q<VisualElement>("build-buildings-tab");
			facilitiesTab = content.Q<VisualElement>("build-facilities-tab");
			rulesTab = content.Q<VisualElement>("build-rules-tab");
			routingTab = content.Q<VisualElement>("build-routing-tab");
			routingSummary = content.Q<Label>("build-routing-summary");
			routingMessage = content.Q<Label>("build-routing-message");
			routingLinkButton = content.Q<Button>("build-routing-link-button");
			routingLandingLinkButton = content.Q<Button>("build-routing-landing-link-button");
			routingSourceList = content.Q<ScrollView>("build-routing-source-list");
			routingEmpty = content.Q<Label>("build-routing-empty");
			footprintField = content.Q<DropdownField>("building-footprint-field");
			buildingSelectionName = content.Q<Label>("building-selection-name");
			buildingSelectionDetails = content.Q<Label>("building-selection-details");
			buildingMessage = content.Q<Label>("building-build-message");
			createBuildingButton = content.Q<Button>("create-building-button");
			categoryList = content.Q<ScrollView>("build-category-list");
			catalogTitle = content.Q<Label>("build-catalog-title");
			catalogMessage = content.Q<Label>("build-catalog-message");
			placeableList = content.Q<ScrollView>("build-placeable-list");
			placeableEmpty = content.Q<Label>("build-placeable-empty");
			createRuleButton = content.Q<Button>("create-rule-preset-button");
			createColdChainRuleButton = content.Q<Button>("create-cold-chain-rule-button");
			ruleList = content.Q<ScrollView>("build-rule-list");
			ruleEmpty = content.Q<Label>("build-rule-empty");
			ruleMessage = content.Q<Label>("build-rule-message");
			ruleLibrary = content.Q<VisualElement>("build-rule-library");
			ruleEditor = content.Q<ScrollView>("build-rule-editor");
			ruleEditorTitle = content.Q<Label>("rule-editor-title");
			ruleNameField = content.Q<TextField>("rule-editor-name");
			rulePriorityField = content.Q<IntegerField>("rule-editor-priority");
			ruleRedSlider = content.Q<Slider>("rule-editor-red");
			ruleGreenSlider = content.Q<Slider>("rule-editor-green");
			ruleBlueSlider = content.Q<Slider>("rule-editor-blue");
			ruleColorPreview = content.Q<VisualElement>("rule-editor-color-preview");
			ruleCapsuleBufferStateField = content.Q<DropdownField>("rule-editor-capsule-state");
			ruleCargoProcessStageField = content.Q<DropdownField>("rule-editor-cargo-stage");
			ruleItemStatusField = content.Q<DropdownField>("rule-editor-item-status");
			ruleWorkerKindField = content.Q<DropdownField>("rule-editor-worker-kind");
			ruleItemField = content.Q<DropdownField>("rule-editor-item");
			whiteListSummary = content.Q<Label>("rule-whitelist-summary");
			blackListSummary = content.Q<Label>("rule-blacklist-summary");

			if (buildingsButton == null || facilitiesButton == null || rulesButton == null || routingButton == null || buildingsTab == null ||
				facilitiesTab == null || rulesTab == null || routingTab == null || routingSummary == null || routingMessage == null || routingLinkButton == null ||
				routingLandingLinkButton == null || routingSourceList == null || routingEmpty == null ||
				footprintField == null || buildingSelectionName == null ||
				buildingSelectionDetails == null || buildingMessage == null || createBuildingButton == null ||
				categoryList == null || catalogTitle == null || catalogMessage == null || placeableList == null ||
				placeableEmpty == null || createRuleButton == null || createColdChainRuleButton == null ||
				ruleList == null || ruleEmpty == null || ruleMessage == null ||
				ruleLibrary == null || ruleEditor == null || ruleNameField == null || rulePriorityField == null ||
				ruleColorPreview == null || ruleCapsuleBufferStateField == null || ruleCargoProcessStageField == null ||
				ruleItemStatusField == null || ruleWorkerKindField == null || ruleItemField == null)
			{
				Debug.LogError("[BuildManagementWindow] Required UXML elements are missing.", this);
				return false;
			}

			window.SetTitle("Build Management");
			window.SetContent(content);
			buildingsButton.clicked += OpenBuildings;
			facilitiesButton.clicked += OpenFacilities;
			rulesButton.clicked += OpenRules;
			routingButton.clicked += OpenRouting;
			routingLinkButton.clicked += ToggleBuildingLinkMode;
			routingLandingLinkButton.clicked += ToggleLandingDestinationMode;
			window.Opened += OnWindowOpened;
			window.Closed += OnWindowClosed;
			if (routingOverlay != null) routingOverlay.PathsChanged += RefreshRoutingSummary;
			if (buildingLinkController != null)
			{
				buildingLinkController.StateChanged += RefreshRoutingSummary;
				buildingLinkController.LinkCreated += OnBuildingLinkCreated;
			}
			if (workflowDestinationController != null)
			{
				workflowDestinationController.StateChanged += RefreshRoutingSummary;
				workflowDestinationController.DestinationChanged += OnWorkflowDestinationChanged;
			}
			createBuildingButton.clicked += BeginBuildingPlacement;
			createRuleButton.clicked += CreateRule;
			createColdChainRuleButton.clicked += CreateColdChainRule;
			footprintField.RegisterValueChangedCallback(OnFootprintChanged);
			BindRuleEditor(content);
			initialized = true;
			SelectTab(0);
			ShowRuleLibrary();
			return true;
		}

		private void UnbindControls()
		{
			if (buildingsButton != null) buildingsButton.clicked -= OpenBuildings;
			if (facilitiesButton != null) facilitiesButton.clicked -= OpenFacilities;
			if (rulesButton != null) rulesButton.clicked -= OpenRules;
			if (routingButton != null) routingButton.clicked -= OpenRouting;
			if (routingLinkButton != null) routingLinkButton.clicked -= ToggleBuildingLinkMode;
			if (routingLandingLinkButton != null) routingLandingLinkButton.clicked -= ToggleLandingDestinationMode;
			if (window != null)
			{
				window.Opened -= OnWindowOpened;
				window.Closed -= OnWindowClosed;
			}
			if (workflowDestinationController != null)
			{
				workflowDestinationController.StateChanged -= RefreshRoutingSummary;
				workflowDestinationController.DestinationChanged -= OnWorkflowDestinationChanged;
			}
			if (routingOverlay != null) routingOverlay.PathsChanged -= RefreshRoutingSummary;
			if (buildingLinkController != null)
			{
				buildingLinkController.StateChanged -= RefreshRoutingSummary;
				buildingLinkController.LinkCreated -= OnBuildingLinkCreated;
			}
			if (createBuildingButton != null) createBuildingButton.clicked -= BeginBuildingPlacement;
			if (createRuleButton != null) createRuleButton.clicked -= CreateRule;
			if (createColdChainRuleButton != null) createColdChainRuleButton.clicked -= CreateColdChainRule;
			footprintField?.UnregisterValueChangedCallback(OnFootprintChanged);
		}

		private void BindServices()
		{
			UnbindServices();
			if (GameContext.HasInstance == false) return;
			catalog = GameContext.Instance.BuildPlaceableCatalog;
			footprintService = GameContext.Instance.BuildingFootprintService;
			economyService = GameContext.Instance.EconomyService;
			ruleManager = GameContext.Instance.FacilityRuleMgr;
			researchService = GameContext.Instance.ResearchService;
			GameContext.Instance.InteractionCtx.OnModeChanged += OnInteractionModeChanged;
			if (economyService != null) economyService.OnMoneyChanged += OnMoneyChanged;
			if (researchService != null) researchService.OnResearchStateChanged += OnResearchStateChanged;
			if (ruleManager != null)
			{
				ruleManager.OnPresetCreated += OnRuleChanged;
				ruleManager.OnPresetChanged += OnRuleChanged;
				ruleManager.OnPresetDeleted += OnRuleDeleted;
				ruleManager.OnFacilityRulePresetApplied += OnRuleApplied;
				ruleManager.OnPresetsRebuilt += RefreshRules;
			}
		}

		private void UnbindServices()
		{
			if (GameContext.HasInstance && GameContext.Instance.InteractionCtx != null)
				GameContext.Instance.InteractionCtx.OnModeChanged -= OnInteractionModeChanged;
			if (economyService != null) economyService.OnMoneyChanged -= OnMoneyChanged;
			if (researchService != null) researchService.OnResearchStateChanged -= OnResearchStateChanged;
			if (ruleManager != null)
			{
				ruleManager.OnPresetCreated -= OnRuleChanged;
				ruleManager.OnPresetChanged -= OnRuleChanged;
				ruleManager.OnPresetDeleted -= OnRuleDeleted;
				ruleManager.OnFacilityRulePresetApplied -= OnRuleApplied;
				ruleManager.OnPresetsRebuilt -= RefreshRules;
			}
			catalog = null;
			footprintService = null;
			economyService = null;
			ruleManager = null;
			researchService = null;
		}

		private void OnInteractionModeChanged(
			InteractionContext.InteractionDomain _,
			InteractionContext.InteractionAction action)
		{
			if (applyModeActive && action != InteractionContext.InteractionAction.Select)
				EndApplyMode();
		}

		private void OpenBuildings() => SelectTab(0);
		private void OpenFacilities() => SelectTab(1);
		private void OpenRules() => SelectTab(2);
		private void OpenRouting() => SelectTab(3);

		private void SelectTab(int index)
		{
			if (index != 3)
			{
				buildingLinkController?.EndLinkEdit();
				workflowDestinationController?.EndSelection();
				ClearRoutingSelection();
			}
			selectedTabIndex = index;
			buildingsTab.style.display = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			facilitiesTab.style.display = index == 1 ? DisplayStyle.Flex : DisplayStyle.None;
			rulesTab.style.display = index == 2 ? DisplayStyle.Flex : DisplayStyle.None;
			routingTab.style.display = index == 3 ? DisplayStyle.Flex : DisplayStyle.None;
			buildingsButton.EnableInClassList(SelectedTabClass, index == 0);
			facilitiesButton.EnableInClassList(SelectedTabClass, index == 1);
			rulesButton.EnableInClassList(SelectedTabClass, index == 2);
			routingButton.EnableInClassList(SelectedTabClass, index == 3);
			routingOverlay?.SetVisible(index == 3 && window != null && window.IsOpen);
			workflowDestinationController?.SetRoutingVisible(index == 3 && window != null && window.IsOpen);
			if (index == 3) RefreshRoutingConnectionList();
			RefreshRoutingSummary();
		}

		private void OnWindowOpened()
		{
			routingOverlay?.SetVisible(selectedTabIndex == 3);
			workflowDestinationController?.SetRoutingVisible(selectedTabIndex == 3);
			if (selectedTabIndex == 3) RefreshRoutingConnectionList();
			RefreshRoutingSummary();
		}

		private void OnWindowClosed()
		{
			buildingLinkController?.EndLinkEdit();
			workflowDestinationController?.EndSelection();
			workflowDestinationController?.SetRoutingVisible(false);
			ClearRoutingSelection();
			routingOverlay?.SetVisible(false);
		}

		private void ToggleBuildingLinkMode()
		{
			if (buildingLinkController == null) return;
			if (buildingLinkController.IsEditing)
				buildingLinkController.EndLinkEdit();
			else
			{
				workflowDestinationController?.EndSelection();
				ClearRoutingSelection();
				RefreshRoutingConnectionList();
				buildingLinkController.BeginLinkEdit();
			}
			RefreshRoutingSummary();
		}

		private void ToggleLandingDestinationMode()
		{
			if (workflowDestinationController == null) return;
			if (workflowDestinationController.IsEditing)
				workflowDestinationController.EndSelection();
			else
			{
				buildingLinkController?.EndLinkEdit();
				ClearRoutingSelection();
				RefreshRoutingConnectionList();
				workflowDestinationController.BeginInboundSelection();
			}
			RefreshRoutingSummary();
		}

		private void OpenRoutingForDestinationSelection(bool inbound)
		{
			if (InitializeView() == false || workflowDestinationController == null) return;
			EndApplyMode();
			if (catalog == null || footprintService == null) BindServices();
			RefreshAll();
			SelectTab(3);
			window.Open();
			buildingLinkController?.EndLinkEdit();
			ClearRoutingSelection();
			RefreshRoutingConnectionList();
			if (inbound)
				workflowDestinationController.BeginInboundSelection();
			else
				workflowDestinationController.BeginOutboundSelection();
			RefreshRoutingSummary();
		}

		private void OnBuildingLinkCreated(Building source, Building target)
		{
			routingOverlay?.RefreshConnections();
			RefreshRoutingConnectionList();
			RefreshRoutingSummary();
		}

		private void OnWorkflowDestinationChanged()
		{
			routingOverlay?.RefreshConnections();
			RefreshRoutingConnectionList();
			RefreshRoutingSummary();
		}

		private void RefreshRoutingConnectionList()
		{
			if (routingSourceList == null || routingEmpty == null) return;
			routingSourceList.Clear();

			int sourceCount = 0;
			configuredRoutingConnectionCount = 0;
			bool selectedSourceExists = false;
			AreaManager areaManager = AreaManager;
			if (areaManager != null)
			{
				IReadOnlyList<Area> areas = areaManager.RegisteredAreas;
				for (int i = 0; i < areas.Count; ++i)
				{
					Area area = areas[i];
					if (area == null || area.Type != AreaType.RocketLanding || area.DestinationBuildingId == 0) continue;
					bool expanded = IsSelectedRoutingSource(area);
					selectedSourceExists |= expanded;
					routingSourceList.Add(CreateRoutingSource(
						area.DisplayName,
						1,
						expanded,
						() => ToggleRoutingSource(area),
						expanded ? new[] { RoutingConnectionKey.ForLanding(area, area.DestinationBuildingId) } : null));
					sourceCount += 1;
					configuredRoutingConnectionCount += 1;
				}
			}

			BuildingManager buildingManager = BuildingManager;
			if (buildingManager != null)
			{
				IReadOnlyList<Building> buildings = buildingManager.RegisteredBuildings;
				for (int i = 0; i < buildings.Count; ++i)
				{
					Building building = buildings[i];
					if (building == null || building.OutputBuildingIds.Count == 0) continue;
					bool expanded = IsSelectedRoutingSource(building.RuntimeBuildingId);
					selectedSourceExists |= expanded;
					List<RoutingConnectionKey> connections = null;
					if (expanded)
					{
						connections = new List<RoutingConnectionKey>(building.OutputBuildingIds.Count);
						foreach (uint targetBuildingId in building.OutputBuildingIds)
							connections.Add(RoutingConnectionKey.ForBuildings(building.RuntimeBuildingId, targetBuildingId));
					}
					routingSourceList.Add(CreateRoutingSource(
						building.DisplayName,
						building.OutputBuildingIds.Count,
						expanded,
						() => ToggleRoutingSource(building.RuntimeBuildingId),
						connections));
					sourceCount += 1;
					configuredRoutingConnectionCount += building.OutputBuildingIds.Count;
				}
			}

			routingEmpty.style.display = sourceCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			if (selectedRoutingSourceKind != RoutingSourceKind.None && selectedSourceExists == false)
				ClearRoutingSelection();
		}

		private VisualElement CreateRoutingSource(string sourceName, int connectionCount, bool expanded,
			Action toggleSource, IReadOnlyList<RoutingConnectionKey> connections)
		{
			VisualElement source = new();
			source.AddToClassList("build-routing-source");
			source.EnableInClassList(ExpandedRoutingSourceClass, expanded);

			Button header = new(toggleSource) { text = string.Empty };
			header.AddToClassList("build-routing-source__header");
			Label name = new(sourceName);
			name.AddToClassList("build-routing-source__name");
			Label count = new($"Connection: {connectionCount}");
			count.AddToClassList("build-routing-source__count");
			header.Add(name);
			header.Add(count);
			source.Add(header);

			if (expanded && connections != null)
			{
				VisualElement details = new();
				details.AddToClassList("build-routing-details");
				for (int i = 0; i < connections.Count; ++i)
					details.Add(CreateRoutingConnection(connections[i]));
				source.Add(details);
			}

			return source;
		}

		private VisualElement CreateRoutingConnection(RoutingConnectionKey connection)
		{
			VisualElement row = new();
			row.AddToClassList("build-routing-connection");
			row.EnableInClassList(SelectedRoutingConnectionClass,
				selectedRoutingConnection.HasValue && selectedRoutingConnection.Value.Equals(connection));

			Button target = new(() => ToggleRoutingConnection(connection))
			{
				text = ResolveRoutingBuildingName(connection.TargetBuildingId),
			};
			target.AddToClassList("build-routing-connection__target");
			Button disconnect = new(() => DisconnectRoutingConnection(connection)) { text = "Disconnect" };
			disconnect.AddToClassList("build-routing-connection__disconnect");
			row.Add(target);
			row.Add(disconnect);
			return row;
		}

		private void ToggleRoutingSource(Area area)
		{
			if (IsSelectedRoutingSource(area))
				ClearRoutingSelection();
			else
			{
				selectedRoutingSourceKind = RoutingSourceKind.LandingArea;
				selectedRoutingSourceArea = area;
				selectedRoutingSourceBuildingId = 0;
				selectedRoutingConnection = null;
				routingOverlay?.ShowSourceConnections(area);
			}
			RefreshRoutingConnectionList();
			RefreshRoutingSummary();
		}

		private void ToggleRoutingSource(uint buildingId)
		{
			if (IsSelectedRoutingSource(buildingId))
				ClearRoutingSelection();
			else
			{
				selectedRoutingSourceKind = RoutingSourceKind.Building;
				selectedRoutingSourceArea = null;
				selectedRoutingSourceBuildingId = buildingId;
				selectedRoutingConnection = null;
				routingOverlay?.ShowSourceConnections(buildingId);
			}
			RefreshRoutingConnectionList();
			RefreshRoutingSummary();
		}

		private void ToggleRoutingConnection(RoutingConnectionKey connection)
		{
			if (selectedRoutingConnection.HasValue && selectedRoutingConnection.Value.Equals(connection))
			{
				selectedRoutingConnection = null;
				ApplySelectedRoutingSourceFilter();
			}
			else
			{
				selectedRoutingConnection = connection;
				routingOverlay?.ShowConnection(connection);
			}
			RefreshRoutingConnectionList();
			RefreshRoutingSummary();
		}

		private void DisconnectRoutingConnection(RoutingConnectionKey connection)
		{
			bool disconnected = false;
			if (connection.Type == RoutingConnectionType.LandingToBuilding)
			{
				disconnected = AreaManager != null &&
					AreaManager.TrySetDestinationBuilding(connection.SourceArea, 0);
			}
			else if (BuildingManager != null &&
				BuildingManager.TryGetBuilding(connection.SourceBuildingId, out Building source) && source != null)
			{
				if (BuildingManager.TryGetBuilding(connection.TargetBuildingId, out Building target) && target != null)
					disconnected = BuildingManager.TryUnlinkBuildings(source, target);
				else
					disconnected = source.RemoveOutputBuilding(connection.TargetBuildingId);
			}

			if (disconnected == false) return;
			selectedRoutingConnection = null;
			if (SelectedRoutingSourceHasConnections())
				ApplySelectedRoutingSourceFilter();
			else
				ClearRoutingSelection();
			routingOverlay?.RefreshConnections();
			RefreshRoutingConnectionList();
			RefreshRoutingSummary();
		}

		private void ApplySelectedRoutingSourceFilter()
		{
			if (selectedRoutingSourceKind == RoutingSourceKind.LandingArea)
				routingOverlay?.ShowSourceConnections(selectedRoutingSourceArea);
			else if (selectedRoutingSourceKind == RoutingSourceKind.Building)
				routingOverlay?.ShowSourceConnections(selectedRoutingSourceBuildingId);
			else
				routingOverlay?.ShowAllConnections();
		}

		private void ClearRoutingSelection()
		{
			selectedRoutingSourceKind = RoutingSourceKind.None;
			selectedRoutingSourceArea = null;
			selectedRoutingSourceBuildingId = 0;
			selectedRoutingConnection = null;
			routingOverlay?.ShowAllConnections();
		}

		private bool IsSelectedRoutingSource(Area area) =>
			selectedRoutingSourceKind == RoutingSourceKind.LandingArea && ReferenceEquals(selectedRoutingSourceArea, area);

		private bool IsSelectedRoutingSource(uint buildingId) =>
			selectedRoutingSourceKind == RoutingSourceKind.Building && selectedRoutingSourceBuildingId == buildingId;

		private bool SelectedRoutingSourceHasConnections()
		{
			if (selectedRoutingSourceKind == RoutingSourceKind.LandingArea)
				return selectedRoutingSourceArea != null && selectedRoutingSourceArea.DestinationBuildingId != 0;
			if (selectedRoutingSourceKind == RoutingSourceKind.Building && BuildingManager != null &&
				BuildingManager.TryGetBuilding(selectedRoutingSourceBuildingId, out Building building) && building != null)
				return building.OutputBuildingIds.Count > 0;
			return false;
		}

		private string ResolveRoutingBuildingName(uint buildingId)
		{
			return BuildingManager != null && BuildingManager.TryGetBuilding(buildingId, out Building building) && building != null
				? building.DisplayName
				: $"Missing Building #{buildingId}";
		}

		private void RefreshAll()
		{
			RefreshBuildingOptions();
			RefreshCategories();
			DisplaySelectedSection();
			RefreshRules();
			RefreshRoutingConnectionList();
			RefreshRoutingSummary();
		}

		private void RefreshRoutingSummary()
		{
			if (routingSummary == null || routingMessage == null || routingLinkButton == null || routingLandingLinkButton == null) return;
			routingLinkButton.SetEnabled(buildingLinkController != null);
			routingLinkButton.text = buildingLinkController != null && buildingLinkController.IsEditing
				? "Cancel Linking"
				: "Link Buildings";
			routingLandingLinkButton.SetEnabled(workflowDestinationController != null);
			routingLandingLinkButton.text = workflowDestinationController != null && workflowDestinationController.IsEditing
				? workflowDestinationController.SelectionType == WorkflowDestinationLinkModeController.DestinationSelectionType.OutboundLoading
					? "Cancel Loading Link"
					: "Cancel Landing Link"
				: "Link Landing";
			if (routingOverlay == null)
			{
				routingSummary.text = "Routing overlay unavailable";
				routingMessage.text = "The connectivity overlay controller is not configured.";
				return;
			}

			routingSummary.text = $"{configuredRoutingConnectionCount} connections · {routingOverlay.VisiblePathCount} visible paths";
			if (buildingLinkController != null && buildingLinkController.IsEditing)
			{
				routingMessage.text = buildingLinkController.StatusText;
				return;
			}
			if (workflowDestinationController != null &&
				(workflowDestinationController.IsEditing || workflowDestinationController.HasStatusMessage))
			{
				routingMessage.text = workflowDestinationController.StatusText;
				return;
			}

			routingMessage.text = routingOverlay.PendingPathCount > 0
				? $"Calculating {routingOverlay.PendingPathCount} paths..."
				: "Orange: Landing routing · Green: Building routing · Green takes priority where routes overlap.";
		}

		private void RefreshBuildingOptions()
		{
			footprintPresets.Clear();
			List<string> choices = new();
			if (footprintService != null)
			{
				foreach (BuildingFootprintPreset preset in footprintService.AvailablePresets)
				{
					if (preset == null || preset.IsValid == false) continue;
					footprintPresets.Add(preset);
					choices.Add(preset.DisplayName);
				}
			}

			footprintField.choices = choices;
			int selectedIndex = Mathf.Max(0, footprintPresets.IndexOf(footprintService?.ActivePreset));
			if (choices.Count > 0) footprintField.SetValueWithoutNotify(choices[Mathf.Clamp(selectedIndex, 0, choices.Count - 1)]);
			RefreshBuildingSelection();
		}


		private void OnFootprintChanged(ChangeEvent<string> _)
		{
			if (footprintService != null && footprintField.index >= 0 && footprintField.index < footprintPresets.Count)
				footprintService.SetActivePreset(footprintPresets[footprintField.index]);
			RefreshBuildingSelection();
		}

		private void RefreshBuildingSelection()
		{
			BuildingFootprintPreset preset = GetSelectedPreset();
			bool available = preset != null && footprintService != null;
			createBuildingButton.SetEnabled(available);
			buildingSelectionName.text = available
				? $"Building · {preset.DisplayName}"
				: "No footprint available";
			buildingSelectionDetails.text = available
				? $"{preset.Width} × {preset.Height} footprint. Wall costs are charged only when placement succeeds."
				: "Building placement requires a valid footprint preset.";
		}

		private void BeginBuildingPlacement()
		{
			BuildingFootprintPreset preset = GetSelectedPreset();
			if (preset == null || footprintService == null || footprintService.SetActivePreset(preset) == false)
			{
				buildingMessage.text = "Building placement is unavailable.";
				return;
			}

			if (buildingPlacementOverlay == null)
			{
				buildingMessage.text = "Building placement overlay is unavailable.";
				return;
			}

			window.Close();
			buildingPlacementOverlay.BeginCreateOneShot();
		}

		private void RefreshCategories()
		{
			categoryList.Clear();
			IReadOnlyList<BuildPlaceableSection> sections = catalog?.Sections;
			if (ContainsVisibleSection(sections, selectedSection) == false) selectedSection = FirstVisibleSection(sections);
			if (sections == null) return;
			foreach (BuildPlaceableSection section in sections)
			{
				if (IsSectionVisible(section) == false) continue;
				BuildPlaceableSection captured = section;
				Button button = new(() => SelectSection(captured)) { text = GetSectionName(section) };
				button.AddToClassList("build-category-button");
				button.EnableInClassList(SelectedCategoryClass, section == selectedSection);
				categoryList.Add(button);
			}
		}

		private void SelectSection(BuildPlaceableSection section)
		{
			selectedSection = section;
			RefreshCategories();
			DisplaySelectedSection();
		}

		private void DisplaySelectedSection()
		{
			placeableList.Clear();
			catalogTitle.text = selectedSection != null ? GetSectionName(selectedSection) : "Facilities";
			int count = 0;
			if (selectedSection?.placeables != null)
			{
				foreach (PlaceableDefinition definition in selectedSection.placeables)
				{
					if (definition == null || definition.prefab == null || definition.gridFootprint == null ||
						IsPlaceableUnlocked(definition) == false) continue;
					placeableList.Add(CreatePlaceableRow(definition));
					++count;
				}
			}
			catalogMessage.text = count > 0 ? $"{count} buildable facilities. Select one to enter placement mode." : "No available facilities.";
			placeableEmpty.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private VisualElement CreatePlaceableRow(PlaceableDefinition definition)
		{
			TemplateContainer row = placeableRowTemplate.CloneTree();
			VisualElement icon = row.Q<VisualElement>("build-placeable-icon");
			row.Q<Label>("build-placeable-name").text = GetPlaceableName(definition);
			row.Q<Label>("build-placeable-type").text = definition.definitionType.ToString();
			row.Q<Label>("build-placeable-size").text = $"{definition.gridFootprint.width} × {definition.gridFootprint.height}";
			row.Q<Label>("build-placeable-environment").text = GetEnvironmentName(definition.placementEnvironment);
			row.Q<Label>("build-placeable-cost").text = $"${definition.Cost:N0}";
			if (definition.icon != null) icon.style.backgroundImage = new StyleBackground(definition.icon);
			Button placeButton = row.Q<Button>("build-placeable-button");
			placeButton.SetEnabled(economyService != null && economyService.CanAfford(Mathf.Max(0, definition.Cost)));
			placeButton.clicked += () => BeginFacilityPlacement(definition);
			return row;
		}

		private void BeginFacilityPlacement(PlaceableDefinition definition)
		{
			if (definition == null || GameContext.HasInstance == false || GameContext.Instance.InteractionCtx == null) return;
			if (IsPlaceableUnlocked(definition) == false)
			{
				catalogMessage.text = $"Requires research: {definition.RequiredResearchUid}.";
				return;
			}
			window.Close();
			GameContext.Instance.InteractionCtx.EnterPlacementMode(definition);
		}

		private void RefreshRules()
		{
			if (ruleList == null) return;
			createColdChainRuleButton.style.display = IsThermalOperationsResearched()
				? DisplayStyle.Flex
				: DisplayStyle.None;
			ruleList.Clear();
			IReadOnlyList<FacilityRulePreset> presets = ruleManager?.Presets;
			int count = presets?.Count ?? 0;
			for (int i = 0; i < count; ++i)
			{
				FacilityRulePreset preset = presets[i];
				if (preset != null) ruleList.Add(CreateRuleRow(preset));
			}
			ruleEmpty.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			ruleMessage.text = ruleManager != null
				? $"{count} presets · {ruleManager.GetNoRuleFacilityCount()} facilities currently have no Rule."
				: "FacilityRuleManager is unavailable.";
		}

		private VisualElement CreateRuleRow(FacilityRulePreset preset)
		{
			TemplateContainer row = ruleRowTemplate.CloneTree();
			row.Q<VisualElement>("build-rule-color").style.backgroundColor = preset.Color;
			row.Q<Label>("build-rule-name").text = preset.DisplayName;
			row.Q<Label>("build-rule-summary").text = BuildRuleSummary(preset.Rule);
			row.Q<Label>("build-rule-priority").text = $"Priority {preset.Rule?.Priority ?? 0}";
			row.Q<Label>("build-rule-applied").text = $"{ruleManager?.GetAppliedFacilityCount(preset.Id) ?? 0} facilities";
			Button duplicateButton = row.Q<Button>("build-rule-duplicate-button");
			Button applyButton = row.Q<Button>("build-rule-apply-button");
			Button editButton = row.Q<Button>("build-rule-edit-button");
			Button deleteButton = row.Q<Button>("build-rule-delete-button");
			duplicateButton.clicked += () => DuplicateRule(preset);
			applyButton.clicked += () => BeginApplyMode(preset.Id);
			editButton.clicked += () => EditRule(preset);
			bool deleteConfirmed = false;
			deleteButton.clicked += () =>
			{
				if (deleteConfirmed == false)
				{
					deleteConfirmed = true;
					deleteButton.text = "Confirm";
					return;
				}
				ruleManager?.DeletePreset(preset.Id);
			};
			return row;
		}

		private void CreateRule()
		{
			editingRuleId = FacilityRuleManager.NoRulePresetId;
			workingRule = new FacilityRule();
			workingRuleColor = Color.white;
			ShowRuleEditor("Create Facility Rule", $"Rule {(ruleManager?.Presets.Count ?? 0) + 1}");
		}

		private void CreateColdChainRule()
		{
			if (IsThermalOperationsResearched() == false)
				return;

			editingRuleId = FacilityRuleManager.NoRulePresetId;
			workingRule = new FacilityRule();
			workingRule.ItemRule.SetRequiredItemTags(ItemTag.Food);
			workingRuleColor = new Color(0.25f, 0.75f, 1.0f);
			ShowRuleEditor("Create Cold Chain Rule", "Cold Chain");
		}

		private void EditRule(FacilityRulePreset preset)
		{
			if (preset == null) return;
			editingRuleId = preset.Id;
			workingRule = new FacilityRule(preset.Rule);
			workingRuleColor = preset.Color;
			ShowRuleEditor("Edit Facility Rule", preset.DisplayName);
		}

		private void DuplicateRule(FacilityRulePreset preset)
		{
			if (preset == null || ruleManager == null) return;
			ruleManager.CreatePreset($"{preset.DisplayName} Copy", new FacilityRule(preset.Rule), preset.Color);
		}

		private void OnRuleChanged(FacilityRulePreset _) => RefreshRules();
		private void OnRuleDeleted(uint _) => RefreshRules();
		private void OnRuleApplied(IFacility facility, uint previousPresetId, uint nextPresetId) => RefreshRules();

		private void BindRuleEditor(VisualElement content)
		{
			ruleCapsuleBufferStateField.choices = EnumNames(RuleCapsuleBufferStates);
			ruleCargoProcessStageField.choices = EnumNames(RuleCargoProcessStages);
			ruleItemStatusField.choices = EnumNames(RuleItemStatuses);
			ruleWorkerKindField.choices = EnumNames(RuleWorkerKinds);
			ruleItems.Clear();
			List<string> itemChoices = new();
			ItemDatabase itemDatabase = GameContext.HasInstance ? GameContext.Instance.ItemDB : null;
			if (itemDatabase != null)
			{
				for (int i = 0; itemDatabase.TryGetItemBySortedIndex(i, out ItemDefinition item); ++i)
				{
					ruleItems.Add(item);
					itemChoices.Add($"{item.ItemID} · {item.name}");
				}
			}
			ruleItemField.choices = itemChoices;
			if (itemChoices.Count > 0) ruleItemField.SetValueWithoutNotify(itemChoices[0]);

			rulePriorityField.RegisterValueChangedCallback(evt => { if (!suppressRuleEditorEvents) workingRule.SetPriority(Mathf.Max(0, evt.newValue)); });
			ruleRedSlider.RegisterValueChangedCallback(_ => OnRuleColorChanged());
			ruleGreenSlider.RegisterValueChangedCallback(_ => OnRuleColorChanged());
			ruleBlueSlider.RegisterValueChangedCallback(_ => OnRuleColorChanged());
			ruleCapsuleBufferStateField.RegisterValueChangedCallback(_ =>
			{
				if (!suppressRuleEditorEvents && ruleCapsuleBufferStateField.index >= 0)
					workingRule.SetRequiredCapsuleBufferState(RuleCapsuleBufferStates[ruleCapsuleBufferStateField.index]);
			});
			ruleCargoProcessStageField.RegisterValueChangedCallback(_ =>
			{
				if (!suppressRuleEditorEvents && ruleCargoProcessStageField.index >= 0)
					workingRule.SetRequiredCargoProcessStage(RuleCargoProcessStages[ruleCargoProcessStageField.index]);
			});
			ruleItemStatusField.RegisterValueChangedCallback(_ => { if (!suppressRuleEditorEvents && ruleItemStatusField.index >= 0) workingRule.ItemRule.SetRequiredItemStatus(RuleItemStatuses[ruleItemStatusField.index]); });
			ruleWorkerKindField.RegisterValueChangedCallback(_ => { if (!suppressRuleEditorEvents && ruleWorkerKindField.index >= 0) workingRule.WorkerRule.SetRequiredWorkerKind(RuleWorkerKinds[ruleWorkerKindField.index]); });

			BindFlagToggle(content, "rule-required-tag-fragile", ItemTag.Fragile, true);
			BindFlagToggle(content, "rule-required-tag-food", ItemTag.Food, true);
			BindFlagToggle(content, "rule-required-tag-danger", ItemTag.Danger, true);
			BindFlagToggle(content, "rule-required-tag-electric", ItemTag.Electric, true);
			BindFlagToggle(content, "rule-forbidden-tag-fragile", ItemTag.Fragile, false);
			BindFlagToggle(content, "rule-forbidden-tag-food", ItemTag.Food, false);
			BindFlagToggle(content, "rule-forbidden-tag-danger", ItemTag.Danger, false);
			BindFlagToggle(content, "rule-forbidden-tag-electric", ItemTag.Electric, false);
			BindAbilityToggle(content, "rule-ability-carrybox", WorkerAbility.CarryBox);
			BindAbilityToggle(content, "rule-ability-picking", WorkerAbility.PickingStoring);
			BindAbilityToggle(content, "rule-ability-packing", WorkerAbility.Packing);
			BindAbilityToggle(content, "rule-ability-labeling", WorkerAbility.Labeling);
			BindAbilityToggle(content, "rule-ability-cargo", WorkerAbility.CargoHandling);
			BindHumanToggle(content, "rule-required-human-fulltime", HumanType.FullTime, true);
			BindHumanToggle(content, "rule-required-human-parttime", HumanType.PartTime, true);
			BindHumanToggle(content, "rule-required-human-illegal", HumanType.Illegal, true);
			BindHumanToggle(content, "rule-forbidden-human-fulltime", HumanType.FullTime, false);
			BindHumanToggle(content, "rule-forbidden-human-parttime", HumanType.PartTime, false);
			BindHumanToggle(content, "rule-forbidden-human-illegal", HumanType.Illegal, false);
			BindRobotToggle(content, "rule-required-robot-transfer", true);
			BindRobotToggle(content, "rule-forbidden-robot-transfer", false);
			BindDestinationToggle(content, "rule-destination-none", OrderDestination.None);
			BindDestinationToggle(content, "rule-destination-mars", OrderDestination.Mars);
			BindDestinationToggle(content, "rule-destination-titan", OrderDestination.Titan);

			content.Q<Button>("rule-add-whitelist").clicked += () => AddSelectedRuleItem(true);
			content.Q<Button>("rule-add-blacklist").clicked += () => AddSelectedRuleItem(false);
			content.Q<Button>("rule-clear-whitelist").clicked += () => { workingRule.ItemRule.SetWhiteList(Array.Empty<ItemDefinition>()); RefreshRuleItemLists(); };
			content.Q<Button>("rule-clear-blacklist").clicked += () => { workingRule.ItemRule.SetBlackList(Array.Empty<ItemDefinition>()); RefreshRuleItemLists(); };
			content.Q<Button>("rule-editor-save").clicked += SaveRuleEditor;
			content.Q<Button>("rule-editor-cancel").clicked += ShowRuleLibrary;
		}

		private void ShowRuleEditor(string title, string displayName)
		{
			ruleLibrary.style.display = DisplayStyle.None;
			ruleEditor.style.display = DisplayStyle.Flex;
			ruleEditorTitle.text = title;
			ruleNameField.SetValueWithoutNotify(displayName);
			RefreshRuleEditor();
		}

		private void ShowRuleLibrary()
		{
			if (ruleLibrary == null || ruleEditor == null) return;
			ruleLibrary.style.display = DisplayStyle.Flex;
			ruleEditor.style.display = DisplayStyle.None;
			RefreshRules();
		}

		private void SaveRuleEditor()
		{
			if (ruleManager == null) return;
			string displayName = ruleNameField.value;
			if (editingRuleId == FacilityRuleManager.NoRulePresetId)
				ruleManager.CreatePreset(displayName, workingRule, workingRuleColor);
			else
			{
				ruleManager.RenamePreset(editingRuleId, displayName);
				ruleManager.SetPresetColor(editingRuleId, workingRuleColor);
				ruleManager.SetPresetRule(editingRuleId, workingRule);
			}
			ShowRuleLibrary();
		}

		private void RefreshRuleEditor()
		{
			suppressRuleEditorEvents = true;
			rulePriorityField.SetValueWithoutNotify(workingRule.Priority);
			ruleRedSlider.SetValueWithoutNotify(workingRuleColor.r);
			ruleGreenSlider.SetValueWithoutNotify(workingRuleColor.g);
			ruleBlueSlider.SetValueWithoutNotify(workingRuleColor.b);
			ruleColorPreview.style.backgroundColor = workingRuleColor;
			ruleCapsuleBufferStateField.SetValueWithoutNotify(workingRule.RequiredCapsuleBufferState.ToString());
			ruleCargoProcessStageField.SetValueWithoutNotify(workingRule.RequiredCargoProcessStage.ToString());
			ruleItemStatusField.SetValueWithoutNotify(workingRule.ItemRule.RequiredItemStatus.ToString());
			ruleWorkerKindField.SetValueWithoutNotify(workingRule.WorkerRule.RequiredWorkerKind.ToString());
			SetToggle("rule-required-tag-fragile", workingRule.ItemRule.RequiredItemTags.HasFlag(ItemTag.Fragile));
			SetToggle("rule-required-tag-food", workingRule.ItemRule.RequiredItemTags.HasFlag(ItemTag.Food));
			SetToggle("rule-required-tag-danger", workingRule.ItemRule.RequiredItemTags.HasFlag(ItemTag.Danger));
			SetToggle("rule-required-tag-electric", workingRule.ItemRule.RequiredItemTags.HasFlag(ItemTag.Electric));
			SetToggle("rule-forbidden-tag-fragile", workingRule.ItemRule.ForbiddenItemTags.HasFlag(ItemTag.Fragile));
			SetToggle("rule-forbidden-tag-food", workingRule.ItemRule.ForbiddenItemTags.HasFlag(ItemTag.Food));
			SetToggle("rule-forbidden-tag-danger", workingRule.ItemRule.ForbiddenItemTags.HasFlag(ItemTag.Danger));
			SetToggle("rule-forbidden-tag-electric", workingRule.ItemRule.ForbiddenItemTags.HasFlag(ItemTag.Electric));
			SetToggle("rule-ability-carrybox", workingRule.WorkerRule.RequiredWorkerAbility.HasFlag(WorkerAbility.CarryBox));
			SetToggle("rule-ability-picking", workingRule.WorkerRule.RequiredWorkerAbility.HasFlag(WorkerAbility.PickingStoring));
			SetToggle("rule-ability-packing", workingRule.WorkerRule.RequiredWorkerAbility.HasFlag(WorkerAbility.Packing));
			SetToggle("rule-ability-labeling", workingRule.WorkerRule.RequiredWorkerAbility.HasFlag(WorkerAbility.Labeling));
			SetToggle("rule-ability-cargo", workingRule.WorkerRule.RequiredWorkerAbility.HasFlag(WorkerAbility.CargoHandling));
			SetToggle("rule-required-human-fulltime", workingRule.WorkerRule.RequiredHumanTypes.Contains(HumanType.FullTime));
			SetToggle("rule-required-human-parttime", workingRule.WorkerRule.RequiredHumanTypes.Contains(HumanType.PartTime));
			SetToggle("rule-required-human-illegal", workingRule.WorkerRule.RequiredHumanTypes.Contains(HumanType.Illegal));
			SetToggle("rule-forbidden-human-fulltime", workingRule.WorkerRule.ForbiddenHumanTypes.Contains(HumanType.FullTime));
			SetToggle("rule-forbidden-human-parttime", workingRule.WorkerRule.ForbiddenHumanTypes.Contains(HumanType.PartTime));
			SetToggle("rule-forbidden-human-illegal", workingRule.WorkerRule.ForbiddenHumanTypes.Contains(HumanType.Illegal));
			SetToggle("rule-required-robot-transfer", workingRule.WorkerRule.RequiredRobotTypes.Contains(RobotType.Transfer));
			SetToggle("rule-forbidden-robot-transfer", workingRule.WorkerRule.ForbiddenRobotTypes.Contains(RobotType.Transfer));
			SetToggle("rule-destination-none", workingRule.ManifestRule.RequiredDestinations.Contains(OrderDestination.None));
			SetToggle("rule-destination-mars", workingRule.ManifestRule.RequiredDestinations.Contains(OrderDestination.Mars));
			SetToggle("rule-destination-titan", workingRule.ManifestRule.RequiredDestinations.Contains(OrderDestination.Titan));
			suppressRuleEditorEvents = false;
			RefreshRuleItemLists();
		}

		private void OnRuleColorChanged()
		{
			if (suppressRuleEditorEvents) return;
			workingRuleColor = new Color(ruleRedSlider.value, ruleGreenSlider.value, ruleBlueSlider.value, 1f);
			ruleColorPreview.style.backgroundColor = workingRuleColor;
		}

		private void BindFlagToggle(VisualElement root, string name, ItemTag tag, bool required)
		{
			root.Q<Toggle>(name).RegisterValueChangedCallback(evt =>
			{
				if (suppressRuleEditorEvents) return;
				if (required)
				{
					ItemTag value = workingRule.ItemRule.RequiredItemTags;
					workingRule.ItemRule.SetRequiredItemTags(evt.newValue ? value | tag : value & ~tag);
				}
				else
				{
					ItemTag value = workingRule.ItemRule.ForbiddenItemTags;
					workingRule.ItemRule.SetForbiddenItemTags(evt.newValue ? value | tag : value & ~tag);
				}
			});
		}

		private void BindAbilityToggle(VisualElement root, string name, WorkerAbility ability)
		{
			root.Q<Toggle>(name).RegisterValueChangedCallback(evt =>
			{
				if (suppressRuleEditorEvents) return;
				WorkerAbility value = workingRule.WorkerRule.RequiredWorkerAbility;
				workingRule.WorkerRule.SetRequiredWorkerAbility(evt.newValue ? value | ability : value & ~ability);
			});
		}

		private void BindHumanToggle(VisualElement root, string name, HumanType type, bool required)
		{
			root.Q<Toggle>(name).RegisterValueChangedCallback(evt =>
			{
				if (suppressRuleEditorEvents) return;
				List<HumanType> values = new(required ? workingRule.WorkerRule.RequiredHumanTypes : workingRule.WorkerRule.ForbiddenHumanTypes);
				SetListValue(values, type, evt.newValue);
				if (required) workingRule.WorkerRule.SetRequiredHumanTypes(values);
				else workingRule.WorkerRule.SetForbiddenHumanTypes(values);
			});
		}

		private void BindRobotToggle(VisualElement root, string name, bool required)
		{
			root.Q<Toggle>(name).RegisterValueChangedCallback(evt =>
			{
				if (suppressRuleEditorEvents) return;
				List<RobotType> values = new(required ? workingRule.WorkerRule.RequiredRobotTypes : workingRule.WorkerRule.ForbiddenRobotTypes);
				SetListValue(values, RobotType.Transfer, evt.newValue);
				if (required) workingRule.WorkerRule.SetRequiredRobotTypes(values);
				else workingRule.WorkerRule.SetForbiddenRobotTypes(values);
			});
		}

		private void BindDestinationToggle(VisualElement root, string name, OrderDestination destination)
		{
			root.Q<Toggle>(name).RegisterValueChangedCallback(evt =>
			{
				if (suppressRuleEditorEvents) return;
				List<OrderDestination> values = new(workingRule.ManifestRule.RequiredDestinations);
				SetListValue(values, destination, evt.newValue);
				workingRule.ManifestRule.SetRequiredDestinations(values);
			});
		}

		private void AddSelectedRuleItem(bool whiteList)
		{
			if (ruleItemField.index < 0 || ruleItemField.index >= ruleItems.Count) return;
			ItemDefinition item = ruleItems[ruleItemField.index];
			List<ItemDefinition> values = new(whiteList ? workingRule.ItemRule.WhiteList : workingRule.ItemRule.BlackList);
			if (values.Contains(item) == false) values.Add(item);
			if (whiteList) workingRule.ItemRule.SetWhiteList(values);
			else workingRule.ItemRule.SetBlackList(values);
			RefreshRuleItemLists();
		}

		private void RefreshRuleItemLists()
		{
			whiteListSummary.text = BuildItemSummary("White List", workingRule.ItemRule.WhiteList, "Any");
			blackListSummary.text = BuildItemSummary("Black List", workingRule.ItemRule.BlackList, "None");
		}

		private void SetToggle(string name, bool value) => ruleEditor.Q<Toggle>(name).SetValueWithoutNotify(value);

		private static string BuildItemSummary(string label, IReadOnlyList<ItemDefinition> items, string empty)
		{
			if (items == null || items.Count == 0) return $"{label}: {empty}";
			return $"{label}: {string.Join(", ", items.Where(item => item != null).Select(item => item.name))}";
		}

		private static List<string> EnumNames<T>(IReadOnlyList<T> values)
		{
			List<string> names = new();
			for (int i = 0; i < values.Count; ++i) names.Add(values[i].ToString());
			return names;
		}

		private static void SetListValue<T>(List<T> values, T value, bool enabled)
		{
			if (enabled)
			{
				if (values.Contains(value) == false) values.Add(value);
			}
			else values.Remove(value);
		}

		private void BeginApplyMode(uint presetId)
		{
			if (ruleManager == null || GameContext.HasInstance == false || GameContext.Instance.InteractionCtx == null) return;
			EndApplyMode();
			GameContext.Instance.InteractionCtx.ExitBuildingMode();
			applyingRuleId = presetId;
			applyModeActive = true;
			GameContext.Instance.InteractionCtx.OnItemSelected += OnApplyTargetSelected;
			GameContext.Instance.GridOverlay.SetFacilityRuleApplyVisible(true);
			window.Close();
		}

		private void EndApplyMode()
		{
			if (GameContext.HasInstance)
				GameContext.Instance.GridOverlay.SetFacilityRuleApplyVisible(false);

			if (applyModeActive == false) return;
			if (GameContext.HasInstance && GameContext.Instance.InteractionCtx != null)
				GameContext.Instance.InteractionCtx.OnItemSelected -= OnApplyTargetSelected;
			applyModeActive = false;
			applyingRuleId = FacilityRuleManager.NoRulePresetId;
		}

		private void OnApplyTargetSelected(GameObject selectedObject)
		{
			if (TryGetFacility(selectedObject, out IFacility facility) == false)
			{
				GameContext.Instance.FloatingTextManager?.ShowScreen(FloatingTextPreset.Error, "Select a Facility", Input.mousePosition);
				return;
			}
			if (ruleManager.ApplyPreset(facility, applyingRuleId))
				Debug.Log($"Applied Facility Rule preset {applyingRuleId} to {selectedObject.name}.");
		}

		private static bool TryGetFacility(GameObject selectedObject, out IFacility facility)
		{
			facility = null;
			if (selectedObject == null) return false;
			foreach (Component component in selectedObject.GetComponents<Component>())
			{
				if (component is not IFacility found) continue;
				facility = found;
				return true;
			}
			return false;
		}

		private static string BuildRuleSummary(FacilityRule rule)
		{
			if (rule == null || rule.IsEmpty) return "No restrictions";
			List<string> parts = new();
			if (rule.RequiredCapsuleBufferState != CapsuleBufferStateRequirement.Any) parts.Add($"Capsule {rule.RequiredCapsuleBufferState}");
			if (rule.RequiredCargoProcessStage != CargoProcessStage.None) parts.Add(CargoProcessStageUtility.ToDisplayString(rule.RequiredCargoProcessStage));
			if (rule.ItemRule != null && rule.ItemRule.IsEmpty == false) parts.Add("Item");
			if (rule.WorkerRule != null && rule.WorkerRule.IsEmpty == false) parts.Add("Worker");
			if (rule.ManifestRule != null && rule.ManifestRule.IsEmpty == false) parts.Add("Destination");
			return string.Join(" · ", parts);
		}

		private void OnMoneyChanged(int _) => DisplaySelectedSection();
		private void OnResearchStateChanged()
		{
			RefreshCategories();
			DisplaySelectedSection();
			RefreshRules();
		}

		private BuildingFootprintPreset GetSelectedPreset() => footprintField.index >= 0 && footprintField.index < footprintPresets.Count ? footprintPresets[footprintField.index] : footprintService?.ActivePreset;
		private static string GetPlaceableName(PlaceableDefinition definition) => string.IsNullOrWhiteSpace(definition.displayName) ? definition.name : definition.displayName;
		private static string GetSectionName(BuildPlaceableSection section) => string.IsNullOrWhiteSpace(section.displayName) ? section.sectionId : section.displayName;
		private static string GetEnvironmentName(PlacementEnvironmentRequirement environment)
		{
			bool indoor = environment.HasFlag(PlacementEnvironmentRequirement.Indoor);
			bool outdoor = environment.HasFlag(PlacementEnvironmentRequirement.Outdoor);
			if (indoor && outdoor) return "Any";
			if (indoor) return "Indoor";
			if (outdoor) return "Outdoor";
			return "Restricted";
		}
		private bool IsPlaceableUnlocked(PlaceableDefinition definition)
		{
			return definition != null &&
				(definition.RequiresResearch == false ||
					researchService?.IsResearched(definition.RequiredResearchUid) == true);
		}
		private bool IsThermalOperationsResearched() =>
			researchService?.IsResearched(ResearchIds.ThermalOperations) == true;
		private bool IsSectionVisible(BuildPlaceableSection section)
		{
			if (section?.placeables == null) return false;
			for (int i = 0; i < section.placeables.Count; ++i)
			{
				PlaceableDefinition definition = section.placeables[i];
				if (definition != null && definition.prefab != null && definition.gridFootprint != null &&
					IsPlaceableUnlocked(definition))
					return true;
			}
			return false;
		}
		private bool ContainsVisibleSection(IReadOnlyList<BuildPlaceableSection> sections, BuildPlaceableSection target)
		{
			if (sections == null || target == null) return false;
			for (int i = 0; i < sections.Count; ++i)
				if (sections[i] == target && IsSectionVisible(sections[i])) return true;
			return false;
		}
		private BuildPlaceableSection FirstVisibleSection(IReadOnlyList<BuildPlaceableSection> sections)
		{
			if (sections == null) return null;
			for (int i = 0; i < sections.Count; ++i)
				if (IsSectionVisible(sections[i])) return sections[i];
			return null;
		}
	}
}
