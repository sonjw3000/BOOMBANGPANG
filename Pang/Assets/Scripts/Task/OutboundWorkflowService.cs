using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static WorkerTask;

// outbound 작업 흐름 관리
// 주문을 까서 picking -> packaging -> loading 작업을 관리

public partial class OutboundWorkflowService : MonoBehaviour, IBoundService
{
	private const CollectingPolicyType DefaultCollectingPolicyType = CollectingPolicyType.Nearest;

	[SerializeField] private PackingStationService packingStationService;
	[SerializeField] private LaunchStationService launchStationService;
	[SerializeField] private float orderInterval = 10.0f;
	[SerializeField] private float cargoPortThresholdPercent = 80.0f;
	[SerializeField] [Range(1f, 100f)] private float pickingBoxFillLimitPercent = 80.0f;
	[SerializeField] private int maxPickingTasksPerUpdate = 64;
	[SerializeField] private CollectingPolicyType defaultPickingCollectingPolicyType = DefaultCollectingPolicyType;

	private float timeSinceLastOrder = 0.0f;
	private PickingPlanner pickingPlanner;
	private readonly HashSet<OutboundCargoPort> queuedCargoTransferPorts = new();
	private readonly HashSet<OutboundCargoPort> pendingCargoTransferPorts = new();
	private readonly Dictionary<OutboundCargoPort, InboundCargoPort> queuedCargoTransferTargets = new();

	public PackingStationService PackingStationService => packingStationService;
	public LaunchStationService LaunchStationService => launchStationService;
	public PickingPlanner PickingPlanner => pickingPlanner;
	public CollectingPolicyType PickingCollectingPolicyType => pickingPlanner != null ? pickingPlanner.CollectingPolicyType : defaultPickingCollectingPolicyType;
	private OrderManager OrderMgr => GameContext.Instance.OrderMgr;
	private TaskManager TaskMgr => GameContext.Instance.TaskMgr;
	private ItemDatabase ItemDB => GameContext.Instance.ItemDB;
	private BoxManager BoxMgr => GameContext.Instance.BoxMgr;
	private CargoPortService CargoPortService => GameContext.HasInstance ? GameContext.Instance.CargoPortSvc : null;
	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;

	public void OnTaskCompleted(WorkerTask task)
	{
		switch (task.Type)
		{
			case TaskType.OB:
				break;
			case TaskType.CargoTransfer:
				if (task is CargoTransferTask cargoTransferTask)
					OnCargoTransferTaskCompleted(cargoTransferTask);
				break;
			case TaskType.Picking:
				break;
			case TaskType.Packing:
				if (task is PackingTask packingTask)
					PackingStationService.OnPackingTaskCompleted(packingTask.TargetStation);
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

	public void BuildLoadingTask(CargoPort cargoPort)
	{
		if (cargoPort is not OutboundCargoPort || cargoPort.CanGetBox() == false)
			return;

		LaunchStation targetStation = ResolveLoadingTargetStation(cargoPort);
		if (targetStation == null)
			return;

		TaskMgr.EnqueueTask(new LoadingTask(cargoPort, targetStation));
	}

	private void Awake()
	{
		RebuildPlanner();
	}

	private void Start()
	{
		SubscribeCargoPortEvents();
	}

	private void OnEnable()
	{
		SubscribeCargoPortEvents();
	}

	private void OnDisable()
	{
		UnsubscribeCargoPortEvents();
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

	private void RebuildPlanner()
	{
		pickingPlanner = new PickingPlanner(
			GameContext.Instance.StorageService,
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
		float toteCapacity = BoxMgr != null ? BoxMgr.ToteCapacity : 0.0f;
		if (toteCapacity <= 0.0f)
			return 0.0f;

		return toteCapacity * Mathf.Clamp01(pickingBoxFillLimitPercent / 100.0f);
	}

	private void SubscribeCargoPortEvents()
	{
		CargoPortService cargoPortService = CargoPortService;
		if (cargoPortService == null)
			return;

		cargoPortService.OnCargoDocked -= HandleCargoDocked;
		cargoPortService.OnCargoUndocked -= HandleCargoUndocked;
		cargoPortService.OnCargoDocked += HandleCargoDocked;
		cargoPortService.OnCargoUndocked += HandleCargoUndocked;
	}

	private void UnsubscribeCargoPortEvents()
	{
		CargoPortService cargoPortService = CargoPortService;
		if (cargoPortService == null)
			return;

		cargoPortService.OnCargoDocked -= HandleCargoDocked;
		cargoPortService.OnCargoUndocked -= HandleCargoUndocked;
	}

	private void HandleCargoDocked(uint buildingId, CargoPort cargoPort)
	{
		if (cargoPort is not OutboundCargoPort outboundCargoPort)
			return;

		TryEnqueueCargoTransferTask(outboundCargoPort);
	}

	private void HandleCargoUndocked(uint buildingId, CargoPort cargoPort)
	{
		switch (cargoPort)
		{
			case OutboundCargoPort outboundCargoPort:
				queuedCargoTransferPorts.Remove(outboundCargoPort);
				pendingCargoTransferPorts.Remove(outboundCargoPort);
				queuedCargoTransferTargets.Remove(outboundCargoPort);
				break;

			case InboundCargoPort inboundCargoPort:
				RemoveQueuedCargoTransferTarget(inboundCargoPort);
				TryEnqueuePendingCargoTransferTasks();
				break;
		}
	}

	private void OnCargoTransferTaskCompleted(CargoTransferTask task)
	{
		if (task?.SourcePort == null)
			return;

		queuedCargoTransferPorts.Remove(task.SourcePort);
		pendingCargoTransferPorts.Remove(task.SourcePort);
		queuedCargoTransferTargets.Remove(task.SourcePort);

		if (task.TargetPort != null)
			RemoveQueuedCargoTransferTarget(task.TargetPort);
	}

	private void TryEnqueueCargoTransferTask(OutboundCargoPort sourcePort)
	{
		if (sourcePort == null || queuedCargoTransferPorts.Contains(sourcePort) || TaskMgr == null || sourcePort.CanGetBox() == false)
			return;

		InboundCargoPort targetPort = ResolveLinkedInboundTarget(sourcePort);
		if (targetPort == null)
		{
			pendingCargoTransferPorts.Add(sourcePort);
			return;
		}

		TaskMgr.EnqueueTask(new CargoTransferTask(sourcePort, targetPort));
		queuedCargoTransferPorts.Add(sourcePort);
		queuedCargoTransferTargets[sourcePort] = targetPort;
		pendingCargoTransferPorts.Remove(sourcePort);
	}

	private void TryEnqueuePendingCargoTransferTasks()
	{
		if (pendingCargoTransferPorts.Count <= 0)
			return;

		OutboundCargoPort[] pendingPorts = new OutboundCargoPort[pendingCargoTransferPorts.Count];
		pendingCargoTransferPorts.CopyTo(pendingPorts);
		for (int i = 0; i < pendingPorts.Length; ++i)
			TryEnqueueCargoTransferTask(pendingPorts[i]);
	}

	private InboundCargoPort ResolveLinkedInboundTarget(OutboundCargoPort sourcePort)
	{
		if (sourcePort == null || ResolveSourceBuilding(sourcePort, out Building sourceBuilding) == false)
			return null;

		int3 sourcePoint = ResolveInteractionOrigin(sourcePort, InteractionKind.Pick);
		ZoneFilter zoneFilter = ZoneFilter.ForContainer(sourcePort.DockedCapsule);
		return sourceBuilding.ResolveLinkedInboundPortTarget(sourcePoint, zoneFilter);
	}

	private LaunchStation ResolveLoadingTargetStation(CargoPort sourcePort)
	{
		if (sourcePort == null || launchStationService == null)
			return null;

		int3 sourcePoint = ResolveInteractionOrigin(sourcePort, InteractionKind.Pick);
		ZoneFilter zoneFilter = ZoneFilter.ForContainer(sourcePort.DockedCapsule);
		if (ResolveSourceBuilding(sourcePort, out Building sourceBuilding) &&
			launchStationService.TryFindDestination(sourceBuilding.RuntimeBuildingId, sourcePoint, InteractionKind.Put, zoneFilter, out LaunchStation localStation))
		{
			return localStation;
		}

		return launchStationService.TryFindDestination(0, sourcePoint, InteractionKind.Put, zoneFilter, out LaunchStation globalStation)
			? globalStation
			: null;
	}

	private static int3 ResolveInteractionOrigin(BoxInteraction interactionTarget, InteractionKind interactionKind)
	{
		if (interactionTarget == null)
			return default;

		if (interactionTarget.InteractionPointMap != null &&
			interactionTarget.InteractionPointMap.ContainsKey(interactionKind) &&
			interactionTarget.InteractionPointMap[interactionKind] != null &&
			interactionTarget.InteractionPointMap[interactionKind].Count > 0)
		{
			return interactionTarget.GetClosestInteractionPoint(interactionKind, interactionTarget.GridPosition);
		}

		return interactionTarget.GridPosition;
	}

	private void RemoveQueuedCargoTransferTarget(InboundCargoPort cargoPort)
	{
		if (cargoPort == null || queuedCargoTransferTargets.Count <= 0)
			return;

		OutboundCargoPort[] sourcePorts = new OutboundCargoPort[queuedCargoTransferTargets.Count];
		queuedCargoTransferTargets.Keys.CopyTo(sourcePorts, 0);
		for (int i = 0; i < sourcePorts.Length; ++i)
		{
			OutboundCargoPort sourcePort = sourcePorts[i];
			if (sourcePort == null || queuedCargoTransferTargets.TryGetValue(sourcePort, out InboundCargoPort targetPort) == false || targetPort != cargoPort)
				continue;

			queuedCargoTransferTargets.Remove(sourcePort);
		}
	}

	private bool ResolveSourceBuilding(CargoPort sourcePort, out Building building)
	{
		building = null;
		if (sourcePort == null || GridService == null || BuildingManager == null)
			return false;

		GridCell cell = GridService.GetCell(sourcePort.GridPosition);
		return cell != null &&
			cell.BuildingId != 0 &&
			BuildingManager.TryGetBuilding(cell.BuildingId, out building) &&
			building != null;
	}
}
