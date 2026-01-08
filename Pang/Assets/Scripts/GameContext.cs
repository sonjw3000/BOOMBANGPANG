using UnityEngine;

// 이것만은 꼭 지키자
// GameContext는 데이터만 가진다
// 로직을 가져선 안된다

[DefaultExecutionOrder(-100)]
public class GameContext : MonoBehaviour
{
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
	[SerializeField] private float timeScale = 1.0f;

	// datas
	//[SerializeField] private Resources mapResources;
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

	[Header("워크플로우 매니저")]
	// workflow managers
	[SerializeField] private InboundWorkflowManager inboundWorkFlowManager;
	[SerializeField] private OutboundWorkflowManager outboundWorkFlowManager;

	// go to resource
	[Header("InGame Objects")]
	[SerializeField] private PlaceableCatalog catalog;
	[SerializeField] private TileCatalog baseTiles;

	//public Resources MapResources => mapResources;
	public ItemDatabase ItemDB => itemDB;
	//public GridMap GridMap => gridMap;
	public GridService GridService => gridService;
	public WorkerManager WorkerMgr => workerManager;
	public TaskManager TaskMgr => taskManager;
	public ShelfStorageIndex StorageIndex => itemInventory;
	public RocketManager RocketMgr => rocketManager;
	public OrderManager OrderMgr => orderManager;
	public WMSystem WMSys => warehouseManagement;

	public InboundWorkflowManager IBWorkflowMgr => inboundWorkFlowManager;
	public OutboundWorkflowManager OBWorkflowMgr => outboundWorkFlowManager;

	public PlaceableCatalog PlaceableCatalog => catalog;
	public TileCatalog BaseTiles => baseTiles;

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
		//instance.mapResources.Initialize();
		DontDestroyOnLoad(gameObject);
	}

	private void Start()
	{
		GameSaveLoader loadGame = new GameSaveLoader();
		
		if (loadGame.LoadMap(mapJsonFile) == false)
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

	private void Update()
	{
		Time.timeScale = timeScale;
	}

}
