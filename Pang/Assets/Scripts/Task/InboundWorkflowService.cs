using UnityEngine;
using static WorkerTask.TaskType;

// inbound 작업 흐름을 관리
// 깨차

// rocket 착륙
// payload unload
// labeling
// storing

public class InboundWorkflowService : MonoBehaviour, IBoundService
{
	private const CollectingPolicyType DefaultCollectingPolicyType = CollectingPolicyType.Nearest;
	private const PlacingPolicyType DefaultPlacingPolicyType = PlacingPolicyType.BelowAverageFilledNearest;

	[SerializeField] private CargoPortService cargoPortService;
	[SerializeField] private InboundRequestService requestService;
	[SerializeField] private int maxStoreTasksPerUpdate = 64;
	[SerializeField] [Range(1f, 100f)] private float storingBoxFillLimitPercent = 80.0f;
	[SerializeField] private CollectingPolicyType defaultStoringCollectingPolicyType = DefaultCollectingPolicyType;
	[SerializeField] private PlacingPolicyType defaultStoringPlacingPolicyType = DefaultPlacingPolicyType;

	private StoringPlanner storingPlanner;

	public CargoPortService CargoPortService => cargoPortService;
	public InboundRequestService RequestService => requestService;
	public StoringPlanner StoringPlanner => storingPlanner;
	private TaskManager TaskMgr => GameContext.Instance.TaskMgr;
	private ItemDatabase ItemDB => GameContext.Instance.ItemDB;
	private BoxPoolManager BoxPoolManager => GameContext.Instance.WMSys.BoxPoolManager;
	public CollectingPolicyType StoringCollectingPolicyType => storingPlanner != null ? storingPlanner.CollectingPolicyType : defaultStoringCollectingPolicyType;
	public PlacingPolicyType StoringPlacingPolicyType => storingPlanner != null ? storingPlanner.PlacingPolicyType : defaultStoringPlacingPolicyType;

	public void SetStoringCollectingPolicy(CollectingPolicyType policyType)
	{
		defaultStoringCollectingPolicyType = policyType;
		if (storingPlanner == null)
			return;

		storingPlanner.SetCollectingPolicy(policyType);
	}

	public void SetStoringPlacingPolicy(PlacingPolicyType policyType)
	{
		defaultStoringPlacingPolicyType = policyType;
		if (storingPlanner == null)
			return;

		storingPlanner.SetPlacingPolicy(policyType);
	}

	public InboundWorkflowPolicySaveData CapturePolicyState()
	{
		return new InboundWorkflowPolicySaveData
		{
			StoringCollectingPolicy = StoringCollectingPolicyType,
			StoringPlacingPolicy = StoringPlacingPolicyType,
		};
	}

	public void RestorePolicyState(InboundWorkflowPolicySaveData data)
	{
		CollectingPolicyType collectingPolicyType = data != null ? data.StoringCollectingPolicy : DefaultCollectingPolicyType;
		PlacingPolicyType policyType = data != null ? data.StoringPlacingPolicy : DefaultPlacingPolicyType;
		SetStoringCollectingPolicy(collectingPolicyType);
		SetStoringPlacingPolicy(policyType);
	}

	public void OnTaskCompleted(WorkerTask task)
	{
		switch (task.Type)
		{
			case Unloading:
				break;
			case Storing:
				break;
		}
	}

	public void BuildTaskByPayload(Rocket rocket)
	{
		UnloadingTask task = new(rocket);
		TaskMgr.EnqueueTask(task);
	}

	private void Awake()
	{
		RebuildPlanner();
	}

	private void Start()
	{
		cargoPortService.OnItemPresentChanged += OnPortItemPresentChanged;
		cargoPortService.OnItemQuantityChanged += OnPortItemQuantityChanged;
		cargoPortService.OnReserveQuantityChanged += OnPortItemReserved;
	}

	private void OnDestroy()
	{
		cargoPortService.OnItemPresentChanged -= OnPortItemPresentChanged;
		cargoPortService.OnItemQuantityChanged -= OnPortItemQuantityChanged;
		cargoPortService.OnReserveQuantityChanged -= OnPortItemReserved;
	}

	private void Update()
	{
		CheckStoreTaskAvailable();
	}

	public void ResetRuntimeState()
	{
		requestService?.ResetRuntimeState();
		RebuildPlanner();
	}

	private void CheckStoreTaskAvailable()
	{
		if (storingPlanner == null || storingPlanner.HasPendingCollectWork() == false)
			return;

		int desiredTaskCount = GetDesiredStoringTaskCount();
		int currentTaskCount = GetCurrentStoringTaskCount();
		if (desiredTaskCount <= currentTaskCount)
			return;

		int tasksToBuild = Mathf.Min(maxStoreTasksPerUpdate, Mathf.Max(0, desiredTaskCount - currentTaskCount));
		for (int i = 0; i < tasksToBuild; ++i)
		{
			if (storingPlanner.BuildStoreTask(out var task) == false)
				break;

			if (task != null)
				TaskMgr.EnqueueTask(task);
		}
	}

	private void OnPortItemPresentChanged(ShelfBase port, uint itemId, bool present)
	{
		requestService.OnPortItemPresentChanged(port, itemId, present);
	}

	private void OnPortItemReserved(ShelfBase port, uint itemId, int quantity)
	{
		requestService.OnPortItemReservedChanged(port, itemId, quantity);
	}

	private void OnPortItemQuantityChanged(ShelfBase port, uint itemId, int quantityDelta)
	{
		requestService.OnPortItemQuantityChanged(port, itemId, quantityDelta);
	}

	private void RebuildPlanner()
	{
		storingPlanner = new StoringPlanner(
			cargoPortService,
			requestService,
			defaultStoringCollectingPolicyType,
			defaultStoringPlacingPolicyType);
	}

	private int GetCurrentStoringTaskCount()
	{
		return TaskMgr.TaskQueue[Storing].Count + TaskMgr.TaskOnProgress[Storing].Count;
	}

	private int GetDesiredStoringTaskCount()
	{
		float effectiveBoxCapacity = GetEffectiveStoringBoxCapacity();
		if (effectiveBoxCapacity <= 0.0f)
			return 0;

		float totalOutstandingSize = requestService != null ? requestService.GetOutstandingTotalSize(ItemDB) : 0.0f;
		if (totalOutstandingSize <= 0.0f)
			return 0;

		return Mathf.CeilToInt(totalOutstandingSize / effectiveBoxCapacity);
	}

	private float GetEffectiveStoringBoxCapacity()
	{
		float toteCapacity = BoxPoolManager != null ? BoxPoolManager.ToteCapacity : 0.0f;
		if (toteCapacity <= 0.0f)
			return 0.0f;

		float fillRatio = Mathf.Clamp01(storingBoxFillLimitPercent / 100.0f);
		return toteCapacity * fillRatio;
	}
}
