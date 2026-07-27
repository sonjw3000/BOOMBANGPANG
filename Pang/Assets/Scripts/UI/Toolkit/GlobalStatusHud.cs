using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class GlobalStatusHud : MonoBehaviour
	{
		private const string DocumentObjectName = "GlobalStatusHudDocument";
		private const string TooltipDocumentObjectName = "UITooltipDocument";
		private const string HistoryDocumentObjectName = "GlobalHistoryWindowDocument";
		private const string ContractDocumentObjectName = "ContractManagementWindowDocument";
		private const string InventoryDocumentObjectName = "InventoryManagementWindowDocument";
		private const string OrdersDocumentObjectName = "OrdersManagementWindowDocument";
		private const string WorkforceDocumentObjectName = "WorkforceManagementWindowDocument";
		private const string BuildDocumentObjectName = "BuildManagementWindowDocument";
		private const string WorkflowDocumentObjectName = "WorkflowManagementWindowDocument";
		private const string CompanyDocumentObjectName = "CompanyManagementWindowDocument";
		private const string DebugDocumentObjectName = "DebugControlWindowDocument";
		private const int MaxVisibleEvents = 5;
		private const float EventFadeSeconds = 0.8f;
		private const float ReferenceWidth = 1920f;
		private const float ReferenceHeight = 1080f;
		private const float ReferenceUiScale = 1.0f;
		[SerializeField] private VisualTreeAsset visualTreeAsset;
		[SerializeField] private VisualTreeAsset tooltipVisualTreeAsset;
		[SerializeField] private VisualTreeAsset hudEventEntryTemplate;
		[SerializeField] private VisualTreeAsset windowVisualTreeAsset;
		[SerializeField] private VisualTreeAsset historyContentTemplate;
		[SerializeField] private VisualTreeAsset historyRowTemplate;
		[SerializeField] private VisualTreeAsset contractContentTemplate;
		[SerializeField] private VisualTreeAsset activeContractRowTemplate;
		[SerializeField] private VisualTreeAsset contractMarketRowTemplate;
		[SerializeField] private VisualTreeAsset vendorContractRowTemplate;
		[SerializeField] private VisualTreeAsset inventoryContentTemplate;
		[SerializeField] private VisualTreeAsset inventoryItemRowTemplate;
		[SerializeField] private VisualTreeAsset ordersContentTemplate;
		[SerializeField] private VisualTreeAsset orderRowTemplate;
		[SerializeField] private VisualTreeAsset orderLineRowTemplate;
		[SerializeField] private VisualTreeAsset workforceContentTemplate;
		[SerializeField] private VisualTreeAsset workforceRosterRowTemplate;
		[SerializeField] private VisualTreeAsset workforceCandidateRowTemplate;
		[SerializeField] private List<WorkforceMarketData_SO> workforceHumanMarkets = new();
		[SerializeField] private List<WorkforceMarketData_SO> workforceRobotMarkets = new();
		[SerializeField] private VisualTreeAsset buildContentTemplate;
		[SerializeField] private VisualTreeAsset buildPlaceableRowTemplate;
		[SerializeField] private VisualTreeAsset buildRuleRowTemplate;
		[SerializeField] private BuildingSelectionProxy buildSelectionProxyPrefab;
		[SerializeField] private GameObject buildOverlayQuadPrefab;
		[SerializeField] private GameObject buildOverlayLabelPrefab;
		[SerializeField] private VisualTreeAsset workflowContentTemplate;
		[SerializeField] private VisualTreeAsset workflowLandingAreaRowTemplate;
		[SerializeField] private VisualTreeAsset companyContentTemplate;
		[SerializeField] private VisualTreeAsset companyLicenseRowTemplate;
		[SerializeField] private VisualTreeAsset companyResearchRowTemplate;
		[SerializeField] private VisualTreeAsset debugContentTemplate;
		[SerializeField] private PanelSettings panelSettings;
		[SerializeField] private int sortingOrder = 100;

		private readonly List<ActiveHudEvent> activeEvents = new();
		private UIDocument uiDocument;
		private UIDocument tooltipDocument;
		private UITooltipPresenter tooltipPresenter;
		private UnityEngine.UI.CanvasScaler legacyCanvasScaler;
		private VisualElement hudRoot;
		private VisualElement leftHud;
		private VisualElement timeCluster;
		private VisualElement economySummary;
		private VisualElement hudEventArea;
		private VisualElement hudEventList;
		private Label moneyValue;
		private Label reputationValue;
		private Label dateValue;
		private Label speedValue;
		private Button pauseButton;
		private Button normalSpeedButton;
		private Button doubleSpeedButton;
		private Button managementButton;
		private Button contractManagementButton;
		private Button inventoryManagementButton;
		private Button ordersManagementButton;
		private Button workforceManagementButton;
		private Button buildManagementButton;
		private Button workflowManagementButton;
		private Button companyManagementButton;
		private VisualElement managementMenu;
		private GlobalHistoryWindow historyWindow;
		private ContractManagementWindow contractManagementWindow;
		private InventoryManagementWindow inventoryManagementWindow;
		private OrderManagementWindow orderManagementWindow;
		private WorkforceManagementWindow workforceManagementWindow;
		private BuildManagementWindow buildManagementWindow;
		private WorkflowManagementWindow workflowManagementWindow;
		private CompanyManagementWindow companyManagementWindow;
		private DebugControlWindow debugControlWindow;
		private SelectionCardHud selectionCard;
		private EconomyService economyService;
		private HudEventManager hudEventManager;
		private GameTime gameTime;
		private ResearchService researchService;
		private bool started;
		private bool? timeHudDockedRight;
		private int scaledScreenWidth = -1;
		private int scaledScreenHeight = -1;

		public SelectionCardHud SelectionCard => selectionCard;

		private sealed class ActiveHudEvent
		{
			public VisualElement Root;
			public float Elapsed;
			public float VisibleSeconds;
		}

		private void OnEnable()
		{
			ApplyPanelScale();
			EnsureDocument();
			EnsureTooltip();
			EnsureHistoryWindow();
			EnsureContractManagementWindow();
			EnsureInventoryManagementWindow();
			EnsureOrderManagementWindow();
			ConfigureManagementNavigation();
			EnsureWorkforceManagementWindow();
			EnsureBuildManagementWindow();
			EnsureWorkflowManagementWindow();
			EnsureCompanyManagementWindow();
			EnsureDebugControlWindow();
			BindControls();

			if (started)
				BindServices();
		}

		private void Start()
		{
			started = true;
			BindServices();
		}

		private void Update()
		{
			ApplyPanelScale();

			for (int i = activeEvents.Count - 1; i >= 0; --i)
			{
				ActiveHudEvent activeEvent = activeEvents[i];
				activeEvent.Elapsed += Time.unscaledDeltaTime;
				float fade = Mathf.Clamp01((activeEvent.Elapsed - activeEvent.VisibleSeconds) / EventFadeSeconds);
				activeEvent.Root.style.opacity = 1f - fade;

				if (fade < 1f)
					continue;

				activeEvent.Root.RemoveFromHierarchy();
				activeEvents.RemoveAt(i);
			}
		}

		private void ApplyPanelScale()
		{
			legacyCanvasScaler ??= GetComponentInChildren<UnityEngine.UI.CanvasScaler>(true);
			if ((panelSettings == null && legacyCanvasScaler == null) ||
				(scaledScreenWidth == Screen.width && scaledScreenHeight == Screen.height))
			{
				return;
			}

			scaledScreenWidth = Screen.width;
			scaledScreenHeight = Screen.height;
			float widthScale = Screen.width / ReferenceWidth;
			float heightScale = Screen.height / ReferenceHeight;
			float uiScale = Mathf.Max(0.01f, ReferenceUiScale * Mathf.Min(widthScale, heightScale));
			if (panelSettings != null)
			{
				panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
				panelSettings.scale = uiScale;
			}

			if (legacyCanvasScaler != null)
			{
				legacyCanvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
				legacyCanvasScaler.scaleFactor = uiScale;
			}
		}

		private void OnDisable()
		{
			UnbindControls();
			UnbindServices();
		}

		private void EnsureDocument()
		{
			if (uiDocument != null)
				return;

			if (visualTreeAsset == null || panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] VisualTreeAsset or PanelSettings is missing.", this);
				return;
			}

			GameObject documentObject = new(DocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			uiDocument = documentObject.AddComponent<UIDocument>();
			uiDocument.panelSettings = panelSettings;
			uiDocument.visualTreeAsset = visualTreeAsset;
			uiDocument.sortingOrder = sortingOrder;
			documentObject.SetActive(true);
		}

		private void EnsureTooltip()
		{
			if (tooltipPresenter != null)
				return;

			if (tooltipVisualTreeAsset == null || panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] Tooltip VisualTreeAsset or PanelSettings is missing.", this);
				return;
			}

			GameObject documentObject = new(TooltipDocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			tooltipDocument = documentObject.AddComponent<UIDocument>();
			tooltipDocument.panelSettings = panelSettings;
			tooltipDocument.visualTreeAsset = tooltipVisualTreeAsset;
			tooltipDocument.sortingOrder = sortingOrder + 1000;
			tooltipPresenter = documentObject.AddComponent<UITooltipPresenter>();
			documentObject.SetActive(true);
			tooltipPresenter.Initialize(tooltipDocument.rootVisualElement);
		}

		private void EnsureHistoryWindow()
		{
			if (historyWindow != null)
				return;

			if (windowVisualTreeAsset == null || historyContentTemplate == null || historyRowTemplate == null || panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] History window assets are missing.", this);
				return;
			}

			GameObject documentObject = new(HistoryDocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			UIDocument historyDocument = documentObject.AddComponent<UIDocument>();
			historyDocument.panelSettings = panelSettings;
			historyDocument.visualTreeAsset = windowVisualTreeAsset;
			historyDocument.sortingOrder = sortingOrder + 10;

			UIWindow window = documentObject.AddComponent<UIWindow>();
			window.SetOpenOnEnable(false);
			historyWindow = documentObject.AddComponent<GlobalHistoryWindow>();
			historyWindow.Configure(window, historyContentTemplate, historyRowTemplate);
			documentObject.SetActive(true);
		}

		private void EnsureContractManagementWindow()
		{
			if (contractManagementWindow != null)
				return;

			if (windowVisualTreeAsset == null || contractContentTemplate == null || activeContractRowTemplate == null ||
				contractMarketRowTemplate == null || vendorContractRowTemplate == null || panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] Contract management window assets are missing.", this);
				return;
			}

			GameObject documentObject = new(ContractDocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			UIDocument contractDocument = documentObject.AddComponent<UIDocument>();
			contractDocument.panelSettings = panelSettings;
			contractDocument.visualTreeAsset = windowVisualTreeAsset;
			contractDocument.sortingOrder = sortingOrder + 20;

			UIWindow window = documentObject.AddComponent<UIWindow>();
			window.SetOpenOnEnable(false);
			contractManagementWindow = documentObject.AddComponent<ContractManagementWindow>();
			contractManagementWindow.Configure(window, contractContentTemplate, activeContractRowTemplate,
				contractMarketRowTemplate, vendorContractRowTemplate);
			documentObject.SetActive(true);
		}

		private void EnsureInventoryManagementWindow()
		{
			if (inventoryManagementWindow != null)
				return;

			if (windowVisualTreeAsset == null || inventoryContentTemplate == null || inventoryItemRowTemplate == null ||
				panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] Inventory management window assets are missing.", this);
				return;
			}

			GameObject documentObject = new(InventoryDocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			UIDocument inventoryDocument = documentObject.AddComponent<UIDocument>();
			inventoryDocument.panelSettings = panelSettings;
			inventoryDocument.visualTreeAsset = windowVisualTreeAsset;
			inventoryDocument.sortingOrder = sortingOrder + 30;

			UIWindow window = documentObject.AddComponent<UIWindow>();
			window.SetOpenOnEnable(false);
			inventoryManagementWindow = documentObject.AddComponent<InventoryManagementWindow>();
			inventoryManagementWindow.Configure(window, inventoryContentTemplate, inventoryItemRowTemplate);
			documentObject.SetActive(true);
		}

		private void EnsureOrderManagementWindow()
		{
			if (orderManagementWindow != null)
				return;

			if (windowVisualTreeAsset == null || ordersContentTemplate == null || orderRowTemplate == null ||
				orderLineRowTemplate == null || panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] Orders management window assets are missing.", this);
				return;
			}

			GameObject documentObject = new(OrdersDocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			UIDocument ordersDocument = documentObject.AddComponent<UIDocument>();
			ordersDocument.panelSettings = panelSettings;
			ordersDocument.visualTreeAsset = windowVisualTreeAsset;
			ordersDocument.sortingOrder = sortingOrder + 40;

			UIWindow window = documentObject.AddComponent<UIWindow>();
			window.SetOpenOnEnable(false);
			orderManagementWindow = documentObject.AddComponent<OrderManagementWindow>();
			orderManagementWindow.Configure(window, ordersContentTemplate, orderRowTemplate, orderLineRowTemplate);
			documentObject.SetActive(true);
		}

		private void ConfigureManagementNavigation()
		{
			if (inventoryManagementWindow == null || orderManagementWindow == null)
				return;

			inventoryManagementWindow.ConfigureNavigation(orderManagementWindow.OpenForItem);
			orderManagementWindow.ConfigureNavigation(OpenInventoryManagementForItem);
		}

		private void EnsureWorkforceManagementWindow()
		{
			if (workforceManagementWindow != null)
				return;

			if (windowVisualTreeAsset == null || workforceContentTemplate == null || workforceRosterRowTemplate == null ||
				workforceCandidateRowTemplate == null || panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] Workforce management window assets are missing.", this);
				return;
			}

			GameObject documentObject = new(WorkforceDocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			UIDocument workforceDocument = documentObject.AddComponent<UIDocument>();
			workforceDocument.panelSettings = panelSettings;
			workforceDocument.visualTreeAsset = windowVisualTreeAsset;
			workforceDocument.sortingOrder = sortingOrder + 50;

			UIWindow window = documentObject.AddComponent<UIWindow>();
			window.SetOpenOnEnable(false);
			workforceManagementWindow = documentObject.AddComponent<WorkforceManagementWindow>();
			workforceManagementWindow.Configure(window, workforceContentTemplate, workforceRosterRowTemplate,
				workforceCandidateRowTemplate, workforceHumanMarkets, workforceRobotMarkets);
			documentObject.SetActive(true);
		}

		private void EnsureBuildManagementWindow()
		{
			if (buildManagementWindow != null)
				return;

			if (windowVisualTreeAsset == null || buildContentTemplate == null || buildPlaceableRowTemplate == null ||
				buildRuleRowTemplate == null || buildSelectionProxyPrefab == null || buildOverlayQuadPrefab == null ||
				buildOverlayLabelPrefab == null || panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] Build management window assets are missing.", this);
				return;
			}

			GameObject documentObject = new(BuildDocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			UIDocument buildDocument = documentObject.AddComponent<UIDocument>();
			buildDocument.panelSettings = panelSettings;
			buildDocument.visualTreeAsset = windowVisualTreeAsset;
			buildDocument.sortingOrder = sortingOrder + 60;

			UIWindow window = documentObject.AddComponent<UIWindow>();
			window.SetOpenOnEnable(false);
			BuildingPlacementOverlayController buildingOverlay = documentObject.AddComponent<BuildingPlacementOverlayController>();
			buildingOverlay.Configure(buildSelectionProxyPrefab, buildOverlayQuadPrefab, buildOverlayLabelPrefab);
			RoutingConnectivityOverlayController routingOverlay = documentObject.AddComponent<RoutingConnectivityOverlayController>();
			routingOverlay.Configure(buildOverlayQuadPrefab);
			CargoPortLinkModeController buildingLinkController = documentObject.AddComponent<CargoPortLinkModeController>();
			buildingLinkController.Configure(buildOverlayQuadPrefab, buildOverlayLabelPrefab);
			WorkflowDestinationLinkModeController workflowDestinationController = documentObject.AddComponent<WorkflowDestinationLinkModeController>();
			workflowDestinationController.Configure(buildOverlayQuadPrefab, buildOverlayLabelPrefab);
			buildManagementWindow = documentObject.AddComponent<BuildManagementWindow>();
			buildManagementWindow.Configure(window, buildContentTemplate, buildPlaceableRowTemplate, buildRuleRowTemplate,
				buildingOverlay, routingOverlay, buildingLinkController, workflowDestinationController);
			documentObject.SetActive(true);
		}

		private void EnsureWorkflowManagementWindow()
		{
			if (workflowManagementWindow != null)
				return;

			if (windowVisualTreeAsset == null || workflowContentTemplate == null || workflowLandingAreaRowTemplate == null ||
				buildManagementWindow == null || panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] Workflow management window assets are missing.", this);
				return;
			}

			GameObject documentObject = new(WorkflowDocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			UIDocument workflowDocument = documentObject.AddComponent<UIDocument>();
			workflowDocument.panelSettings = panelSettings;
			workflowDocument.visualTreeAsset = windowVisualTreeAsset;
			workflowDocument.sortingOrder = sortingOrder + 70;

			UIWindow window = documentObject.AddComponent<UIWindow>();
			window.SetOpenOnEnable(false);
			workflowManagementWindow = documentObject.AddComponent<WorkflowManagementWindow>();
			workflowManagementWindow.Configure(window, workflowContentTemplate, workflowLandingAreaRowTemplate, buildManagementWindow);
			documentObject.SetActive(true);
		}

		private void EnsureCompanyManagementWindow()
		{
			if (companyManagementWindow != null)
				return;

			if (windowVisualTreeAsset == null || historyRowTemplate == null || companyContentTemplate == null ||
				companyLicenseRowTemplate == null || companyResearchRowTemplate == null || panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] Company management window assets are missing.", this);
				return;
			}

			GameObject documentObject = new(CompanyDocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			UIDocument companyDocument = documentObject.AddComponent<UIDocument>();
			companyDocument.panelSettings = panelSettings;
			companyDocument.visualTreeAsset = windowVisualTreeAsset;
			companyDocument.sortingOrder = sortingOrder + 80;

			UIWindow window = documentObject.AddComponent<UIWindow>();
			window.SetOpenOnEnable(false);
			companyManagementWindow = documentObject.AddComponent<CompanyManagementWindow>();
			companyManagementWindow.Configure(window, companyContentTemplate, historyRowTemplate,
				companyLicenseRowTemplate, companyResearchRowTemplate);
			documentObject.SetActive(true);
		}

		private void EnsureDebugControlWindow()
		{
			if (debugControlWindow != null)
				return;

			if (windowVisualTreeAsset == null || debugContentTemplate == null || panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] Debug window assets are missing.", this);
				return;
			}

			GameObject documentObject = new(DebugDocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			UIDocument debugDocument = documentObject.AddComponent<UIDocument>();
			debugDocument.panelSettings = panelSettings;
			debugDocument.visualTreeAsset = windowVisualTreeAsset;
			debugDocument.sortingOrder = sortingOrder + 90;

			UIWindow window = documentObject.AddComponent<UIWindow>();
			window.SetOpenOnEnable(false);
			window.SetDefaultSize(new Vector2(560f, 460f));
			debugControlWindow = documentObject.AddComponent<DebugControlWindow>();
			debugControlWindow.Configure(window, debugContentTemplate);
			documentObject.SetActive(true);
		}

		private void BindControls()
		{
			if (uiDocument == null)
				return;

			VisualElement root = uiDocument.rootVisualElement;
			root.pickingMode = PickingMode.Ignore;
			selectionCard ??= new SelectionCardHud();
			if (selectionCard.Bind(root) == false)
				Debug.LogError("[GlobalStatusHud] Selection Card UXML elements are missing.", this);
			hudRoot = root;
			leftHud = root.Q<VisualElement>(className: "left-hud");
			timeCluster = root.Q<VisualElement>(className: "time-cluster");
			economySummary = root.Q<VisualElement>("economy-summary");
			hudEventArea = root.Q<VisualElement>("hud-event-area");
			hudEventList = root.Q<VisualElement>("hud-event-list");
			moneyValue = root.Q<Label>("money-value");
			reputationValue = root.Q<Label>("reputation-value");
			dateValue = root.Q<Label>("date-value");
			speedValue = root.Q<Label>("speed-value");
			pauseButton = root.Q<Button>("pause-button");
			normalSpeedButton = root.Q<Button>("normal-speed-button");
			doubleSpeedButton = root.Q<Button>("double-speed-button");
			managementButton = root.Q<Button>("management-button");
			contractManagementButton = root.Q<Button>("contract-management-button");
			inventoryManagementButton = root.Q<Button>("inventory-management-button");
			ordersManagementButton = root.Q<Button>("orders-management-button");
			workforceManagementButton = root.Q<Button>("workforce-management-button");
			buildManagementButton = root.Q<Button>("build-management-button");
			workflowManagementButton = root.Q<Button>("workflow-management-button");
			companyManagementButton = root.Q<Button>("company-management-button");
			managementMenu = root.Q<VisualElement>("management-menu");

			if (leftHud == null || timeCluster == null || economySummary == null || hudEventArea == null ||
				hudEventList == null || moneyValue == null ||
				reputationValue == null || dateValue == null || speedValue == null || pauseButton == null ||
				normalSpeedButton == null || doubleSpeedButton == null || managementButton == null || contractManagementButton == null ||
				inventoryManagementButton == null || ordersManagementButton == null ||
				workforceManagementButton == null ||
				buildManagementButton == null ||
				workflowManagementButton == null ||
				companyManagementButton == null ||
				managementMenu == null)
			{
				Debug.LogError("[GlobalStatusHud] Required UXML elements are missing.", this);
				return;
			}

			economySummary.UnregisterCallback<ClickEvent>(OnEconomySummaryClicked);
			economySummary.RegisterCallback<ClickEvent>(OnEconomySummaryClicked);
			hudEventArea.UnregisterCallback<ClickEvent>(OnHudEventAreaClicked);
			hudEventArea.RegisterCallback<ClickEvent>(OnHudEventAreaClicked);
			pauseButton.clicked -= Pause;
			pauseButton.clicked += Pause;
			normalSpeedButton.clicked -= SetNormalSpeed;
			normalSpeedButton.clicked += SetNormalSpeed;
			doubleSpeedButton.clicked -= DoubleSpeed;
			doubleSpeedButton.clicked += DoubleSpeed;
			pauseButton.SetTooltip(UITooltipContent.DescriptionOnly("Pause", "Pause the simulation."));
			normalSpeedButton.SetTooltip(UITooltipContent.DescriptionOnly("Normal Speed", "Run the simulation at normal speed."));
			doubleSpeedButton.SetTooltip(UITooltipContent.DescriptionOnly("Increase Speed", "Increase the simulation speed."));
			managementButton.clicked -= ToggleManagementMenu;
			managementButton.clicked += ToggleManagementMenu;
			managementButton.SetTooltip(UITooltipContent.DescriptionOnly("Management", "Open the management menu."));
			contractManagementButton.clicked -= OpenContractManagement;
			contractManagementButton.clicked += OpenContractManagement;
			inventoryManagementButton.clicked -= OpenInventoryManagement;
			inventoryManagementButton.clicked += OpenInventoryManagement;
			ordersManagementButton.clicked -= OpenOrdersManagement;
			ordersManagementButton.clicked += OpenOrdersManagement;
			workforceManagementButton.clicked -= OpenWorkforceManagement;
			workforceManagementButton.clicked += OpenWorkforceManagement;
			buildManagementButton.clicked -= OpenBuildManagement;
			buildManagementButton.clicked += OpenBuildManagement;
			workflowManagementButton.clicked -= OpenWorkflowManagement;
			workflowManagementButton.clicked += OpenWorkflowManagement;
			companyManagementButton.clicked -= OpenCompanyManagement;
			companyManagementButton.clicked += OpenCompanyManagement;
			contractManagementButton.SetTooltip(UITooltipContent.DescriptionOnly("Contracts", "Review available and active contracts."));
			inventoryManagementButton.SetTooltip(BuildInventoryTooltip);
			ordersManagementButton.SetTooltip(UITooltipContent.DescriptionOnly("Orders", "Review order progress and outbound workflow stages."));
			workforceManagementButton.SetTooltip(UITooltipContent.DescriptionOnly("Workforce", "Review workers and available hiring candidates."));
			buildManagementButton.SetTooltip(UITooltipContent.DescriptionOnly("Build", "Construct facilities and configure building logistics."));
			workflowManagementButton.SetTooltip(UITooltipContent.DescriptionOnly("Workflow", "Configure inbound and outbound workflow behavior."));
			companyManagementButton.SetTooltip(UITooltipContent.DescriptionOnly("Company", "Review company licenses and research."));
			ShowManagementMenu(false);
			hudRoot.UnregisterCallback<GeometryChangedEvent>(OnHudGeometryChanged);
			hudRoot.RegisterCallback<GeometryChangedEvent>(OnHudGeometryChanged);
			timeCluster.UnregisterCallback<GeometryChangedEvent>(OnHudGeometryChanged);
			timeCluster.RegisterCallback<GeometryChangedEvent>(OnHudGeometryChanged);
			hudRoot.schedule.Execute(UpdateTimeHudPlacement);
		}

		private void UnbindControls()
		{
			economySummary?.UnregisterCallback<ClickEvent>(OnEconomySummaryClicked);
			hudEventArea?.UnregisterCallback<ClickEvent>(OnHudEventAreaClicked);
			hudRoot?.UnregisterCallback<GeometryChangedEvent>(OnHudGeometryChanged);
			timeCluster?.UnregisterCallback<GeometryChangedEvent>(OnHudGeometryChanged);
			if (pauseButton != null)
				pauseButton.clicked -= Pause;
			if (normalSpeedButton != null)
				normalSpeedButton.clicked -= SetNormalSpeed;
			if (doubleSpeedButton != null)
				doubleSpeedButton.clicked -= DoubleSpeed;
			if (managementButton != null)
				managementButton.clicked -= ToggleManagementMenu;
			if (contractManagementButton != null)
				contractManagementButton.clicked -= OpenContractManagement;
			if (inventoryManagementButton != null)
				inventoryManagementButton.clicked -= OpenInventoryManagement;
			if (ordersManagementButton != null)
				ordersManagementButton.clicked -= OpenOrdersManagement;
			if (workforceManagementButton != null)
				workforceManagementButton.clicked -= OpenWorkforceManagement;
			if (buildManagementButton != null)
				buildManagementButton.clicked -= OpenBuildManagement;
			if (workflowManagementButton != null)
				workflowManagementButton.clicked -= OpenWorkflowManagement;
			if (companyManagementButton != null)
				companyManagementButton.clicked -= OpenCompanyManagement;
		}

		private void BindServices()
		{
			UnbindServices();

			if (GameContext.HasInstance == false)
			{
				Debug.LogWarning("[GlobalStatusHud] GameContext is not ready.", this);
				return;
			}

			economyService = GameContext.Instance.EconomyService;
			hudEventManager = GameContext.Instance.HudEventManager;
			gameTime = GameContext.Instance.GameTime;
			researchService = GameContext.Instance.ResearchService;

			if (economyService != null)
			{
				economyService.OnMoneyChanged += OnMoneyChanged;
				economyService.OnReputationChanged += OnReputationChanged;
			}

			if (hudEventManager != null)
				hudEventManager.OnRecordPublished += OnHudEventPublished;

			if (gameTime != null)
			{
				gameTime.OnTimeScaleChanged += OnTimeScaleChanged;
				gameTime.OnWeekPassed += OnWeekPassed;
			}

			if (researchService != null)
				researchService.OnResearchStateChanged += OnResearchStateChanged;

			RefreshInventoryResearchState();
			RefreshAll();
		}

		private void UnbindServices()
		{
			if (economyService != null)
			{
				economyService.OnMoneyChanged -= OnMoneyChanged;
				economyService.OnReputationChanged -= OnReputationChanged;
			}

			if (hudEventManager != null)
				hudEventManager.OnRecordPublished -= OnHudEventPublished;

			if (gameTime != null)
			{
				gameTime.OnTimeScaleChanged -= OnTimeScaleChanged;
				gameTime.OnWeekPassed -= OnWeekPassed;
			}

			if (researchService != null)
				researchService.OnResearchStateChanged -= OnResearchStateChanged;

			economyService = null;
			hudEventManager = null;
			gameTime = null;
			researchService = null;
		}

		private void OnEconomySummaryClicked(ClickEvent _)
		{
			historyWindow?.OpenEconomy();
		}

		private void OnHudEventAreaClicked(ClickEvent _)
		{
			historyWindow?.OpenEvents();
		}

		private void OnHudGeometryChanged(GeometryChangedEvent _)
		{
			UpdateTimeHudPlacement();
		}

		private void UpdateTimeHudPlacement()
		{
			if (hudRoot == null || leftHud == null || timeCluster == null)
				return;

			float panelWidth = hudRoot.resolvedStyle.width;
			float timeWidth = timeCluster.resolvedStyle.width;
			if (float.IsNaN(panelWidth) || float.IsNaN(timeWidth) || panelWidth <= 0f || timeWidth <= 0f)
				return;

			float centeredLeft = (panelWidth - timeWidth) * 0.5f;
			bool shouldDockRight = leftHud.worldBound.xMax + 10f > centeredLeft;
			if (timeHudDockedRight == shouldDockRight)
				return;

			timeHudDockedRight = shouldDockRight;
			if (shouldDockRight)
			{
				timeCluster.style.left = StyleKeyword.Auto;
				timeCluster.style.right = 12f;
				timeCluster.style.translate = new Translate(0f, 0f);
				return;
			}

			timeCluster.style.left = new Length(50f, LengthUnit.Percent);
			timeCluster.style.right = StyleKeyword.Auto;
			timeCluster.style.translate = new Translate(new Length(-50f, LengthUnit.Percent), 0f);
		}

		private void Pause()
		{
			gameTime?.Pause();
		}

		private void SetNormalSpeed()
		{
			gameTime?.SetNormalSpeed();
		}

		private void DoubleSpeed()
		{
			gameTime?.DoubleSpeed();
		}

		private void ToggleManagementMenu()
		{
			if (managementMenu == null)
				return;

			ShowManagementMenu(managementMenu.resolvedStyle.display == DisplayStyle.None);
		}

		private void ShowManagementMenu(bool visible)
		{
			if (managementMenu == null || managementButton == null)
				return;

			managementMenu.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
			managementButton.EnableInClassList("management-button--open", visible);
		}

		private void OpenContractManagement()
		{
			ShowManagementMenu(false);
			contractManagementWindow?.Open();
		}

		private void OpenInventoryManagement()
		{
			if (IsInventoryManagementUnlocked() == false)
				return;

			ShowManagementMenu(false);
			inventoryManagementWindow?.Open();
		}

		private void OpenInventoryManagementForItem(uint itemId)
		{
			if (IsInventoryManagementUnlocked() == false)
				return;

			inventoryManagementWindow?.OpenForItem(itemId);
		}

		private void OnResearchStateChanged()
		{
			RefreshInventoryResearchState();
		}

		private void RefreshInventoryResearchState()
		{
			inventoryManagementButton?.EnableInClassList(
				"management-menu__button--locked",
				IsInventoryManagementUnlocked() == false);
		}

		private bool IsInventoryManagementUnlocked()
		{
			return researchService?.IsResearched(ResearchIds.InventoryDigitization) == true;
		}

		private UITooltipContent BuildInventoryTooltip()
		{
			const string title = "Global Inventory";
			const string description = "Review global stock, reservations, available quantities, incoming supply, and order demand.";
			if (IsInventoryManagementUnlocked())
				return UITooltipContent.DescriptionOnly(title, description);

			string researchName = ResearchIds.InventoryDigitization;
			if (researchService?.Catalog?.TryGet(ResearchIds.InventoryDigitization, out ResearchDefinition definition) == true)
				researchName = definition.DisplayName;

			return UITooltipContent.Locked(title, description, $"Required research: {researchName}");
		}

		private void OpenOrdersManagement()
		{
			ShowManagementMenu(false);
			orderManagementWindow?.Open();
		}

		private void OpenWorkforceManagement()
		{
			ShowManagementMenu(false);
			workforceManagementWindow?.Open();
		}

		private void OpenBuildManagement()
		{
			ShowManagementMenu(false);
			buildManagementWindow?.Open();
		}

		private void OpenWorkflowManagement()
		{
			ShowManagementMenu(false);
			workflowManagementWindow?.Open();
		}

		private void OpenCompanyManagement()
		{
			ShowManagementMenu(false);
			companyManagementWindow?.Open();
		}

		private void OnMoneyChanged(int value)
		{
			if (moneyValue != null)
				moneyValue.text = $"${value:N0}";
		}

		private void OnReputationChanged(float value)
		{
			if (reputationValue != null)
				reputationValue.text = value.ToString("F1");
		}

		private void OnTimeScaleChanged(float value)
		{
			RefreshSpeed(value);
			RefreshDate();
		}

		private void OnWeekPassed()
		{
			RefreshDate();
		}

		private void OnHudEventPublished(HudEventRecord record)
		{
			if (record == null || hudEventEntryTemplate == null || hudEventList == null)
				return;

			while (activeEvents.Count >= MaxVisibleEvents)
			{
				activeEvents[0].Root.RemoveFromHierarchy();
				activeEvents.RemoveAt(0);
			}

			TemplateContainer entry = hudEventEntryTemplate.CloneTree();
			VisualElement entryRoot = entry.Q<VisualElement>(className: "hud-event-entry");
			Label message = entry.Q<Label>("hud-event-message");
			entryRoot.AddToClassList($"hud-event-entry--{record.Type.ToString().ToLowerInvariant()}");
			message.text = record.Message;
			hudEventList.Add(entry);
			activeEvents.Add(new ActiveHudEvent
			{
				Root = entry,
				Elapsed = 0f,
				VisibleSeconds = record.VisibleSeconds,
			});
		}

		private void RefreshAll()
		{
			OnMoneyChanged(economyService != null ? economyService.Money : 0);
			OnReputationChanged(economyService != null ? economyService.Reputation : 0f);
			RefreshDate();
			RefreshSpeed(gameTime != null ? gameTime.TimeScale : 1f);
		}

		private void RefreshDate()
		{
			if (dateValue == null)
				return;

			dateValue.text = gameTime != null
				? $"{gameTime.Year + 1}년 {gameTime.Month}월 {gameTime.Week}째 주"
				: "1년 1월 1째 주";
		}

		private void RefreshSpeed(float value)
		{
			if (speedValue != null)
				speedValue.text = value <= 0f ? "PAUSED" : $"{value:0.#}x";

			if (doubleSpeedButton != null)
				doubleSpeedButton.SetEnabled(gameTime != null && value < gameTime.MaxTimeScale);
		}
	}
}
