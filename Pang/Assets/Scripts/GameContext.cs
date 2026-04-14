using UnityEngine;

// 이것만은 꼭 지키자
// GameContext는 데이터만 가진다
// 로직을 가져선 안된다

[DefaultExecutionOrder(-100)]
public class GameContext : MonoBehaviour
{
	public const bool CHEATMODE = true;

	private static GameContext instance;
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
	// 나중에 다른 곳으로 빼도 될 듯?

	// datas
	//[SerializeField] private Resources mapResources;
	[SerializeField] private bool gameCheat = false;
	
	[Header("시간")]
	[SerializeField] private GameTime gameTime;

	[Header("경제")]
	[SerializeField] private EconomyService economyService;

	[Header("아이템 데이터베이스")]
	[SerializeField] private ItemDatabase itemDB;

	[Header("맵")]
	//[SerializeField] private GridMap gridMap;
	[SerializeField] private GridService gridService;
	[SerializeField] private string mapJsonFile;

	[Header("도메인 매니저")]
	// domain managers
	[SerializeField] private WorkerManager workerManager;
	[SerializeField] private TaskManager taskManager;
	[SerializeField] private ShelfStorageIndex itemInventory;
	[SerializeField] private RocketManager rocketManager;
	[SerializeField] private OrderManager orderManager;
	[SerializeField] private WMSystem warehouseManagement;
	[SerializeField] private ContractService contractService;

	[Header("워크플로우 매니저")]
	// workflow managers
	[SerializeField] private InboundWorkflowManager inboundWorkFlowManager;
	[SerializeField] private OutboundWorkflowManager outboundWorkFlowManager;

	// go to resource
	[Header("InGame Objects")]
	[SerializeField] private PlaceableCatalog catalog;
	[SerializeField] private TileCatalog baseTiles;

	[Header("Risk Service")]
	[SerializeField] private HumanIncidentService humanIncidentService;

	[Header("UI관련해서 추가함")]
	[SerializeField] private ProcessStatsCollector processStats;
	[SerializeField] private MetricsService metrics;

	private DeliveryService deliveryService = new();

	//[Header("나중에 빼자")]
	private InteractionContext interactionCtx;

	//public Resources MapResources => mapResources;
	public bool GameCheat => gameCheat;
	public GameTime GameTime => gameTime;
	public EconomyService EconomyService => economyService;
	public ItemDatabase ItemDB => itemDB;
	//public GridMap GridMap => gridMap;
	public GridService GridService => gridService;
	public WorkerManager WorkerMgr => workerManager;
	public TaskManager TaskMgr => taskManager;
	public ShelfStorageIndex StorageIndex => itemInventory;
	public RocketManager RocketMgr => rocketManager;
	public OrderManager OrderMgr => orderManager;
	public WMSystem WMSys => warehouseManagement;
	public ContractService ContractMgr => contractService;

	public InboundWorkflowManager IBWorkflowMgr => inboundWorkFlowManager;
	public OutboundWorkflowManager OBWorkflowMgr => outboundWorkFlowManager;

	public PlaceableCatalog PlaceableCatalog => catalog;
	public TileCatalog BaseTiles => baseTiles;

	public HumanIncidentService HumanIncident => humanIncidentService;

	public ProcessStatsCollector ProcessStats => processStats;
	public MetricsService Metrics => metrics;
	public DeliveryService DeliveryService => deliveryService;
	public InteractionContext InteractionCtx => interactionCtx;

	private void Awake()
	{
		Debug.Log("GameGlobalContext Online!");
		if (instance != null && instance != this)
		{
			Destroy(gameObject);
			Debug.Log("WARNNING!! GameGlobalContext Duplicated");
			return;
		}

		instance = this;
		interactionCtx = new InteractionContext();
		DontDestroyOnLoad(gameObject);

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
		GameSaveLoader loadGame = new();

		// todo
		// 맵 로드 실패시 기본맵 생성
		// 일단은 맵 로드 실패를 가정함
		//if (loadGame.LoadMap(mapJsonFile) == false)
		if (true)
		{
			Debug.LogWarning("No Such Map File!!");
			gridService.BuildDefaultMap();
		}
		else
		{
			gridService.LoadByData(loadGame);
		}

		// on game start
		gridService.OnGameStart();
	}

	// 순서가 중요한 이벤트들은 여기서 등록
	private void AddEvent()
	{
		// times to process
		gameTime.OnWeekPassed += contractService.AdvanceWeek;

		// times for payments
		gameTime.OnMonthPassed += economyService.ProcessMonthlyPayment;

		// 순서가 중요한가?
		// 일단은 여기에서 등록하자
		gridService.OnPlaceableInstalled += economyService.OnPlacement;
	}

	private void RemoveEvent()
	{
		gameTime.OnWeekPassed -= contractService.AdvanceWeek;
		gameTime.OnMonthPassed -= economyService.ProcessMonthlyPayment;

		gridService.OnPlaceableInstalled -= economyService.OnPlacement;
	}
}
