using UnityEngine;
using static WorkerTask;

// outbound 작업 흐름 관리
// 주문을 까서 picking -> packaging -> loading 작업을 관리

public class OutboundWorkflowManager : MonoBehaviour, IBoundManager
{
	private const CollectingPolicyType DefaultCollectingPolicyType = CollectingPolicyType.Nearest;

	[SerializeField] private PackingStationService packingStationService;
	[SerializeField] private CargoPortService cargoPortService;
	[SerializeField] private LaunchStationService launchStationService;
	[SerializeField] private float orderInterval = 10.0f;
	[SerializeField] private float cargoPortThresholdPercent = 80.0f;
	[SerializeField] [Range(1f, 100f)] private float pickingBoxFillLimitPercent = 80.0f;
	[SerializeField] private int maxPickingTasksPerUpdate = 64;
	[SerializeField] private CollectingPolicyType defaultPickingCollectingPolicyType = DefaultCollectingPolicyType;

	private float timeSinceLastOrder = 0.0f;
	private PickingPlanner pickingPlanner;

	public PackingStationService PackingStations => packingStationService;
	public CargoPortService CargoPorts => cargoPortService;
	public LaunchStationService LaunchStations => launchStationService;
	public PickingPlanner PickingPlanner => pickingPlanner;
	public CollectingPolicyType PickingCollectingPolicyType => pickingPlanner != null ? pickingPlanner.CollectingPolicyType : defaultPickingCollectingPolicyType;
	private OrderManager OrderMgr => GameContext.Instance.OrderMgr;
	private TaskManager TaskMgr => GameContext.Instance.TaskMgr;
	private ItemDatabase ItemDB => GameContext.Instance.ItemDB;
	private BoxPoolService BoxPoolMgr => GameContext.Instance.WMSys.BoxPoolMgr;

	public void OnTaskCompleted(WorkerTask task)
	{
		switch (task.Type)
		{
			case TaskType.Picking:
				break;
			case TaskType.Packing:
				if (task is PackingTask packingTask)
					PackingStations.OnPackingTaskCompleted(packingTask.TargetStation);
				break;
			case TaskType.Loading:
				break;
		}
	}

	public void MakeOrder()
	{
		OrderMgr.CreateRandomOrder();
	}

	public void SetPickingCollectingPolicy(CollectingPolicyType policyType)
	{
		defaultPickingCollectingPolicyType = policyType;
		if (pickingPlanner == null)
			return;

		pickingPlanner.SetCollectingPolicy(policyType);
	}

	public OutboundWorkflowPolicySaveData CapturePolicyState()
	{
		return new OutboundWorkflowPolicySaveData
		{
			PickingCollectingPolicy = PickingCollectingPolicyType,
		};
	}

	public void RestorePolicyState(OutboundWorkflowPolicySaveData data)
	{
		CollectingPolicyType policyType = data != null ? data.PickingCollectingPolicy : DefaultCollectingPolicyType;
		SetPickingCollectingPolicy(policyType);
	}

	public void BuildLoadingTask(CargoPort cargoPort)
	{
		if (cargoPort.InputReady == false)
		{
			Debug.Log("cargo port input is not ready");
			return;
		}

		cargoPort.SetInputReady(false);
		TaskMgr.EnqueueTask(new LoadingTask(cargoPort));
	}

	private void Awake()
	{
		cargoPortService.OnItemQuantityChanged += OnPortItemQuantityChanged;
		RebuildPlanner();
	}

	private void OnDestroy()
	{
		cargoPortService.OnItemQuantityChanged -= OnPortItemQuantityChanged;
	}

	private void Update()
	{
		timeSinceLastOrder += Time.deltaTime;
		if (timeSinceLastOrder >= orderInterval)
		{
			timeSinceLastOrder = 0.0f;
			MakeOrder();
		}

		CheckPickingTaskAvailable();
	}

	public void ResetRuntimeState()
	{
		timeSinceLastOrder = 0.0f;
		RebuildPlanner();
	}

	private void CheckPickingTaskAvailable()
	{
		if (pickingPlanner == null || pickingPlanner.HasPendingCollectWork() == false)
			return;

		int desiredTaskCount = GetDesiredPickingTaskCount();
		int currentTaskCount = GetCurrentPickingTaskCount();
		if (desiredTaskCount <= currentTaskCount)
			return;

		int tasksToBuild = Mathf.Min(maxPickingTasksPerUpdate, Mathf.Max(0, desiredTaskCount - currentTaskCount));
		for (int i = 0; i < tasksToBuild; ++i)
		{
			if (pickingPlanner.BuildPickingTask(out var task) == false)
				break;

			if (task != null)
				TaskMgr.EnqueueTask(task);
		}
	}

	private void OnPortItemQuantityChanged(ShelfBase port, uint itemId, int quantityDelta)
	{
		CargoPort cargoPort = (CargoPort)port;
		if (cargoPort.InputReady && cargoPort.FilledPercent >= cargoPortThresholdPercent)
		{
			cargoPort.SetInputReady(false);
			TaskMgr.EnqueueTask(new LoadingTask(cargoPort));
		}
	}

	private void RebuildPlanner()
	{
		pickingPlanner = new PickingPlanner(
			GameContext.Instance.StorageIndex,
			GameContext.Instance.OrderMgr,
			pickingBoxFillLimitPercent,
			defaultPickingCollectingPolicyType);
	}

	private int GetCurrentPickingTaskCount()
	{
		return TaskMgr.TaskQueue[TaskType.Picking].Count + TaskMgr.TaskOnProgress[TaskType.Picking].Count;
	}

	private int GetDesiredPickingTaskCount()
	{
		float effectiveBoxCapacity = GetEffectivePickingBoxCapacity();
		if (effectiveBoxCapacity <= 0.0f)
			return 0;

		float totalOutstandingSize = OrderMgr != null ? OrderMgr.GetOutstandingPickingTotalSize(ItemDB) : 0.0f;
		if (totalOutstandingSize <= 0.0f)
			return 0;

		return Mathf.CeilToInt(totalOutstandingSize / effectiveBoxCapacity);
	}

	private float GetEffectivePickingBoxCapacity()
	{
		float toteCapacity = BoxPoolMgr != null ? BoxPoolMgr.ToteCapacity : 0.0f;
		if (toteCapacity <= 0.0f)
			return 0.0f;

		return toteCapacity * Mathf.Clamp01(pickingBoxFillLimitPercent / 100.0f);
	}
}
