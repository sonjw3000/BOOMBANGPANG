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
	[SerializeField] private PathFindingService pathFindingService;
	[SerializeField] private ZoneManager zoneManager;
	[SerializeField] private FacilityManager facilityManager;
	[SerializeField] private AirlockService airlockService;
	[SerializeField] private BuildingManager buildingManager;
	[SerializeField] private BuildingFootprintService buildingFootprintService;
	[SerializeField] private WorkerStandbyService workerStandbyService;
	[SerializeField] private TrafficCoordinator trafficCoordinator;

	[Header("Workflow Managers")]
	// workflow managers
	[SerializeField] private InboundWorkflowService inboundWorkflowService;
	[SerializeField] private OutboundWorkflowService outboundWorkflowService;

	// go to resource
	[Header("InGame Objects")]
	[SerializeField] private PlaceableCatalog catalog;
	[SerializeField] private BuildPlaceableCatalog buildCatalog;
	[SerializeField] private TileCatalog baseTiles;

	[Header("Risk Service")]
	[SerializeField] private HumanIncidentService humanIncidentService;

	[Header("Worker Visuals")]
	[SerializeField] private WorkerVisualCatalog workerVisualCatalog;

	[Header("UI's Game Tracking")]
	[SerializeField] private ProcessStatsCollector processStats;
	[SerializeField] private MetricsService metrics;
	[SerializeField] private FloatingTextManager floatingTextManager;

	private DeliveryService deliveryService = new();
	private GameSaveService saveService;
	private DemoGoalService demoGoalService;
	private CapsuleRelocateCoordinator capsuleRelocateCoordinator;

	private InteractionContext interactionCtx;

	//public Resources MapResources => mapResources;
	public bool GameCheat => gameCheat;
	public GameTime GameTime => gameTime;
	public EconomyService EconomyService => economyService;
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
	public PathFindingService PathFinding => pathFindingService;
	public TrafficCoordinator TrafficCoordinator
	{
		get
		{
			return ResolveManager(ref trafficCoordinator, nameof(TrafficCoordinator));
		}
	}
	public ZoneManager ZoneMgr
	{
		get
		{
			if (zoneManager == null)
				zoneManager = FindFirstObjectByType<ZoneManager>();

			return zoneManager;
		}
	}

	public AirlockService AirlockSvc
	{
		get
		{
			return ResolveManager(ref airlockService, nameof(AirlockService));
		}
	}

	public BuildingManager BuildingMgr
	{
		get
		{
			return ResolveManager(ref buildingManager, nameof(BuildingManager));
		}
	}

	public FacilityManager FacilityMgr
	{
		get
		{
			return ResolveManager(ref facilityManager, nameof(FacilityManager));
		}
	}

	public BuildingFootprintService BuildingFootprintService
	{
		get
		{
			return ResolveManager(ref buildingFootprintService, nameof(BuildingFootprintService));
		}
	}
	public WorkerStandbyService WorkerStandbyService
	{
		get
		{
			return ResolveManager(ref workerStandbyService, nameof(WorkerStandbyService));
		}
	}

	public InboundWorkflowService IBWorkflowSvc => inboundWorkflowService;
	public OutboundWorkflowService OBWorkflowSvc => outboundWorkflowService;

	public PlaceableCatalog PlaceableCatalog => catalog;
	public PlaceableCatalog PlaceableDefinitionRegistry => catalog;
	public BuildPlaceableCatalog BuildPlaceableCatalog
	{
		get
		{
			if (buildCatalog == null)
				buildCatalog = Resources.Load<BuildPlaceableCatalog>("BuildCatalogs/DefaultBuildPlaceableCatalog");

			return buildCatalog;
		}
	}
	public TileCatalog BaseTiles => baseTiles;

	public HumanIncidentService HumanIncident => humanIncidentService;
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
	public DeliveryService DeliveryService => deliveryService;
	public InteractionContext InteractionCtx => interactionCtx;
	public GameSaveService SaveService => ResolveManager(ref saveService, nameof(GameSaveService));
	public DemoGoalService DemoGoalService => ResolveManager(ref demoGoalService, nameof(DemoGoalService));
	public CapsuleRelocateCoordinator CapsuleRelocateCoordinator
	{
		get
		{
			capsuleRelocateCoordinator ??= new CapsuleRelocateCoordinator(CapsuleDockSvc, CanUseCapsuleRelocateLink);
			return capsuleRelocateCoordinator;
		}
	}

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

		Debug.Log("GameGlobalContext Online!");
		if (instance != null && instance != this)
		{
			Destroy(gameObject);
			Debug.Log("WARNNING!! GameGlobalContext Duplicated");
			return;
		}

		instance = this;
		interactionCtx = new InteractionContext();
		capsuleRelocateCoordinator = new CapsuleRelocateCoordinator(CapsuleDockSvc, CanUseCapsuleRelocateLink);
		_ = FloatingTextManager;
		_ = SaveService;
		_ = DemoGoalService;
		//DontDestroyOnLoad(gameObject);
	}

	private void Start()
	{
		AddEvent();
		LoadMap();
	}

	private void OnDestroy()
	{
		RemoveEvent();
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
	}

	private void AddEvent()
	{
		if (CapsuleDockSvc != null)
		{
			CapsuleDockSvc.OnCapsuleDocked += HandleCapsuleRelocateDocked;
			CapsuleDockSvc.OnCapsuleUndocked += HandleCapsuleRelocateUndocked;
			CapsuleDockSvc.OnDockStateChanged += HandleCapsuleRelocateDockStateChanged;
		}

		// times to process
		gameTime.OnWeekPassed += contractService.AdvanceWeek;
		gameTime.OnWeekPassed += orderManager.CheckExpiredOrders;

		// times for payments
		gameTime.OnMonthPassed += economyService.ProcessMonthlyPayment;

		gridService.OnPlaceableInstalled += economyService.OnPlacement;
	}

	private void RemoveEvent()
	{
		if (CapsuleDockSvc != null)
		{
			CapsuleDockSvc.OnCapsuleDocked -= HandleCapsuleRelocateDocked;
			CapsuleDockSvc.OnCapsuleUndocked -= HandleCapsuleRelocateUndocked;
			CapsuleDockSvc.OnDockStateChanged -= HandleCapsuleRelocateDockStateChanged;
		}

		gameTime.OnWeekPassed -= contractService.AdvanceWeek;
		gameTime.OnWeekPassed -= orderManager.CheckExpiredOrders;
		gameTime.OnMonthPassed -= economyService.ProcessMonthlyPayment;

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
			if (inboundWorkflowService == null)
				return false;

			uint destinationBuildingId = inboundWorkflowService.UnloadingDestinationBuildingId;
			if (destinationBuildingId != 0)
				return destinationBuildingId == targetBuildingId;

			return buildingManager != null && buildingManager.TryGetBuilding(targetBuildingId, out _);
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
