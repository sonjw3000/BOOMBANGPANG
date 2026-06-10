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
	[SerializeField] private ShelfStorageIndex itemInventory;
	[SerializeField] private RocketManager rocketManager;
	[SerializeField] private OrderManager orderManager;
	[SerializeField] private OrderDeliveryService orderDelivery;
	[SerializeField] private WMSystem warehouseManagement;
	[SerializeField] private ContractService contractService;
	[SerializeField] private PathFindingService pathFindingService;
	[SerializeField] private ZoneManager zoneManager;
	[SerializeField] private BuildingManager buildingManager;
	[SerializeField] private BuildingFootprintService buildingFootprintService;
	[SerializeField] private WorkerStandbyService workerStandbyService;
	[SerializeField] private TrafficCoordinator trafficCoordinator;

	[Header("Workflow Managers")]
	// workflow managers
	[FormerlySerializedAs("inboundWorkFlowManager")]
	[SerializeField] private InboundWorkflowService inboundWorkflowService;
	[FormerlySerializedAs("outboundWorkFlowManager")]
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

	private DeliveryService deliveryService = new();
	private GameSaveService saveService;
	private DemoGoalService demoGoalService;

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
	public ShelfStorageIndex StorageIndex => itemInventory;
	public RocketManager RocketMgr => rocketManager;
	public OrderManager OrderMgr => orderManager;
	public OrderDeliveryService OrderDelivery => orderDelivery;
	public WMSystem WMSys => warehouseManagement;
	public ContractService ContractMgr => contractService;
	public PathFindingService PathFinding => pathFindingService;
	public TrafficCoordinator TrafficCoordinator
	{
		get
		{
			if (trafficCoordinator == null)
				trafficCoordinator = GetComponent<TrafficCoordinator>();

			if (trafficCoordinator == null)
				trafficCoordinator = gameObject.AddComponent<TrafficCoordinator>();

			return trafficCoordinator;
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
	public BuildingManager BuildingMgr
	{
		get
		{
			if (buildingManager == null)
				buildingManager = GetComponent<BuildingManager>();

			if (buildingManager == null)
				buildingManager = FindFirstObjectByType<BuildingManager>();

			if (buildingManager == null)
				buildingManager = gameObject.AddComponent<BuildingManager>();

			return buildingManager;
		}
	}
	public BuildingFootprintService BuildingFootprintService
	{
		get
		{
			if (buildingFootprintService == null)
				buildingFootprintService = GetComponent<BuildingFootprintService>();

			if (buildingFootprintService == null)
				buildingFootprintService = FindFirstObjectByType<BuildingFootprintService>();

			if (buildingFootprintService == null)
				buildingFootprintService = gameObject.AddComponent<BuildingFootprintService>();

			return buildingFootprintService;
		}
	}
	public WorkerStandbyService WorkerStandbyService
	{
		get
		{
			if (workerStandbyService == null)
				workerStandbyService = GetComponent<WorkerStandbyService>();

			if (workerStandbyService == null)
				workerStandbyService = gameObject.AddComponent<WorkerStandbyService>();

			return workerStandbyService;
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
	public DeliveryService DeliveryService => deliveryService;
	public InteractionContext InteractionCtx => interactionCtx;
	public GameSaveService SaveService => saveService;
	public DemoGoalService DemoGoalService => demoGoalService;

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
		saveService = GetComponent<GameSaveService>();
		if (saveService == null)
			saveService = gameObject.AddComponent<GameSaveService>();

		demoGoalService = GetComponent<DemoGoalService>();
		if (demoGoalService == null)
			demoGoalService = gameObject.AddComponent<DemoGoalService>();
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
		// times to process
		gameTime.OnWeekPassed += contractService.AdvanceWeek;
		gameTime.OnWeekPassed += orderManager.CheckExpiredOrders;

		// times for payments
		gameTime.OnMonthPassed += economyService.ProcessMonthlyPayment;

		gridService.OnPlaceableInstalled += economyService.OnPlacement;
	}

	private void RemoveEvent()
	{
		gameTime.OnWeekPassed -= contractService.AdvanceWeek;
		gameTime.OnWeekPassed -= orderManager.CheckExpiredOrders;
		gameTime.OnMonthPassed -= economyService.ProcessMonthlyPayment;

		gridService.OnPlaceableInstalled -= economyService.OnPlacement;
	}
}
