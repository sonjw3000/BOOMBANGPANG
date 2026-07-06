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
	[SerializeField] private CollectingPolicyType defaultPickingCollectingPolicyType = DefaultCollectingPolicyType;

	private float timeSinceLastOrder = 0.0f;
	private readonly HashSet<OutboundCargoPort> queuedCargoTransferPorts = new();
	private readonly Dictionary<OutboundCargoPort, InboundCargoPort> queuedCargoTransferTargets = new();
	private readonly Dictionary<uint, PickingManifest> pickingManifests = new();
	private readonly List<PickingDispatchCandidate> pickingDispatchCandidates = new();

	public PackingStationService PackingStationService => packingStationService;
	public LaunchStationService LaunchStationService => launchStationService;
	public IReadOnlyDictionary<uint, PickingManifest> PickingManifests => pickingManifests;
	public CollectingPolicyType PickingCollectingPolicyType => defaultPickingCollectingPolicyType;
	public float PickingBoxFillLimitPercent => pickingBoxFillLimitPercent;
	public float CargoPortThresholdPercent => cargoPortThresholdPercent;
	private OrderManager OrderMgr => GameContext.Instance.OrderMgr;
	private TaskManager TaskMgr => GameContext.Instance.TaskMgr;
	private CargoPortService CargoPortService => GameContext.Instance.CargoPortSvc;
	private GridService GridService => GameContext.Instance.GridService;
	private BuildingManager BuildingManager => GameContext.Instance.BuildingMgr;

	private readonly struct PickingDispatchCandidate
	{
		public readonly StorageBuilding Building;
		public readonly int PickableQuantity;

		public PickingDispatchCandidate(StorageBuilding building, int pickableQuantity)
		{
			Building = building;
			PickableQuantity = pickableQuantity;
		}
	}

	public void OnTaskCompleted(WorkerTask task)
	{
		switch (task.Type)
		{
			case TaskType.OB:
				break;
			case TaskType.CargoTransfer:
				if (task is CargoTransferTask cargoTransferTask)
					OnCargoTransferTaskCompleted(cargoTransferTask);
				else if (task is CapsuleRelocationTask capsuleTransferTask)
					OnCargoTransferTaskCompleted(capsuleTransferTask);
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
		if (BuildingManager == null)
			return;

		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			if (buildings[i] is StorageBuilding storageBuilding)
				storageBuilding.PickingPlanner?.SetCollectingPolicy(policyType);
		}
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

	public int TransferPickingManifest(BoxBase from, BoxBase to, uint itemId, int quantity)
	{
		return TransferPickingManifest(from, to, itemId, quantity, false);
	}

	public int TransferPickingManifest(BoxBase from, BoxBase to, ItemStack stack)
	{
		if (stack == null)
			return 0;

		return TransferPickingManifest(from, to, stack.ItemID, stack.Quantity, stack.HasStatus(ItemStatus.Packed));
	}

	public int TransferPickingManifest(BoxBase from, BoxBase to, uint itemId, int quantity, bool packed)
	{
		if (from == null || to == null || from.BoxId == to.BoxId || quantity <= 0)
			return 0;

		if (TryGetPickingManifest(from, out PickingManifest sourceManifest) == false)
			return 0;

		PickingManifest targetManifest = GetPickingManifest(to);
		if (targetManifest == null)
			return 0;

		int remaining = quantity;
		int movedTotal = 0;
		List<PickingManifestLine> snapshot = new(sourceManifest.Lines);
		for (int i = 0; i < snapshot.Count && remaining > 0; ++i)
		{
			PickingManifestLine line = snapshot[i];
			if (line == null || line.ItemId != itemId || line.OrderLine == null)
				continue;

			int available = packed ? line.PackedQuantity : line.PackableQuantity;
			if (available <= 0)
				continue;

			int requested = Mathf.Min(remaining, available);
			int moved = packed
				? sourceManifest.RemovePacked(line.OrderLine, itemId, requested)
				: sourceManifest.RemovePicked(line.OrderLine, itemId, requested);
			if (moved <= 0)
				continue;

			int added = packed
				? targetManifest.AddPacked(line.OrderLine, itemId, moved)
				: targetManifest.AddPicked(line.OrderLine, itemId, moved);
			if (added != moved)
				Debug.LogWarning($"[OutboundWorkflowService] Picking manifest transfer mismatch. item={itemId}, moved={moved}, added={added}");

			movedTotal += added;
			remaining -= added;
		}

		if (sourceManifest.IsEmpty)
			ClearPickingManifest(from);

		return movedTotal;
	}

	public bool HasPackableManifest(BoxBase box)
	{
		if (TryGetPickingManifest(box, out PickingManifest manifest) == false)
			return false;

		IReadOnlyList<PickingManifestLine> lines = manifest.Lines;
		for (int i = 0; i < lines.Count; ++i)
		{
			if (lines[i] != null && lines[i].PackableQuantity > 0)
				return true;
		}

		return false;
	}

	public bool TryBuildPackingJob(BoxBase box, IItemContainer container, IGridPlaceable target, out WorkJob job)
	{
		job = null;
		if (box == null || container == null || target == null || TryGetPickingManifest(box, out PickingManifest manifest) == false)
			return false;

		List<WorkLine> lines = new();
		IReadOnlyList<PickingManifestLine> manifestLines = manifest.Lines;
		for (int i = 0; i < manifestLines.Count; ++i)
		{
			PickingManifestLine line = manifestLines[i];
			if (line?.OrderLine == null || line.PackableQuantity <= 0)
				continue;

			lines.Add(new WorkLine(WorkLineAction.Pick, container, target, line.ItemId, line.PackableQuantity, line.OrderLine));
		}

		if (lines.Count <= 0)
			return false;

		job = new WorkJob(PickingPlanner.GetNextJobId(), lines, WorkOp.Packing);
		PickingPlanner.SetNextJobId(job.JobID + 1);
		return true;
	}

	public int ReportPackedFromManifest(BoxBase box, OrderLine orderLine, uint itemId, int quantity)
	{
		if (box == null || quantity <= 0 || TryGetPickingManifest(box, out PickingManifest manifest) == false)
			return 0;

		return manifest.ReportPacked(orderLine, itemId, quantity);
	}

	public bool TryGetPackableManifestLine(BoxBase box, OrderLine orderLine, uint itemId, out PickingManifestLine line)
	{
		line = null;
		if (box == null || TryGetPickingManifest(box, out PickingManifest manifest) == false)
			return false;

		line = manifest.FindLine(orderLine, itemId);
		return line != null && line.PackableQuantity > 0;
	}

	public int GetPackableManifestQuantity(BoxBase box, OrderLine orderLine, uint itemId)
	{
		return TryGetPackableManifestLine(box, orderLine, itemId, out PickingManifestLine line) ? line.PackableQuantity : 0;
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

		DispatchPickingRequests();
	}

	private void DispatchPickingRequests()
	{
		if (OrderMgr == null || BuildingManager == null)
			return;

		foreach (uint itemId in OrderMgr.GetRequestedItemIds())
		{
			foreach (OrderLine orderLine in OrderMgr.GetRequestLines(itemId))
				DispatchPickingRequest(orderLine);
		}
	}

	private void DispatchPickingRequest(OrderLine orderLine)
	{
		if (orderLine == null || orderLine.CanAllocatePicking == false)
			return;

		int remaining = orderLine.GetPickingAllocatableQuantity();
		if (remaining <= 0)
			return;

		BuildPickingDispatchCandidates(orderLine.ItemID);
		pickingDispatchCandidates.Sort((left, right) => right.PickableQuantity.CompareTo(left.PickableQuantity));

		for (int i = 0; i < pickingDispatchCandidates.Count && remaining > 0; ++i)
		{
			PickingDispatchCandidate candidate = pickingDispatchCandidates[i];
			if (candidate.Building == null || candidate.PickableQuantity <= 0)
				continue;

			int quantity = Mathf.Min(remaining, candidate.PickableQuantity);
			if (quantity <= 0)
				continue;

			int accepted = candidate.Building.AcceptPickingRequest(orderLine, quantity, out _);
			if (accepted <= 0)
				continue;

			remaining -= accepted;
		}
	}

	private void BuildPickingDispatchCandidates(uint itemId)
	{
		pickingDispatchCandidates.Clear();
		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			if (buildings[i] is not StorageBuilding storageBuilding || storageBuilding.RuntimeBuildingId == 0)
				continue;

			int pickable = storageBuilding.GetPickableQuantity(itemId);
			if (pickable <= 0)
				continue;

			pickingDispatchCandidates.Add(new PickingDispatchCandidate(storageBuilding, pickable));
		}
	}

	private void SubscribeCargoPortEvents()
	{
		CargoPortService cargoPortService = CargoPortService;
		if (cargoPortService == null)
			return;

		cargoPortService.OnCapsuleDocked -= HandleCapsuleDocked;
		cargoPortService.OnCapsuleUndocked -= HandleCapsuleUndocked;
		cargoPortService.OnCapsuleDocked += HandleCapsuleDocked;
		cargoPortService.OnCapsuleUndocked += HandleCapsuleUndocked;
	}

	private void UnsubscribeCargoPortEvents()
	{
		CargoPortService cargoPortService = CargoPortService;
		if (cargoPortService == null)
			return;

		cargoPortService.OnCapsuleDocked -= HandleCapsuleDocked;
		cargoPortService.OnCapsuleUndocked -= HandleCapsuleUndocked;
	}

	private void HandleCapsuleDocked(uint buildingId, CargoPort cargoPort)
	{
		if (cargoPort is not OutboundCargoPort outboundCargoPort)
			return;

		TryEnqueueCargoTransferTask(outboundCargoPort, buildingId);
	}

	private void HandleCapsuleUndocked(uint buildingId, CargoPort cargoPort)
	{
		switch (cargoPort)
		{
			case OutboundCargoPort outboundCargoPort:
				queuedCargoTransferPorts.Remove(outboundCargoPort);
				queuedCargoTransferTargets.Remove(outboundCargoPort);
				GameContext.Instance.CapsuleRelocateCoordinator.CancelPendingRequests(outboundCargoPort);
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

		GameContext.Instance.CapsuleRelocateCoordinator.RequestSend(new CapsuleRelocateSendRequest(
			sourcePort,
			CapsuleDockState.OB,
			CapsuleLogisticsState.OB,
			CapsuleDockState.IBStandby,
			CapsuleRelocateScope.LinkedBuilding,
			buildingId,
			onMatched: EnqueueCargoTransferTask));
	}

	private bool EnqueueCargoTransferTask(CapsuleRelocateMatch match)
	{
		if (TaskMgr == null ||
			match.SourceDock is not OutboundCargoPort sourcePort ||
			match.TargetDock is not InboundCargoPort targetPort)
		{
			return false;
		}

		CapsuleRelocationTask task = new(
			TaskType.CargoTransfer,
			sourcePort,
			targetPort,
			0,
			CapsuleRelocationReason.SourceMustClear);
		TaskMgr.EnqueueTask(task);
		OnCargoTransferTaskBuilt(sourcePort, targetPort);
		return true;
	}

	internal void OnCargoTransferTaskBuilt(CargoTransferTask task)
	{
		if (task?.SourcePort == null)
			return;

		OnCargoTransferTaskBuilt(task.SourcePort, task.TargetPort);
	}

	private void OnCargoTransferTaskBuilt(OutboundCargoPort sourcePort, InboundCargoPort targetPort)
	{
		if (sourcePort == null)
			return;

		queuedCargoTransferPorts.Add(sourcePort);
		if (targetPort != null)
			queuedCargoTransferTargets[sourcePort] = targetPort;
	}

	private void OnCargoTransferTaskCompleted(CapsuleRelocationTask task)
	{
		if (task?.SourceDock is not OutboundCargoPort sourcePort)
			return;

		queuedCargoTransferPorts.Remove(sourcePort);
		queuedCargoTransferTargets.Remove(sourcePort);

		if (task.TargetDock is InboundCargoPort targetPort)
			RemoveQueuedCargoTransferTarget(targetPort);
	}

	internal LaunchStation ResolveLoadingTargetStation(CargoPort sourcePort)
	{
		if (sourcePort == null || launchStationService == null)
			return null;

		int3 sourcePoint = ResolveInteractionOrigin(sourcePort, InteractionKind.Pick);
		FacilityFilter facilityFilter = FacilityFilter.ForContainer(sourcePort.DockedCapsule);
		if (ResolveSourceBuilding(sourcePort, out Building sourceBuilding) &&
			launchStationService.TryFindDestination(sourceBuilding.RuntimeBuildingId, sourcePoint, InteractionKind.Put, facilityFilter, out LaunchStation localStation))
		{
			return localStation;
		}

		return launchStationService.TryFindDestination(0, sourcePoint, InteractionKind.Put, facilityFilter, out LaunchStation globalStation)
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
