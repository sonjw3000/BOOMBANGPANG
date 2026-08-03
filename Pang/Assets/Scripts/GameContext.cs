using UnityEngine;

using UnityEngine.Serialization;

[DefaultExecutionOrder(-100)]
public class GameContext : MonoBehaviour
{
	public const bool CHEATMODE = true;

	private static GameContext instance;
	public static bool HasInstance => instance != null;
	public static GameContext Instance
	{
		get
		{
			if (instance == null)
			{
				Debug.LogError("GameGlobalContext is NOT initialized!");
				return null;
			}
			return instance;
		}
	}

	// game system multiplier
	// todo
	
	// datas
	//[SerializeField] private Resources mapResources;
	[SerializeField] private bool gameCheat = false;
	
	[Header("Time")]
	[SerializeField] private GameTime gameTime;

	[Header("Company Economy")]
	[SerializeField] private EconomyService economyService;

	[Header("Item Data")]
	[SerializeField] private ItemDatabase itemDB;

	[Header("Map")]
	//[SerializeField] private GridMap gridMap;
	[SerializeField] private GridService gridService;
	[SerializeField] private string mapJsonFile;

	[Header("Domain Managers")]
	// domain managers
	[SerializeField] private WorkerManager workerManager;
	[SerializeField] private WorkerSpawnManager workerSpawnManager;
	[SerializeField] private TaskManager taskManager;
	[FormerlySerializedAs("itemInventory")]
	[SerializeField] private ShelfStorageService shelfStorageService;
	[SerializeField] private BoxManager boxManager;
	[FormerlySerializedAs("rocketManager")]
	[SerializeField] private RocketService rocketService;
	[SerializeField] private CargoPortService cargoPortService;
	[SerializeField] private CapsuleDockService capsuleDockService;
	[SerializeField] private CapsuleBufferService capsuleBufferService;
	[SerializeField] private OrderManager orderManager;
	[SerializeField] private OrderDeliveryService orderDelivery;
	[SerializeField] private WMSystem warehouseManagement;
	[SerializeField] private ContractService contractService;
	[SerializeField] private LicenseService licenseService;
	[SerializeField] private ResearchCatalog researchCatalog;
	[SerializeField] private PathFindingService pathFindingService;
	[FormerlySerializedAs("zoneManager")]
	[SerializeField] private AreaManager areaManager;
	[SerializeField] private FacilityManager facilityManager;
	[SerializeField] private ChargingFacilityService chargingFacilityService;
	[SerializeField] private RestFacilityService restFacilityService;
	[SerializeField] private FacilityRuleManager facilityRuleManager;
	[SerializeField] private FacilityRuleOverlayController facilityRuleOverlayController;
	[SerializeField] private AirlockService airlockService;
	[SerializeField] private BuildingManager buildingManager;
	[SerializeField] private BuildingFootprintService buildingFootprintService;
	[SerializeField] private TrafficCoordinator trafficCoordinator;
	[SerializeField] private VendorService vendorService;
	[SerializeField] private PowerService powerService;
	[SerializeField] private MedicalService medicalService;
	[SerializeField] private RobotFixService robotFixService;
	[SerializeField] private WorkplaceIncidentService workplaceIncidentService;
	[SerializeField] private TemperatureService temperatureService;
	[SerializeField] private OxygenService oxygenService;
	[SerializeField] private WearService wearService;
	[SerializeField] private GridOverlayController gridOverlayController;

	[Header("Workflow Managers")]
	// workflow managers
	[SerializeField] private InboundWorkflowService inboundWorkflowService;
	[SerializeField] private OutboundWorkflowService outboundWorkflowService;

	// go to resource
	[Header("InGame Objects")]
	[SerializeField] private PlaceableCatalog catalog;
	[SerializeField] private BuildPlaceableCatalog buildCatalog;
	[SerializeField] private BuildingAddonCatalog buildingAddonCatalog;
	[SerializeField] private TileCatalog baseTiles;

	[Header("Risk Service")]
	[SerializeField] private HumanIncidentService humanIncidentService;
	[SerializeField] private ItemHandlingDamageService itemHandlingDamageService;
	[SerializeField] private ItemDamageService itemDamageService;
	private FireService fireService;
	private ExplosionService explosionService;
	private ContaminationService contaminationService;
	private CorrosionService corrosionService;
	private RadiationService radiationService;

	[Header("Worker Visuals")]
	[SerializeField] private WorkerVisualCatalog workerVisualCatalog;

	[Header("UI's Game Tracking")]
	[SerializeField] private ProcessStatsCollector processStats;
	[SerializeField] private MetricsService metrics;
	[SerializeField] private FloatingTextManager floatingTextManager;
	[SerializeField] private HudEventManager hudEventManager;

	private DeliveryService deliveryService = new();
	private readonly ResearchService researchService = new();
	private readonly BuildingAddonService buildingAddonService = new();
	private GameSaveService saveService;
	private ScenarioObjectiveService scenarioObjectiveService;
	private CapsuleRelocateCoordinator capsuleRelocateCoordinator;
	private ItemTransferTaskScheduler itemTransferTaskScheduler;
	private SimulationTickCoordinator simulationTickCoordinator;
	private ItemThermalService itemThermalService;

	private InteractionContext interactionCtx;
	private bool eventsBound;

	private void OnValidate()
	{
		researchCatalog?.ValidateKeys();
		buildingAddonCatalog?.ValidateKeys();
	}

	//public Resources MapResources => mapResources;
	public bool GameCheat => gameCheat;
	public GameTime GameTime => gameTime;
	public EconomyService EconomyService => economyService;
	public ResearchService ResearchService => researchService;
	public ItemDatabase ItemDB => itemDB;
	//public GridMap GridMap => gridMap;
	public GridService GridService => gridService;
	public WorkerManager WorkerMgr => workerManager;
	public WorkerSpawnManager WorkerSpawnMgr
	{
		get
		{
			if (workerSpawnManager == null)
				workerSpawnManager = FindFirstObjectByType<WorkerSpawnManager>();

			return workerSpawnManager;
		}
	}
	public TaskManager TaskMgr => taskManager;
	public ShelfStorageService StorageService => shelfStorageService;
	public BoxManager BoxMgr => boxManager;
	public RocketService RocketSvc => rocketService;
	public CargoPortService CargoPortSvc => cargoPortService;
	public CapsuleDockService CapsuleDockSvc
	{
		get
		{
			return ResolveManager(ref capsuleDockService, nameof(CapsuleDockService));
		}
	}
	public CapsuleBufferService CapsuleBufferSvc
	{
		get
		{
			return ResolveManager(ref capsuleBufferService, nameof(CapsuleBufferService));
		}
	}
	public OrderManager OrderMgr => orderManager;
	public OrderDeliveryService OrderDelivery => orderDelivery;
	public WMSystem WMSys => warehouseManagement;
	public ContractService ContractMgr => contractService;
	public LicenseService LicenseService => ResolveManager(ref licenseService, nameof(LicenseService));
	public PathFindingService PathFinding => pathFindingService;
	public TrafficCoordinator TrafficCoordinator => trafficCoordinator;
	public VendorService VendorService => ResolveManager(ref vendorService, nameof(VendorService));
	public VendorService VendeorService => VendorService;
	public PowerService PowerSvc => ResolveOrCreatePowerService();
	public MedicalService MedicalSvc => ResolveOrCreateMedicalService();
	public RobotFixService RobotFixSvc => ResolveOrCreateRobotFixService();
	public WorkplaceIncidentService WorkplaceIncidentSvc => ResolveOrCreateWorkplaceIncidentService();
	public TemperatureService TemperatureSvc => ResolveOrCreateTemperatureService();
	public ItemThermalService ItemThermalSvc => itemThermalService ??= new ItemThermalService();
	public OxygenService OxygenSvc => ResolveOrCreateOxygenService();
	public WearService WearSvc => ResolveOrCreateWearService();
	public AreaManager AreaMgr => areaManager;
	public AirlockService AirlockSvc => airlockService;
	public BuildingManager BuildingMgr => buildingManager;
	public BuildingAddonService BuildingAddonSvc => buildingAddonService;
	public FacilityManager FacilityMgr => facilityManager;
	public ChargingFacilityService ChargingFacilitySvc =>
		ResolveManager(ref chargingFacilityService, nameof(ChargingFacilityService));
	public RestFacilityService RestFacilitySvc =>
		ResolveManager(ref restFacilityService, nameof(RestFacilityService));
	public FacilityRuleManager FacilityRuleMgr => facilityRuleManager;
	public FacilityRuleOverlayController FacilityRuleOverlay => facilityRuleOverlayController;

	public BuildingFootprintService BuildingFootprintService => buildingFootprintService;
	public InboundWorkflowService IBWorkflowSvc => inboundWorkflowService;
	public OutboundWorkflowService OBWorkflowSvc => outboundWorkflowService;

	public PlaceableCatalog PlaceableCatalog => catalog;
	public PlaceableCatalog PlaceableDefinitionRegistry => catalog;
	public BuildPlaceableCatalog BuildPlaceableCatalog => buildCatalog;
	public TileCatalog BaseTiles => baseTiles;

	public HumanIncidentService HumanIncident => humanIncidentService;
	public ItemHandlingDamageService ItemHandlingDamage => ResolveOrCreateItemHandlingDamageService();
	public ItemDamageService ItemDamage => ResolveOrCreateItemDamageService();
	public FireService FireSvc => fireService ??= new FireService();
	public ExplosionService ExplosionSvc
	{
		get
		{
			explosionService ??= new ExplosionService();
			explosionService.Initialize(gameTime);
			return explosionService;
		}
	}
	public ContaminationService ContaminationSvc => contaminationService ??= new ContaminationService();
	public CorrosionService CorrosionSvc => corrosionService ??= new CorrosionService();
	public RadiationService RadiationSvc => radiationService ??= new RadiationService();
	public WorkerVisualCatalog WorkerVisualCatalog
	{
		get
		{
			if (workerVisualCatalog == null)
				workerVisualCatalog = Resources.Load<WorkerVisualCatalog>("Worker/DefaultWorkerVisualCatalog");

			return workerVisualCatalog;
		}
	}

	public ProcessStatsCollector ProcessStats => processStats;
	public MetricsService Metrics => metrics;
	public FloatingTextManager FloatingTextManager
	{
		get
		{
			return ResolveManager(ref floatingTextManager, nameof(FloatingTextManager));
		}
	}
	public HudEventManager HudEventManager
	{
		get
		{
			return ResolveOrCreateHudEventManager();
		}
	}
	public DeliveryService DeliveryService => deliveryService;
	public InteractionContext InteractionCtx => interactionCtx;
	public GameSaveService SaveService => ResolveManager(ref saveService, nameof(GameSaveService));
	public ScenarioObjectiveService ScenarioObjectiveService =>
		ResolveManager(ref scenarioObjectiveService, nameof(ScenarioObjectiveService));
	public CapsuleRelocateCoordinator CapsuleRelocateCoordinator
	{
		get
		{
			capsuleRelocateCoordinator ??= new CapsuleRelocateCoordinator(CapsuleDockSvc, CanUseCapsuleRelocateLink);
			return capsuleRelocateCoordinator;
		}
	}
	public ItemTransferTaskScheduler ItemTransferTaskScheduler => itemTransferTaskScheduler ??= new ItemTransferTaskScheduler();

	private T ResolveManager<T>(ref T field, string componentName) where T : Component
	{
		if (field != null)
			return field;

		field = GetComponentInChildren<T>(true);
		if (field == null)
			Debug.LogError($"[GameContext] {componentName} is missing under Managers.");

		return field;
	}

	private void Awake()
	{
		Application.runInBackground = true;

		if (BindInstance() == false)
			return;

		EnsureRuntimeState();
		//DontDestroyOnLoad(gameObject);
	}

	private void OnEnable()
	{
		if (BindInstance() == false)
			return;

		EnsureRuntimeState();
		BindEvents();
	}

	private void Start()
	{
		LoadMap();
	}

	private void OnDisable()
	{
		simulationTickCoordinator?.Unbind();
		itemThermalService?.Unbind();
		fireService?.Unbind();
		workplaceIncidentService?.Unbind();
		UnbindEvents();
	}

	private void OnDestroy()
	{
		UnbindEvents();
		if (instance == this)
			instance = null;
	}

	private bool BindInstance()
	{
		if (instance == this)
			return true;

		if (instance == null)
		{
			instance = this;
			Debug.Log("GameGlobalContext Online!");
			return true;
		}

		Debug.LogWarning("WARNNING!! GameGlobalContext Duplicated");
		Destroy(gameObject);
		return false;
	}

	private void EnsureRuntimeState()
	{
		deliveryService ??= new DeliveryService();
		researchService.Initialize(researchCatalog, economyService, gameTime);
		buildingAddonService.Initialize(buildingAddonCatalog, buildingManager, economyService, researchService);
		interactionCtx ??= new InteractionContext();
		capsuleRelocateCoordinator ??= new CapsuleRelocateCoordinator(CapsuleDockSvc, CanUseCapsuleRelocateLink);
		itemTransferTaskScheduler ??= new ItemTransferTaskScheduler();
		_ = FloatingTextManager;
		_ = HudEventManager;
		_ = SaveService;
		_ = ScenarioObjectiveService;
		_ = PowerSvc;
		_ = MedicalSvc;
		_ = RobotFixSvc;
		WorkplaceIncidentSvc.Initialize(WorkerMgr, MedicalSvc, RobotFixSvc, VendorService, EconomyService);
		_ = TemperatureSvc;
		_ = OxygenSvc;
		_ = WearSvc;
		_ = ItemHandlingDamage;
		_ = ItemDamage;
		ItemThermalSvc.Bind(FacilityMgr, BoxMgr, BuildingMgr, gridService, itemDB, itemDamageService);
		_ = FireSvc;
		fireService.Bind(gridService);
		_ = ExplosionSvc;
		simulationTickCoordinator ??= new SimulationTickCoordinator();
		simulationTickCoordinator.Bind(
			gameTime,
			explosionService,
			oxygenService,
			temperatureService,
			itemThermalService,
			fireService,
			wearService,
			workerManager);
		_ = ContaminationSvc;
		_ = CorrosionSvc;
		_ = RadiationSvc;
		_ = ResolveOrCreateGridOverlayController();
		_ = LicenseService;
	}

	private PowerService ResolveOrCreatePowerService()
	{
		if (powerService != null)
			return powerService;

		powerService = GetComponentInChildren<PowerService>(true);
		if (powerService == null)
			powerService = gameObject.AddComponent<PowerService>();

		return powerService;
	}

	private MedicalService ResolveOrCreateMedicalService()
	{
		if (medicalService != null)
			return medicalService;

		medicalService = GetComponentInChildren<MedicalService>(true);
		if (medicalService == null)
			medicalService = gameObject.AddComponent<MedicalService>();

		return medicalService;
	}

	private RobotFixService ResolveOrCreateRobotFixService()
	{
		if (robotFixService != null)
			return robotFixService;

		robotFixService = GetComponentInChildren<RobotFixService>(true);
		if (robotFixService == null)
			robotFixService = gameObject.AddComponent<RobotFixService>();

		return robotFixService;
	}

	private WorkplaceIncidentService ResolveOrCreateWorkplaceIncidentService()
	{
		if (workplaceIncidentService != null)
			return workplaceIncidentService;

		workplaceIncidentService = GetComponentInChildren<WorkplaceIncidentService>(true);
		if (workplaceIncidentService == null)
			workplaceIncidentService = gameObject.AddComponent<WorkplaceIncidentService>();

		return workplaceIncidentService;
	}

	private TemperatureService ResolveOrCreateTemperatureService()
	{
		if (temperatureService != null)
			return temperatureService;

		temperatureService = GetComponentInChildren<TemperatureService>(true);
		if (temperatureService == null)
			temperatureService = gameObject.AddComponent<TemperatureService>();

		return temperatureService;
	}

	private OxygenService ResolveOrCreateOxygenService()
	{
		if (oxygenService != null)
			return oxygenService;

		oxygenService = GetComponentInChildren<OxygenService>(true);
		if (oxygenService == null)
			oxygenService = gameObject.AddComponent<OxygenService>();

		return oxygenService;
	}

	private WearService ResolveOrCreateWearService()
	{
		if (wearService != null)
			return wearService;

		wearService = GetComponentInChildren<WearService>(true);
		if (wearService == null)
			wearService = gameObject.AddComponent<WearService>();

		return wearService;
	}

	private ItemHandlingDamageService ResolveOrCreateItemHandlingDamageService()
	{
		if (itemHandlingDamageService != null)
			return itemHandlingDamageService;

		itemHandlingDamageService = GetComponentInChildren<ItemHandlingDamageService>(true);
		if (itemHandlingDamageService == null)
			itemHandlingDamageService = gameObject.AddComponent<ItemHandlingDamageService>();

		return itemHandlingDamageService;
	}

	private ItemDamageService ResolveOrCreateItemDamageService()
	{
		if (itemDamageService != null)
			return itemDamageService;

		itemDamageService = GetComponentInChildren<ItemDamageService>(true);
		if (itemDamageService == null)
			itemDamageService = gameObject.AddComponent<ItemDamageService>();

		return itemDamageService;
	}

	private GridOverlayController ResolveOrCreateGridOverlayController()
	{
		if (gridOverlayController != null)
			return gridOverlayController;

		gridOverlayController = GetComponentInChildren<GridOverlayController>(true);
		if (gridOverlayController == null)
			gridOverlayController = gameObject.AddComponent<GridOverlayController>();

		return gridOverlayController;
	}

	private HudEventManager ResolveOrCreateHudEventManager()
	{
		if (hudEventManager != null)
			return hudEventManager;

		hudEventManager = GetComponentInChildren<HudEventManager>(true);
		if (hudEventManager != null)
			return hudEventManager;

		GameObject managerObject = new("HudEventManager");
		managerObject.transform.SetParent(transform, false);
		hudEventManager = managerObject.AddComponent<HudEventManager>();
		return hudEventManager;
	}

	private void BindEvents()
	{
		if (eventsBound)
			return;

		AddEvent();
		eventsBound = true;
	}

	private void UnbindEvents()
	{
		researchService.Unbind();

		if (eventsBound == false)
			return;

		RemoveEvent();
		eventsBound = false;
	}

	private void LoadMap()
	{
		// todo
		//if (loadGame.LoadMap(mapJsonFile) == false)
		if (true)
		{
			Debug.LogWarning("No Such Map File!!");
			gridService.BuildDefaultMap();
		}
		else
		{

		}

		// on game start
		gridService.OnGameStart();
		TemperatureSvc.RebuildRuntimeState();
		ItemThermalSvc.RebuildRuntimeState();
	}

	private void AddEvent()
	{
		TaskMgr?.BindFacilityInvalidation(FacilityMgr);

		if (CapsuleDockSvc != null)
		{
			CapsuleDockSvc.OnCapsuleDocked += HandleCapsuleRelocateDocked;
			CapsuleDockSvc.OnCapsuleUndocked += HandleCapsuleRelocateUndocked;
			CapsuleDockSvc.OnDockStateChanged += HandleCapsuleRelocateDockStateChanged;
		}

		// times to process
		gameTime.OnWeekPassed += contractService.AdvanceWeek;
		gameTime.OnWeekPassed += orderManager.CheckExpiredOrders;
		if (VendorService != null)
			gameTime.OnWeekPassed += VendorService.OnWeekPass;

		// times for payments
		gameTime.OnMonthPassed += economyService.ProcessMonthlyPayment;
		if (LicenseService != null)
			gameTime.OnMonthPassed += LicenseService.ReevaluateAcquiredLicenses;

		gridService.OnPlaceableInstalled += economyService.OnPlacement;
	}

	private void RemoveEvent()
	{
		TaskMgr?.UnbindFacilityInvalidation();

		if (CapsuleDockSvc != null)
		{
			CapsuleDockSvc.OnCapsuleDocked -= HandleCapsuleRelocateDocked;
			CapsuleDockSvc.OnCapsuleUndocked -= HandleCapsuleRelocateUndocked;
			CapsuleDockSvc.OnDockStateChanged -= HandleCapsuleRelocateDockStateChanged;
		}

		gameTime.OnWeekPassed -= contractService.AdvanceWeek;
		gameTime.OnWeekPassed -= orderManager.CheckExpiredOrders;
		if (VendorService != null)
			gameTime.OnWeekPassed -= VendorService.OnWeekPass;

		gameTime.OnMonthPassed -= economyService.ProcessMonthlyPayment;
		if (LicenseService != null)
			gameTime.OnMonthPassed -= LicenseService.ReevaluateAcquiredLicenses;

		gridService.OnPlaceableInstalled -= economyService.OnPlacement;
	}

	private void HandleCapsuleRelocateDocked(uint buildingId, CapsuleDock dock)
	{
		CapsuleRelocateCoordinator.NotifyCapsuleDocked(dock);
	}

	private void HandleCapsuleRelocateUndocked(uint buildingId, CapsuleDock dock)
	{
		CapsuleRelocateCoordinator.NotifyCapsuleUndocked(dock);
	}

	private void HandleCapsuleRelocateDockStateChanged(uint buildingId, CapsuleDock dock)
	{
		CapsuleRelocateCoordinator.NotifyDockStateChanged(dock);
	}

	private bool CanUseCapsuleRelocateLink(uint sourceBuildingId, uint targetBuildingId)
	{
		if (targetBuildingId == 0)
			return false;

		if (sourceBuildingId == 0)
		{
			if (areaManager != null)
			{
				var areas = areaManager.RegisteredAreas;
				for (int i = 0; i < areas.Count; ++i)
				{
					Area area = areas[i];
					if (area != null && area.Type == AreaType.RocketLanding && area.DestinationBuildingId == targetBuildingId)
						return true;
				}
			}

			return false;
		}

		if (buildingManager == null ||
			buildingManager.TryGetBuilding(sourceBuildingId, out Building sourceBuilding) == false ||
			sourceBuilding == null)
		{
			return false;
		}

		return sourceBuilding.HasOutputBuilding(targetBuildingId);
	}
}
