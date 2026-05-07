using UnityEngine;

// �̰͸��� �� ��Ű��
// GameContext�� �����͸� ������
// ������ ������ �ȵȴ�

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
	// ���߿� �ٸ� ������ ���� �� ��?

	// datas
	//[SerializeField] private Resources mapResources;
	[SerializeField] private bool gameCheat = false;
	
	[Header("�ð�")]
	[SerializeField] private GameTime gameTime;

	[Header("����")]
	[SerializeField] private EconomyService economyService;

	[Header("������ �����ͺ��̽�")]
	[SerializeField] private ItemDatabase itemDB;

	[Header("��")]
	//[SerializeField] private GridMap gridMap;
	[SerializeField] private GridService gridService;
	[SerializeField] private string mapJsonFile;

	[Header("������ �Ŵ���")]
	// domain managers
	[SerializeField] private WorkerManager workerManager;
	[SerializeField] private TaskManager taskManager;
	[SerializeField] private ShelfStorageIndex itemInventory;
	[SerializeField] private RocketManager rocketManager;
	[SerializeField] private OrderManager orderManager;
	[SerializeField] private OrderDeliveryManager orderDelivery;
	[SerializeField] private WMSystem warehouseManagement;
	[SerializeField] private ContractService contractService;
	[SerializeField] private PathFindingService pathFindingService;

	[Header("��ũ�÷ο� �Ŵ���")]
	// workflow managers
	[SerializeField] private InboundWorkflowManager inboundWorkFlowManager;
	[SerializeField] private OutboundWorkflowManager outboundWorkFlowManager;

	// go to resource
	[Header("InGame Objects")]
	[SerializeField] private PlaceableCatalog catalog;
	[SerializeField] private TileCatalog baseTiles;

	[Header("Risk Service")]
	[SerializeField] private HumanIncidentService humanIncidentService;

	[Header("UI�����ؼ� �߰���")]
	[SerializeField] private ProcessStatsCollector processStats;
	[SerializeField] private MetricsService metrics;

	private DeliveryService deliveryService = new();

	//[Header("���߿� ����")]
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
	public OrderDeliveryManager OrderDelivery => orderDelivery;
	public WMSystem WMSys => warehouseManagement;
	public ContractService ContractMgr => contractService;
	public PathFindingService PathFinding => pathFindingService;

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
		// �� �ε� ���н� �⺻�� ����
		// �ϴ��� �� �ε� ���и� ������
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

	// ������ �߿��� �̺�Ʈ���� ���⼭ ���
	private void AddEvent()
	{
		// times to process
		gameTime.OnWeekPassed += contractService.AdvanceWeek;
		gameTime.OnWeekPassed += orderManager.CheckExpiredOrders;

		// times for payments
gameTime.OnMonthPassed += economyService.ProcessMonthlyPayment;

		// ������ �߿��Ѱ�?
		// �ϴ��� ���⿡�� �������
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
