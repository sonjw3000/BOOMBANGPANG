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
	private readonly Dictionary<OutboundCargoPort, InboundCargoPort> queuedCargoTransferTargets = new();
	private readonly Dictionary<uint, PickingManifest> pickingManifests = new();

	public PackingStationService PackingStationService => packingStationService;
	public LaunchStationService LaunchStationService => launchStationService;
	public PickingPlanner PickingPlanner => pickingPlanner;
	public IReadOnlyDictionary<uint, PickingManifest> PickingManifests => pickingManifests;
	public CollectingPolicyType PickingCollectingPolicyType => pickingPlanner != null ? pickingPlanner.CollectingPolicyType : defaultPickingCollectingPolicyType;
	public float CargoPortThresholdPercent => cargoPortThresholdPercent;
	private OrderManager OrderMgr => GameContext.Instance.OrderMgr;
	private TaskManager TaskMgr => GameContext.Instance.TaskMgr;
	private ItemDatabase ItemDB => GameContext.Instance.ItemDB;
	private BoxManager BoxMgr => GameContext.Instance.BoxMgr;
	private CargoPortService CargoPortService => GameContext.HasInstance ? GameContext.Instance.CargoPortSvc : null;
	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private CapsuleBufferService CapsuleBufferService => GameContext.HasInstance ? GameContext.Instance.CapsuleBufferSvc : null;

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

	public PickingManifest GetPickingManifest(BoxBase box)
	{
		return box != null ? GetPickingManifest(box.BoxId) : null;
	}

	public PickingManifest GetPickingManifest(uint boxId)
	{
		if (boxId == 0)
			return null;

		if (pickingManifests.TryGetValue(boxId, out PickingManifest manifest) == false)
		{
			manifest = new PickingManifest();
			pickingManifests.Add(boxId, manifest);
		}

		return manifest;
	}

	public bool TryGetPickingManifest(BoxBase box, out PickingManifest manifest)
	{
		manifest = null;
		return box != null && TryGetPickingManifest(box.BoxId, out manifest);
	}

	public bool TryGetPickingManifest(uint boxId, out PickingManifest manifest)
	{
		manifest = null;
		return boxId != 0 && pickingManifests.TryGetValue(boxId, out manifest);
	}

	public void ClearPickingManifest(BoxBase box)
	{
		if (box != null)
			ClearPickingManifest(box.BoxId);
	}

	public void ClearPickingManifest(uint boxId)
	{
		if (boxId != 0)
			pickingManifests.Remove(boxId);
	}

	public int AddPickedToManifest(BoxBase box, OrderLine orderLine, uint itemId, int quantity)
	{
		if (box == null || quantity <= 0)
			return 0;

		PickingManifest manifest = GetPickingManifest(box);
		return manifest != null ? manifest.AddPicked(orderLine, itemId, quantity) : 0;
	}

	public int TransferPickingManifest(BoxBase from, BoxBase to, OrderLine orderLine, uint itemId, int quantity)
	{
		if (from == null || to == null || from.BoxId == to.BoxId || quantity <= 0)
			return 0;

		if (TryGetPickingManifest(from, out PickingManifest sourceManifest) == false)
			return 0;

		PickingManifest targetManifest = GetPickingManifest(to);
		if (targetManifest == null)
			return 0;

		int moved = sourceManifest.RemovePicked(orderLine, itemId, quantity);
		if (moved <= 0)
			return 0;

		int added = targetManifest.AddPicked(orderLine, itemId, moved);
		if (added != moved)
			Debug.LogWarning($"[OutboundWorkflowService] Picking manifest transfer mismatch. item={itemId}, moved={moved}, added={added}");

		if (sourceManifest.IsEmpty)
			ClearPickingManifest(from);

		return added;
	}

	public int ReportPackedFromManifest(BoxBase box, OrderLine orderLine, uint itemId, int quantity)
	{
		if (box == null || quantity <= 0 || TryGetPickingManifest(box, out PickingManifest manifest) == false)
			return 0;

		return manifest.ReportPacked(orderLine, itemId, quantity);
	}

	public int GetPackableManifestQuantity(BoxBase box, OrderLine orderLine, uint itemId)
	{
		if (box == null || TryGetPickingManifest(box, out PickingManifest manifest) == false)
			return 0;

		PickingManifestLine line = manifest.FindLine(orderLine, itemId);
		return line != null ? line.PackableQuantity : 0;
	}

	public int ReportOutboundProgressFromManifest(BoxBase box, PackageOutboundStage targetStage)
	{
		if (box == null || TryGetPickingManifest(box, out PickingManifest manifest) == false)
			return 0;

		return manifest.ReportOutboundProgress(OrderMgr, targetStage);
	}

	public void BuildLoadingTask(CargoPort cargoPort)
	{
		if (cargoPort is not OutboundCargoPort || cargoPort.CanGetBox() == false)
			return;

		uint requestedBuildingId = ResolveSourceBuilding(cargoPort, out Building sourceBuilding)
			? sourceBuilding.RuntimeBuildingId
			: 0;
		TaskMgr.EnqueueTaskBuildRequest(new LoadingTaskBuildRequest(cargoPort, requestedBuildingId));
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

		for (int i = 0; i < maxPickingTasksPerUpdate; ++i)
		{
			if (TrySelectPickingBuilding(out uint buildingId) == false)
				break;

			if (pickingPlanner.BuildPickingTask(buildingId, out var task) == false)
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
			CapsuleBufferService,
			pickingBoxFillLimitPercent,
			defaultPickingCollectingPolicyType);
	}

	private int GetCurrentPickingTaskCount()
	{
		return TaskMgr.TaskQueue[TaskType.Picking].Count + TaskMgr.TaskOnProgress[TaskType.Picking].Count;
	}

	private int GetCurrentPickingTaskCount(uint buildingId)
	{
		if (TaskMgr == null)
			return 0;

		return CountPickingTasks(TaskMgr.TaskQueue[TaskType.Picking], buildingId) +
			CountPickingTasks(TaskMgr.TaskOnProgress[TaskType.Picking], buildingId);
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

	private int GetDesiredPickingTaskCount(uint buildingId, float pickableOutstandingSize)
	{
		float effectiveBoxCapacity = GetEffectivePickingBoxCapacity();
		if (effectiveBoxCapacity <= 0.0f || pickableOutstandingSize <= 0.0f)
			return 0;

		return Mathf.CeilToInt(pickableOutstandingSize / effectiveBoxCapacity);
	}

	private bool TrySelectPickingBuilding(out uint buildingId)
	{
		buildingId = 0;
		if (BuildingManager == null || pickingPlanner == null || ItemDB == null)
			return false;

		float bestPickableSize = 0.0f;
		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			Building building = buildings[i];
			if (building == null || building.Type != BuildingType.Storage || building.RuntimeBuildingId == 0)
				continue;

			uint candidateBuildingId = building.RuntimeBuildingId;
			float pickableSize = pickingPlanner.GetPickableOutstandingTotalSize(candidateBuildingId, ItemDB);
			if (pickableSize <= 0.0f)
				continue;

			int desired = GetDesiredPickingTaskCount(candidateBuildingId, pickableSize);
			int current = GetCurrentPickingTaskCount(candidateBuildingId);
			if (desired <= current)
				continue;

			if (pickableSize <= bestPickableSize)
				continue;

			bestPickableSize = pickableSize;
			buildingId = candidateBuildingId;
		}

		return buildingId != 0;
	}

	private float GetEffectivePickingBoxCapacity()
	{
		float toteCapacity = BoxMgr != null ? BoxMgr.ToteCapacity : 0.0f;
		if (toteCapacity <= 0.0f)
			return 0.0f;

		return toteCapacity * Mathf.Clamp01(pickingBoxFillLimitPercent / 100.0f);
	}

	private static int CountPickingTasks(IEnumerable<WorkerTask> tasks, uint buildingId)
	{
		if (tasks == null)
			return 0;

		int count = 0;
		foreach (WorkerTask task in tasks)
		{
			if (task is PickingTask pickingTask && pickingTask.BuildingId == buildingId)
				count += 1;
		}

		return count;
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

		TryEnqueueCargoTransferTask(outboundCargoPort, buildingId);
	}

	private void HandleCargoUndocked(uint buildingId, CargoPort cargoPort)
	{
		switch (cargoPort)
		{
			case OutboundCargoPort outboundCargoPort:
				queuedCargoTransferPorts.Remove(outboundCargoPort);
				queuedCargoTransferTargets.Remove(outboundCargoPort);
				TaskMgr?.CancelTaskBuildRequest(CargoTransferBuildRequest.GetRequestKey(outboundCargoPort));
				break;

			case InboundCargoPort inboundCargoPort:
				RemoveQueuedCargoTransferTarget(inboundCargoPort);
				break;
		}
	}

	private void OnCargoTransferTaskCompleted(CargoTransferTask task)
	{
		if (task?.SourcePort == null)
			return;

		queuedCargoTransferPorts.Remove(task.SourcePort);
		queuedCargoTransferTargets.Remove(task.SourcePort);

		if (task.TargetPort != null)
			RemoveQueuedCargoTransferTarget(task.TargetPort);
	}

	private void TryEnqueueCargoTransferTask(OutboundCargoPort sourcePort, uint buildingId)
	{
		if (sourcePort == null || queuedCargoTransferPorts.Contains(sourcePort) || TaskMgr == null || sourcePort.CanGetBox() == false)
			return;

		TaskMgr.EnqueueTaskBuildRequest(new CargoTransferBuildRequest(sourcePort, buildingId));
	}

	internal void OnCargoTransferTaskBuilt(CargoTransferTask task)
	{
		if (task?.SourcePort == null)
			return;

		queuedCargoTransferPorts.Add(task.SourcePort);
		if (task.TargetPort != null)
			queuedCargoTransferTargets[task.SourcePort] = task.TargetPort;
	}

	internal LaunchStation ResolveLoadingTargetStation(CargoPort sourcePort)
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
