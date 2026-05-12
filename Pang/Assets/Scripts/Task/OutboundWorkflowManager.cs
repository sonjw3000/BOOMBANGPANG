using UnityEditor.Experimental.GraphView;
using UnityEngine;

using static WorkerTask;

// outbound 작업 흐름 관리
// 주문을 까서 picking -> packaging -> loading 작업을 관리

public class OutboundWorkflowManager : MonoBehaviour, IBoundManager
{
	// inbound manager's cargo port service
	[SerializeField] PackingStationService packingStationService;
	[SerializeField] CargoPortService cargoPortService;
	[SerializeField] LaunchStationService launchStationService;

	[SerializeField] private float timeSinceLastOrder = 0.0f;
	[SerializeField] private float orderInterval = 10.0f;

	[SerializeField] private float cargoPortThresholdPercent = 80.0f;

	public PackingStationService PackingStations => packingStationService;
	public CargoPortService CargoPorts => cargoPortService;
	public LaunchStationService LaunchStations => launchStationService;
	private OrderManager OrderMgr => GameContext.Instance.OrderMgr;
	private TaskManager TaskMgr => GameContext.Instance.TaskMgr;


	// 주문을 묶는 역할
	private PickingTaskAllocator pickingTaskAllocator = new TestingPickingTaskAllocator();

	// ----------------------------------------------------------------
	// outbound의 task를 연계생성
	public void OnTaskCompleted(WorkerTask task)
	{
		switch (task.Type)
		{
			case TaskType.Picking:
			case TaskType.Packing:
				break;
			//case TaskType.Sorting:
			//	break;
			//case TaskType.Packaging:
			//	break;
			case TaskType.Loading:
				break;
		}
	}

	// ----------------------------------------------------------------
	// 주문이 들어왔을 때 작업 생성
	private void BuildPickingTaskJob()
	{
		// todo
		// OrderLineQueue가 빌 때 까지 반복해야한다
		var task = pickingTaskAllocator.BuildPickingTask();
		if (task == null)
		{
			//Debug.Log("No Picking Task Created");
			return;
		}

		TaskMgr.EnqueueTask(task);
	}

	// ----------------------------------------------------------------
	// 주문 관련
	public void MakeOrder()
	{
		OrderMgr.CreateRandomOrder();
	}

	private void OnPortItemQuantityChanged(ShelfBase port, uint itemId, int quantityDelta)
	{
		CargoPort cargoPort = (CargoPort)port;

		if (cargoPort.InputReady && cargoPort.FilledPercent >= cargoPortThresholdPercent)
		{
			// build loading task here
			cargoPort.SetInputReady(false);

			LoadingTask loadingTask = new LoadingTask(cargoPort);

			TaskMgr.EnqueueTask(loadingTask);
		}
	}

	public void BuildLoadingTask(CargoPort cargoPort)
	{
		if (cargoPort.InputReady == false)
		{
			Debug.Log("cargo port input is not ready");
			return;
		}

		cargoPort.SetInputReady(false);

		LoadingTask loadingTask = new LoadingTask(cargoPort);

		TaskMgr.EnqueueTask(loadingTask);
	}

	// ----------------------------------------------------------------
	// unity 함수

	private void Awake()
	{
		cargoPortService.OnItemQuantityChanged += OnPortItemQuantityChanged;
	}

	private void OnDestroy()
	{
		cargoPortService.OnItemQuantityChanged -= OnPortItemQuantityChanged;
	}

	private void Start()
	{
		// cargoport 서비스 구독
		//cargoPortService.OnItemPresentChanged;
	}

	void Update()
	{
		timeSinceLastOrder += Time.deltaTime;

		if (timeSinceLastOrder >= orderInterval)
		{
			timeSinceLastOrder = 0.0f;
			MakeOrder();
			BuildPickingTaskJob();
		}

	}
}
