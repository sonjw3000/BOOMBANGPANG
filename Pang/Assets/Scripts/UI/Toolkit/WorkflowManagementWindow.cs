using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class WorkflowManagementWindow : MonoBehaviour
	{
		private enum WorkflowTab
		{
			Monitor,
			Inbound,
			Outbound,
			Policy,
		}

		private const string SelectedTabClass = "workflow-tab-button--selected";
		private readonly List<CollectingPolicyType> storingCollectingPolicies = new();
		private readonly List<PlacingPolicyType> storingPlacingPolicies = new();
		private readonly List<PickingPolicyType> pickingPolicies = new();
		private readonly List<CollectingPolicyType> pickingCollectingPolicies = new();

		private UIWindow window;
		private VisualTreeAsset contentTemplate;
		private VisualTreeAsset landingAreaRowTemplate;
		private BuildManagementWindow buildManagementWindow;
		private Button monitorButton;
		private Button inboundButton;
		private Button outboundButton;
		private VisualElement policyButtonControl;
		private Button policyButton;
		private VisualElement monitorTab;
		private VisualElement inboundTab;
		private VisualElement outboundTab;
		private VisualElement policyTab;
		private Button addLandingAreaButton;
		private Button linkLandingAreaButton;
		private ScrollView landingAreaList;
		private Label landingAreaEmpty;
		private Label unloadingDestinationSummary;
		private VisualElement collectingPolicyControl;
		private DropdownField collectingPolicyField;
		private VisualElement placingPolicyControl;
		private DropdownField placingPolicyField;
		private VisualElement storingBoxFillControl;
		private Slider storingBoxFillSlider;
		private Label storingBoxFillValue;
		private VisualElement inboundQualityControl;
		private Toggle inboundQualityControlEnabled;
		private Slider inboundQualityFreshnessSlider;
		private Label inboundQualityFreshnessValue;
		private Slider inboundQualityDamageSlider;
		private Label inboundQualityDamageValue;
		private VisualElement pickingPolicyControl;
		private DropdownField pickingPolicyField;
		private VisualElement pickingCollectingPolicyControl;
		private DropdownField pickingCollectingPolicyField;
		private VisualElement pickingBoxFillControl;
		private Slider pickingBoxFillSlider;
		private Label pickingBoxFillValue;
		private VisualElement outboundQualityControl;
		private Toggle outboundQualityControlEnabled;
		private Slider outboundQualityFreshnessSlider;
		private Label outboundQualityFreshnessValue;
		private Slider outboundQualityDamageSlider;
		private Label outboundQualityDamageValue;
		private Button linkOutboundDestinationButton;
		private Label loadingDestinationSummary;
		private Label messageLabel;
		private AreaManager areaManager;
		private BuildingManager buildingManager;
		private InboundWorkflowService inboundWorkflow;
		private OutboundWorkflowService outboundWorkflow;
		private ResearchService researchService;
		private LogisticsWorkMonitorPresenter workloadPresenter;
		private WorkflowTab selectedTab = WorkflowTab.Monitor;
		[System.NonSerialized] private bool initialized;
		private bool started;

		public void Configure(UIWindow targetWindow, VisualTreeAsset targetContentTemplate,
			VisualTreeAsset targetLandingAreaRowTemplate, BuildManagementWindow targetBuildManagementWindow)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
			landingAreaRowTemplate = targetLandingAreaRowTemplate;
			buildManagementWindow = targetBuildManagementWindow;
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

		private void OnDisable()
		{
			workloadPresenter?.SetActive(false);
			UnbindControls();
			UnbindServices();
			initialized = false;
		}

		private void OnDestroy()
		{
			workloadPresenter?.Dispose();
			workloadPresenter = null;
		}

		public void Open()
		{
			if (InitializeView() == false) return;
			if (areaManager == null) BindServices();
			RefreshAll();
			window.Open();
		}

		public void OpenMonitor()
		{
			if (InitializeView() == false) return;
			if (areaManager == null) BindServices();
			SelectTab(WorkflowTab.Monitor);
			RefreshAll();
			window.Open();
		}

		private bool InitializeView()
		{
			if (initialized) return true;
			if (window == null || contentTemplate == null || landingAreaRowTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[WorkflowManagementWindow] Window or templates are missing.", this);
				return false;
			}

			TemplateContainer content = contentTemplate.CloneTree();
			monitorButton = content.Q<Button>("workflow-monitor-button");
			inboundButton = content.Q<Button>("workflow-inbound-button");
			outboundButton = content.Q<Button>("workflow-outbound-button");
			policyButtonControl = content.Q<VisualElement>("workflow-policy-button-control");
			policyButton = content.Q<Button>("workflow-policy-button");
			monitorTab = content.Q<VisualElement>("workflow-monitor-tab");
			inboundTab = content.Q<VisualElement>("workflow-inbound-tab");
			outboundTab = content.Q<VisualElement>("workflow-outbound-tab");
			policyTab = content.Q<VisualElement>("workflow-policy-tab");
			addLandingAreaButton = content.Q<Button>("workflow-add-landing-area-button");
			linkLandingAreaButton = content.Q<Button>("workflow-link-landing-area-button");
			landingAreaList = content.Q<ScrollView>("workflow-landing-area-list");
			landingAreaEmpty = content.Q<Label>("workflow-landing-area-empty");
			unloadingDestinationSummary = content.Q<Label>("workflow-unloading-destination");
			collectingPolicyControl = content.Q<VisualElement>("workflow-collecting-policy-control");
			collectingPolicyField = content.Q<DropdownField>("workflow-collecting-policy");
			placingPolicyControl = content.Q<VisualElement>("workflow-placing-policy-control");
			placingPolicyField = content.Q<DropdownField>("workflow-placing-policy");
			storingBoxFillControl = content.Q<VisualElement>("workflow-storing-box-fill-control");
			storingBoxFillSlider = content.Q<Slider>("workflow-storing-box-fill");
			storingBoxFillValue = content.Q<Label>("workflow-storing-box-fill-value");
			inboundQualityControl = content.Q<VisualElement>("workflow-inbound-qc-control");
			inboundQualityControlEnabled = content.Q<Toggle>("workflow-inbound-qc-enabled");
			inboundQualityFreshnessSlider = content.Q<Slider>("workflow-inbound-qc-freshness");
			inboundQualityFreshnessValue = content.Q<Label>("workflow-inbound-qc-freshness-value");
			inboundQualityDamageSlider = content.Q<Slider>("workflow-inbound-qc-damage");
			inboundQualityDamageValue = content.Q<Label>("workflow-inbound-qc-damage-value");
			pickingPolicyControl = content.Q<VisualElement>("workflow-picking-policy-control");
			pickingPolicyField = content.Q<DropdownField>("workflow-picking-policy");
			pickingCollectingPolicyControl = content.Q<VisualElement>("workflow-picking-collecting-policy-control");
			pickingCollectingPolicyField = content.Q<DropdownField>("workflow-picking-collecting-policy");
			pickingBoxFillControl = content.Q<VisualElement>("workflow-picking-box-fill-control");
			pickingBoxFillSlider = content.Q<Slider>("workflow-picking-box-fill");
			pickingBoxFillValue = content.Q<Label>("workflow-picking-box-fill-value");
			outboundQualityControl = content.Q<VisualElement>("workflow-outbound-qc-control");
			outboundQualityControlEnabled = content.Q<Toggle>("workflow-outbound-qc-enabled");
			outboundQualityFreshnessSlider = content.Q<Slider>("workflow-outbound-qc-freshness");
			outboundQualityFreshnessValue = content.Q<Label>("workflow-outbound-qc-freshness-value");
			outboundQualityDamageSlider = content.Q<Slider>("workflow-outbound-qc-damage");
			outboundQualityDamageValue = content.Q<Label>("workflow-outbound-qc-damage-value");
			linkOutboundDestinationButton = content.Q<Button>("workflow-link-outbound-destination-button");
			loadingDestinationSummary = content.Q<Label>("workflow-loading-destination");
			messageLabel = content.Q<Label>("workflow-message");

			if (monitorButton == null || inboundButton == null || outboundButton == null ||
				policyButtonControl == null || policyButton == null ||
				monitorTab == null || inboundTab == null || outboundTab == null || policyTab == null ||
				addLandingAreaButton == null || linkLandingAreaButton == null || landingAreaList == null ||
				landingAreaEmpty == null || unloadingDestinationSummary == null || collectingPolicyControl == null ||
				collectingPolicyField == null || placingPolicyControl == null || placingPolicyField == null ||
				storingBoxFillControl == null || storingBoxFillSlider == null || storingBoxFillValue == null ||
				inboundQualityControl == null || inboundQualityControlEnabled == null ||
				inboundQualityFreshnessSlider == null || inboundQualityFreshnessValue == null ||
				inboundQualityDamageSlider == null || inboundQualityDamageValue == null ||
				pickingPolicyControl == null || pickingPolicyField == null ||
				pickingCollectingPolicyControl == null || pickingCollectingPolicyField == null ||
				pickingBoxFillControl == null || pickingBoxFillSlider == null || pickingBoxFillValue == null ||
				outboundQualityControl == null || outboundQualityControlEnabled == null ||
				outboundQualityFreshnessSlider == null || outboundQualityFreshnessValue == null ||
				outboundQualityDamageSlider == null || outboundQualityDamageValue == null ||
				linkOutboundDestinationButton == null || loadingDestinationSummary == null ||
				messageLabel == null)
			{
				Debug.LogError("[WorkflowManagementWindow] Required UXML elements are missing.", this);
				return false;
			}

			workloadPresenter ??= new LogisticsWorkMonitorPresenter();
			if (workloadPresenter.BindView(content) == false)
			{
				Debug.LogError("[WorkflowManagementWindow] Workflow Monitor UXML elements are missing.", this);
				return false;
			}

			collectingPolicyControl.SetTooltip(BuildStoringCollectingPolicyTooltip);
			placingPolicyControl.SetTooltip(BuildStoringPlacingPolicyTooltip);
			storingBoxFillControl.SetTooltip(BuildStoringBoxFillTooltip);
			inboundQualityControl.SetTooltip(BuildInboundQualityControlTooltip);
			pickingPolicyControl.SetTooltip(BuildPickingPolicyTooltip);
			pickingCollectingPolicyControl.SetTooltip(BuildPickingCollectingPolicyTooltip);
			pickingBoxFillControl.SetTooltip(BuildPickingBoxFillTooltip);
			outboundQualityControl.SetTooltip(BuildOutboundQualityControlTooltip);
			policyButtonControl.SetTooltip(BuildPolicyTabTooltip);
			window.SetTitle("Workflow Management");
			window.SetContent(content);
			window.Opened -= OnWindowOpened;
			window.Opened += OnWindowOpened;
			window.Closed -= OnWindowClosed;
			window.Closed += OnWindowClosed;
			monitorButton.clicked += SelectMonitor;
			inboundButton.clicked += OpenInbound;
			outboundButton.clicked += OpenOutbound;
			policyButton.clicked += OpenPolicy;
			addLandingAreaButton.clicked += BeginLandingAreaCreation;
			linkLandingAreaButton.clicked += BeginLandingDestinationLink;
			linkOutboundDestinationButton.clicked += BeginOutboundDestinationLink;
			collectingPolicyField.RegisterValueChangedCallback(OnCollectingPolicyChanged);
			placingPolicyField.RegisterValueChangedCallback(OnPlacingPolicyChanged);
			pickingPolicyField.RegisterValueChangedCallback(OnPickingPolicyChanged);
			pickingCollectingPolicyField.RegisterValueChangedCallback(OnPickingCollectingPolicyChanged);
			storingBoxFillSlider.RegisterValueChangedCallback(OnStoringBoxFillChanged);
			pickingBoxFillSlider.RegisterValueChangedCallback(OnPickingBoxFillChanged);
			inboundQualityControlEnabled.RegisterValueChangedCallback(OnInboundQualityControlChanged);
			inboundQualityFreshnessSlider.RegisterValueChangedCallback(OnInboundQualityFreshnessChanged);
			inboundQualityDamageSlider.RegisterValueChangedCallback(OnInboundQualityDamageChanged);
			outboundQualityControlEnabled.RegisterValueChangedCallback(OnOutboundQualityControlChanged);
			outboundQualityFreshnessSlider.RegisterValueChangedCallback(OnOutboundQualityFreshnessChanged);
			outboundQualityDamageSlider.RegisterValueChangedCallback(OnOutboundQualityDamageChanged);
			initialized = true;
			SelectTab(WorkflowTab.Monitor);
			return true;
		}

		private void UnbindControls()
		{
			workloadPresenter?.UnbindView();
			if (window != null)
			{
				window.Opened -= OnWindowOpened;
				window.Closed -= OnWindowClosed;
			}
			if (monitorButton != null) monitorButton.clicked -= SelectMonitor;
			if (inboundButton != null) inboundButton.clicked -= OpenInbound;
			if (outboundButton != null) outboundButton.clicked -= OpenOutbound;
			if (policyButton != null) policyButton.clicked -= OpenPolicy;
			if (addLandingAreaButton != null) addLandingAreaButton.clicked -= BeginLandingAreaCreation;
			if (linkLandingAreaButton != null) linkLandingAreaButton.clicked -= BeginLandingDestinationLink;
			if (linkOutboundDestinationButton != null) linkOutboundDestinationButton.clicked -= BeginOutboundDestinationLink;
			collectingPolicyField?.UnregisterValueChangedCallback(OnCollectingPolicyChanged);
			placingPolicyField?.UnregisterValueChangedCallback(OnPlacingPolicyChanged);
			pickingPolicyField?.UnregisterValueChangedCallback(OnPickingPolicyChanged);
			pickingCollectingPolicyField?.UnregisterValueChangedCallback(OnPickingCollectingPolicyChanged);
			storingBoxFillSlider?.UnregisterValueChangedCallback(OnStoringBoxFillChanged);
			pickingBoxFillSlider?.UnregisterValueChangedCallback(OnPickingBoxFillChanged);
			inboundQualityControlEnabled?.UnregisterValueChangedCallback(OnInboundQualityControlChanged);
			inboundQualityFreshnessSlider?.UnregisterValueChangedCallback(OnInboundQualityFreshnessChanged);
			inboundQualityDamageSlider?.UnregisterValueChangedCallback(OnInboundQualityDamageChanged);
			outboundQualityControlEnabled?.UnregisterValueChangedCallback(OnOutboundQualityControlChanged);
			outboundQualityFreshnessSlider?.UnregisterValueChangedCallback(OnOutboundQualityFreshnessChanged);
			outboundQualityDamageSlider?.UnregisterValueChangedCallback(OnOutboundQualityDamageChanged);
		}

		private void BindServices()
		{
			UnbindServices();
			if (GameContext.HasInstance == false) return;
			areaManager = GameContext.Instance.AreaMgr;
			buildingManager = GameContext.Instance.BuildingMgr;
			inboundWorkflow = GameContext.Instance.IBWorkflowSvc;
			outboundWorkflow = GameContext.Instance.OBWorkflowSvc;
			researchService = GameContext.Instance.ResearchService;
			workloadPresenter?.BindSources(
				GameContext.Instance.Metrics,
				GameContext.Instance.TaskMgr,
				GameContext.Instance.WorkerMgr,
				GameContext.Instance.OrderMgr,
				GameContext.Instance.BuildingMgr,
				GameContext.Instance.CapsuleDockSvc,
				GameContext.Instance.GameTime);
			if (areaManager != null)
			{
				areaManager.OnAreaAdded += OnAreaChanged;
				areaManager.OnAreaRemoved += OnAreaChanged;
				areaManager.OnAreaChanged += OnAreaChanged;
				areaManager.OnAreasRebuilt += RefreshLandingAreas;
			}
			if (researchService != null)
				researchService.OnResearchStateChanged += RefreshPolicies;
		}

		private void UnbindServices()
		{
			workloadPresenter?.UnbindSources();
			if (areaManager != null)
			{
				areaManager.OnAreaAdded -= OnAreaChanged;
				areaManager.OnAreaRemoved -= OnAreaChanged;
				areaManager.OnAreaChanged -= OnAreaChanged;
				areaManager.OnAreasRebuilt -= RefreshLandingAreas;
			}
			if (researchService != null)
				researchService.OnResearchStateChanged -= RefreshPolicies;
			areaManager = null;
			buildingManager = null;
			inboundWorkflow = null;
			outboundWorkflow = null;
			researchService = null;
		}

		private void SelectMonitor() => SelectTab(WorkflowTab.Monitor);
		private void OpenInbound() => SelectTab(WorkflowTab.Inbound);
		private void OpenOutbound() => SelectTab(WorkflowTab.Outbound);
		private void OpenPolicy()
		{
			if (IsResearchCompleted(ResearchIds.WorkflowPolicyManagement))
				SelectTab(WorkflowTab.Policy);
		}

		private void SelectTab(WorkflowTab tab)
		{
			selectedTab = tab;
			bool monitor = tab == WorkflowTab.Monitor;
			bool inbound = tab == WorkflowTab.Inbound;
			bool outbound = tab == WorkflowTab.Outbound;
			bool policy = tab == WorkflowTab.Policy;
			monitorTab.style.display = monitor ? DisplayStyle.Flex : DisplayStyle.None;
			inboundTab.style.display = inbound ? DisplayStyle.Flex : DisplayStyle.None;
			outboundTab.style.display = outbound ? DisplayStyle.Flex : DisplayStyle.None;
			policyTab.style.display = policy ? DisplayStyle.Flex : DisplayStyle.None;
			monitorButton.EnableInClassList(SelectedTabClass, monitor);
			inboundButton.EnableInClassList(SelectedTabClass, inbound);
			outboundButton.EnableInClassList(SelectedTabClass, outbound);
			policyButton.EnableInClassList(SelectedTabClass, policy);
			workloadPresenter?.SetActive(monitor && window != null && window.IsOpen);
			messageLabel.text = string.Empty;
		}

		private void OnWindowOpened()
		{
			workloadPresenter?.SetActive(selectedTab == WorkflowTab.Monitor);
		}

		private void OnWindowClosed()
		{
			workloadPresenter?.SetActive(false);
		}

		private void RefreshAll()
		{
			workloadPresenter?.Refresh();
			RefreshLandingAreas();
			RefreshDestinations();
			RefreshPolicies();
		}

		private void RefreshLandingAreas()
		{
			if (landingAreaList == null || landingAreaEmpty == null) return;
			landingAreaList.Clear();
			int count = 0;
			IReadOnlyList<Area> areas = areaManager?.RegisteredAreas;
			if (areas != null)
			{
				for (int i = 0; i < areas.Count; ++i)
				{
					Area area = areas[i];
					if (area == null || area.Type != AreaType.RocketLanding) continue;
					landingAreaList.Add(CreateLandingAreaRow(area));
					count += 1;
				}
			}

			landingAreaEmpty.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			linkLandingAreaButton.SetEnabled(count > 0 && buildManagementWindow != null);
			RefreshInboundDestinationSummary();
		}

		private VisualElement CreateLandingAreaRow(Area area)
		{
			TemplateContainer row = landingAreaRowTemplate.CloneTree();
			row.Q<Label>("workflow-area-name").text = area.DisplayName;
			RectInt bounds = area.Bounds;
			row.Q<Label>("workflow-area-bounds").text = $"{bounds.width} × {bounds.height} · ({bounds.xMin}, {bounds.yMin})";
			row.Q<Label>("workflow-area-floor").text = $"Floor {area.Floor}";
			row.Q<Label>("workflow-area-destination").text = area.DestinationBuildingId != 0 && buildingManager != null &&
				buildingManager.TryGetBuilding(area.DestinationBuildingId, out Building destination) && destination != null
				? $"→ {destination.DisplayName}"
				: "Not linked";
			Button removeButton = row.Q<Button>("workflow-area-remove");
			removeButton.clicked += () => RemoveLandingArea(area);
			return row;
		}

		private void RefreshDestinations()
		{
			RefreshInboundDestinationSummary();

			if (loadingDestinationSummary != null)
			{
				loadingDestinationSummary.text = outboundWorkflow != null &&
					outboundWorkflow.TryGetLoadingDestinationBuilding(out Building outboundBuilding) && outboundBuilding != null
					? $"Outbound loading → {outboundBuilding.DisplayName}"
					: "Automatic · nearest valid launch destination";
			}
		}

		private void RefreshInboundDestinationSummary()
		{
			if (unloadingDestinationSummary == null) return;
			int total = 0;
			int linked = 0;
			IReadOnlyList<Area> areas = areaManager?.RegisteredAreas;
			if (areas != null)
			{
				for (int i = 0; i < areas.Count; ++i)
				{
					Area area = areas[i];
					if (area == null || area.Type != AreaType.RocketLanding) continue;
					total += 1;
					if (area.DestinationBuildingId != 0) linked += 1;
				}
			}
			unloadingDestinationSummary.text = $"{linked} / {total} Landing Areas linked";
		}

		private void RefreshPolicies()
		{
			if (policyButton == null) return;

			bool policyUnlocked = IsResearchCompleted(ResearchIds.WorkflowPolicyManagement);
			bool optimizationUnlocked = IsResearchCompleted(ResearchIds.WorkflowPolicyOptimization);
			bool inventoryUnlocked = IsResearchCompleted(ResearchIds.InventoryDigitization);
			bool qualityControlUnlocked = IsResearchCompleted(ResearchIds.QualityControl);
			policyButton.SetEnabled(policyUnlocked);
			if (policyUnlocked == false && policyButton.ClassListContains(SelectedTabClass))
				SelectTab(WorkflowTab.Inbound);

			storingCollectingPolicies.Clear();
			storingCollectingPolicies.Add(CollectingPolicyType.Nearest);
			List<string> storingCollectingChoices = new() { "Manual · nearest capsule" };
			if (optimizationUnlocked)
			{
				storingCollectingPolicies.Add(CollectingPolicyType.LargestQuantityNearest);
				storingCollectingChoices.Add("Largest quantity + nearest");
			}
			SetPolicyChoices(collectingPolicyField, storingCollectingPolicies, storingCollectingChoices,
				inboundWorkflow != null ? inboundWorkflow.StoringCollectingPolicyType : CollectingPolicyType.Nearest);

			storingPlacingPolicies.Clear();
			storingPlacingPolicies.Add(PlacingPolicyType.Nearest);
			List<string> storingPlacingChoices = new() { "Manual · nearest shelf" };
			if (optimizationUnlocked)
			{
				storingPlacingPolicies.Add(PlacingPolicyType.BelowAverageFilledNearest);
				storingPlacingChoices.Add("Below average filled + nearest");
			}
			SetPolicyChoices(placingPolicyField, storingPlacingPolicies, storingPlacingChoices,
				inboundWorkflow != null ? inboundWorkflow.StoringPlacingPolicyType : PlacingPolicyType.Nearest);

			pickingPolicies.Clear();
			pickingPolicies.Add(PickingPolicyType.ManualShelfScan);
			List<string> pickingChoices = new() { "Manual shelf scan" };
			if (inventoryUnlocked)
			{
				pickingPolicies.Add(PickingPolicyType.InventoryGuided);
				pickingChoices.Add("Inventory-guided");
			}
			PickingPolicyType currentPickingPolicy = outboundWorkflow != null
				? outboundWorkflow.PickingPolicyType
				: PickingPolicyType.ManualShelfScan;
			SetPolicyChoices(pickingPolicyField, pickingPolicies, pickingChoices, currentPickingPolicy);

			pickingCollectingPolicies.Clear();
			pickingCollectingPolicies.Add(CollectingPolicyType.Nearest);
			List<string> pickingCollectingChoices = new() { "Nearest known shelf" };
			if (optimizationUnlocked)
			{
				pickingCollectingPolicies.Add(CollectingPolicyType.LargestQuantityNearest);
				pickingCollectingChoices.Add("Largest quantity + nearest");
			}
			SetPolicyChoices(pickingCollectingPolicyField, pickingCollectingPolicies, pickingCollectingChoices,
				outboundWorkflow != null ? outboundWorkflow.PickingCollectingPolicyType : CollectingPolicyType.Nearest);

			collectingPolicyField.SetEnabled(policyUnlocked && storingCollectingPolicies.Count > 1);
			placingPolicyField.SetEnabled(policyUnlocked && storingPlacingPolicies.Count > 1);
			pickingPolicyField.SetEnabled(policyUnlocked && pickingPolicies.Count > 1);
			pickingCollectingPolicyField.SetEnabled(
				policyUnlocked && currentPickingPolicy == PickingPolicyType.InventoryGuided && pickingCollectingPolicies.Count > 1);

			float storingBoxFill = inboundWorkflow != null ? inboundWorkflow.StoringBoxFillLimitPercent : 80.0f;
			storingBoxFillSlider.SetValueWithoutNotify(storingBoxFill);
			storingBoxFillValue.text = $"{storingBoxFill:0}%";
			storingBoxFillSlider.SetEnabled(optimizationUnlocked);

			float pickingBoxFill = outboundWorkflow != null ? outboundWorkflow.PickingBoxFillLimitPercent : 80.0f;
			pickingBoxFillSlider.SetValueWithoutNotify(pickingBoxFill);
			pickingBoxFillValue.text = $"{pickingBoxFill:0}%";
			pickingBoxFillSlider.SetEnabled(optimizationUnlocked);

			bool inboundQualityEnabled = inboundWorkflow?.InboundQualityControlEnabled == true;
			float inboundFreshness = inboundWorkflow != null
				? inboundWorkflow.MinimumInboundFreshnessPercent
				: QualityControlPolicy.DefaultMinimumFreshnessPercent;
			float inboundDamage = inboundWorkflow != null
				? inboundWorkflow.MaximumInboundDamagePercent
				: QualityControlPolicy.DefaultMaximumDamagePercent;
			inboundQualityControlEnabled.SetValueWithoutNotify(inboundQualityEnabled);
			inboundQualityControlEnabled.SetEnabled(qualityControlUnlocked);
			SetQualitySlider(inboundQualityFreshnessSlider, inboundQualityFreshnessValue, inboundFreshness,
				qualityControlUnlocked && inboundQualityEnabled);
			SetQualitySlider(inboundQualityDamageSlider, inboundQualityDamageValue, inboundDamage,
				qualityControlUnlocked && inboundQualityEnabled);

			bool outboundQualityEnabled = outboundWorkflow?.OutboundQualityControlEnabled == true;
			float outboundFreshness = outboundWorkflow != null
				? outboundWorkflow.MinimumOutboundFreshnessPercent
				: QualityControlPolicy.DefaultMinimumFreshnessPercent;
			float outboundDamage = outboundWorkflow != null
				? outboundWorkflow.MaximumOutboundDamagePercent
				: QualityControlPolicy.DefaultMaximumDamagePercent;
			outboundQualityControlEnabled.SetValueWithoutNotify(outboundQualityEnabled);
			outboundQualityControlEnabled.SetEnabled(qualityControlUnlocked);
			SetQualitySlider(outboundQualityFreshnessSlider, outboundQualityFreshnessValue, outboundFreshness,
				qualityControlUnlocked && outboundQualityEnabled);
			SetQualitySlider(outboundQualityDamageSlider, outboundQualityDamageValue, outboundDamage,
				qualityControlUnlocked && outboundQualityEnabled);
		}

		private static void SetQualitySlider(Slider slider, Label valueLabel, float value, bool enabled)
		{
			slider.SetValueWithoutNotify(value);
			slider.SetEnabled(enabled);
			valueLabel.text = $"{value:0}%";
		}

		private void BeginLandingAreaCreation()
		{
			AreaOverlayController overlay = null;
			if (areaManager != null) areaManager.TryGetComponent(out overlay);
			overlay ??= FindAnyObjectByType<AreaOverlayController>(FindObjectsInactive.Include);
			if (overlay == null)
			{
				messageLabel.text = "Landing Area placement is unavailable.";
				return;
			}

			window.Close();
			overlay.BeginCreateOneShot(AreaType.RocketLanding, 0);
		}

		private void RemoveLandingArea(Area area)
		{
			if (areaManager == null || areaManager.RemoveArea(area) == false)
				messageLabel.text = "Unable to remove the Landing Area.";
		}

		private void BeginLandingDestinationLink()
		{
			if (buildManagementWindow == null)
			{
				messageLabel.text = "Build Routing is unavailable.";
				return;
			}

			window.Close();
			buildManagementWindow.OpenRoutingAndBeginInboundDestinationSelection();
		}

		private void BeginOutboundDestinationLink()
		{
			if (buildManagementWindow == null)
			{
				messageLabel.text = "Build Routing is unavailable.";
				return;
			}

			window.Close();
			buildManagementWindow.OpenRoutingAndBeginOutboundDestinationSelection();
		}

		private void OnCollectingPolicyChanged(ChangeEvent<string> evt)
		{
			int index = collectingPolicyField.index;
			if (inboundWorkflow != null && index >= 0 && index < storingCollectingPolicies.Count &&
				inboundWorkflow.TrySetStoringCollectingPolicy(storingCollectingPolicies[index]))
			{
				messageLabel.text = string.Empty;
				return;
			}

			RejectPolicyChange();
		}

		private void OnPlacingPolicyChanged(ChangeEvent<string> evt)
		{
			int index = placingPolicyField.index;
			if (inboundWorkflow != null && index >= 0 && index < storingPlacingPolicies.Count &&
				inboundWorkflow.TrySetStoringPlacingPolicy(storingPlacingPolicies[index]))
			{
				messageLabel.text = string.Empty;
				return;
			}

			RejectPolicyChange();
		}

		private void OnPickingPolicyChanged(ChangeEvent<string> evt)
		{
			int index = pickingPolicyField.index;
			if (outboundWorkflow != null && index >= 0 && index < pickingPolicies.Count &&
				outboundWorkflow.TrySetPickingPolicy(pickingPolicies[index]))
			{
				messageLabel.text = string.Empty;
				RefreshPolicies();
				return;
			}

			RejectPolicyChange();
		}

		private void OnPickingCollectingPolicyChanged(ChangeEvent<string> evt)
		{
			int index = pickingCollectingPolicyField.index;
			if (outboundWorkflow != null && index >= 0 && index < pickingCollectingPolicies.Count &&
				outboundWorkflow.TrySetPickingCollectingPolicy(pickingCollectingPolicies[index]))
			{
				messageLabel.text = string.Empty;
				return;
			}

			RejectPolicyChange();
		}

		private void OnStoringBoxFillChanged(ChangeEvent<float> evt)
		{
			float value = Mathf.Round(evt.newValue);
			if (inboundWorkflow?.TrySetStoringBoxFillLimitPercent(value) == true)
			{
				storingBoxFillSlider.SetValueWithoutNotify(value);
				storingBoxFillValue.text = $"{value:0}%";
				messageLabel.text = string.Empty;
				return;
			}

			RejectPolicyChange();
		}

		private void OnPickingBoxFillChanged(ChangeEvent<float> evt)
		{
			float value = Mathf.Round(evt.newValue);
			if (outboundWorkflow?.TrySetPickingBoxFillLimitPercent(value) == true)
			{
				pickingBoxFillSlider.SetValueWithoutNotify(value);
				pickingBoxFillValue.text = $"{value:0}%";
				messageLabel.text = string.Empty;
				return;
			}

			RejectPolicyChange();
		}

		private void OnInboundQualityControlChanged(ChangeEvent<bool> evt)
		{
			if (inboundWorkflow?.TrySetInboundQualityControlEnabled(evt.newValue) == true)
			{
				messageLabel.text = string.Empty;
				RefreshPolicies();
				return;
			}

			RejectPolicyChange();
		}

		private void OnInboundQualityFreshnessChanged(ChangeEvent<float> evt)
		{
			TrySetInboundQualityThresholds(Mathf.Round(evt.newValue), inboundWorkflow?.MaximumInboundDamagePercent ?? 0.0f);
		}

		private void OnInboundQualityDamageChanged(ChangeEvent<float> evt)
		{
			TrySetInboundQualityThresholds(inboundWorkflow?.MinimumInboundFreshnessPercent ?? 0.0f, Mathf.Round(evt.newValue));
		}

		private void TrySetInboundQualityThresholds(float minimumFreshness, float maximumDamage)
		{
			if (inboundWorkflow?.TrySetInboundQualityThresholds(minimumFreshness, maximumDamage) == true)
			{
				messageLabel.text = string.Empty;
				RefreshPolicies();
				return;
			}

			RejectPolicyChange();
		}

		private void OnOutboundQualityControlChanged(ChangeEvent<bool> evt)
		{
			if (outboundWorkflow?.TrySetOutboundQualityControlEnabled(evt.newValue) == true)
			{
				messageLabel.text = string.Empty;
				RefreshPolicies();
				return;
			}

			RejectPolicyChange();
		}

		private void OnOutboundQualityFreshnessChanged(ChangeEvent<float> evt)
		{
			TrySetOutboundQualityThresholds(Mathf.Round(evt.newValue), outboundWorkflow?.MaximumOutboundDamagePercent ?? 0.0f);
		}

		private void OnOutboundQualityDamageChanged(ChangeEvent<float> evt)
		{
			TrySetOutboundQualityThresholds(outboundWorkflow?.MinimumOutboundFreshnessPercent ?? 0.0f, Mathf.Round(evt.newValue));
		}

		private void TrySetOutboundQualityThresholds(float minimumFreshness, float maximumDamage)
		{
			if (outboundWorkflow?.TrySetOutboundQualityThresholds(minimumFreshness, maximumDamage) == true)
			{
				messageLabel.text = string.Empty;
				RefreshPolicies();
				return;
			}

			RejectPolicyChange();
		}

		private void OnAreaChanged(Area area)
		{
			RefreshLandingAreas();
		}

		private void RejectPolicyChange()
		{
			messageLabel.text = "Required research is not completed.";
			RefreshPolicies();
		}

		private static void SetPolicyChoices<T>(DropdownField field, List<T> policies, List<string> choices, T current)
		{
			field.choices = choices;
			if (choices.Count == 0) return;
			int index = policies.IndexOf(current);
			field.SetValueWithoutNotify(choices[Mathf.Max(0, index)]);
		}

		private UITooltipContent BuildPolicyTabTooltip()
		{
			const string title = "Workflow Policy";
			const string description = "Configure how workers collect, pick, and place cargo.";
			return IsResearchCompleted(ResearchIds.WorkflowPolicyManagement)
				? UITooltipContent.DescriptionOnly(title, description)
				: UITooltipContent.Locked(title, description, "Required research: Workflow Policy Management");
		}

		private UITooltipContent BuildStoringCollectingPolicyTooltip()
		{
			const string title = "Capsule collecting";
			const string description = "Choose how storing work selects an available inbound capsule.";
			return IsResearchCompleted(ResearchIds.WorkflowPolicyOptimization)
				? UITooltipContent.DescriptionOnly(title, description)
				: new UITooltipContent(title, description,
					"Additional policy requires: Workflow Policy Optimization");
		}

		private UITooltipContent BuildStoringPlacingPolicyTooltip()
		{
			const string title = "Shelf placing";
			const string description = "Choose how storing work selects shelves for cargo placement.";
			return IsResearchCompleted(ResearchIds.WorkflowPolicyOptimization)
				? UITooltipContent.DescriptionOnly(title, description)
				: new UITooltipContent(title, description,
					"Additional policy requires: Workflow Policy Optimization");
		}

		private UITooltipContent BuildStoringBoxFillTooltip()
		{
			const string title = "Storing box fill limit";
			const string description = "Stop adding new storing work to a box after it reaches this fill percentage.";
			return IsResearchCompleted(ResearchIds.WorkflowPolicyOptimization)
				? UITooltipContent.DescriptionOnly(title, description)
				: UITooltipContent.Locked(title, description,
					"Required research: Workflow Policy Optimization");
		}

		private UITooltipContent BuildPickingPolicyTooltip()
		{
			const string title = "Picking method";
			const string description = "Manual workers search nearby shelves. Inventory-guided workers receive the exact source shelf.";
			return IsResearchCompleted(ResearchIds.InventoryDigitization)
				? UITooltipContent.DescriptionOnly(title, description)
				: UITooltipContent.Locked(title, description, "Required research: Inventory Digitization");
		}

		private UITooltipContent BuildPickingCollectingPolicyTooltip()
		{
			const string title = "Guided source priority";
			const string description = "Choose which known shelf Inventory-guided picking visits first.";
			if (outboundWorkflow?.PickingPolicyType != PickingPolicyType.InventoryGuided)
				return new UITooltipContent(title, description, "Used by Inventory-guided picking");

			return IsResearchCompleted(ResearchIds.WorkflowPolicyOptimization)
				? UITooltipContent.DescriptionOnly(title, description)
				: new UITooltipContent(title, description,
					"Additional policy requires: Workflow Policy Optimization");
		}

		private UITooltipContent BuildPickingBoxFillTooltip()
		{
			const string title = "Picking box fill limit";
			const string description = "Stop adding new picking work to a box after it reaches this fill percentage.";
			return IsResearchCompleted(ResearchIds.WorkflowPolicyOptimization)
				? UITooltipContent.DescriptionOnly(title, description)
				: UITooltipContent.Locked(title, description,
					"Required research: Workflow Policy Optimization");
		}

		private UITooltipContent BuildInboundQualityControlTooltip()
		{
			const string title = "Inbound quality control";
			const string description = "Labeling workers reject items below the freshness limit or above the damage limit.";
			return IsResearchCompleted(ResearchIds.QualityControl)
				? UITooltipContent.DescriptionOnly(title, description)
				: UITooltipContent.Locked(title, description, "Required research: Quality Control");
		}

		private UITooltipContent BuildOutboundQualityControlTooltip()
		{
			const string title = "Outbound quality control";
			const string description = "LaunchSort workers reject unsuitable packed cargo and request replacement picking.";
			return IsResearchCompleted(ResearchIds.QualityControl)
				? UITooltipContent.DescriptionOnly(title, description)
				: UITooltipContent.Locked(title, description, "Required research: Quality Control");
		}

		private bool IsResearchCompleted(string researchId)
		{
			return researchService?.IsResearched(researchId) == true;
		}
	}
}
