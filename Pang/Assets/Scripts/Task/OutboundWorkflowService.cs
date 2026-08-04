using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static WorkerTask;

// outbound 작업 흐름 관리
// 주문을 까서 picking -> packaging -> loading 작업을 관리

public partial class OutboundWorkflowService : MonoBehaviour, IBoundService
{
	private const PickingPolicyType DefaultPickingPolicyType = PickingPolicyType.ManualShelfScan;
	private const CollectingPolicyType DefaultCollectingPolicyType = CollectingPolicyType.Nearest;

	[SerializeField] private PackingStationService packingStationService;
	[SerializeField] private LaunchStationService launchStationService;
	[SerializeField] private float orderInterval = 10.0f;
	[SerializeField] private float cargoPortThresholdPercent = 80.0f;
	[SerializeField] [Range(1f, 100f)] private float pickingBoxFillLimitPercent = 80.0f;
	[SerializeField] private PickingPolicyType defaultPickingPolicyType = DefaultPickingPolicyType;
	[SerializeField] private CollectingPolicyType defaultPickingCollectingPolicyType = DefaultCollectingPolicyType;
	[SerializeField] private uint loadingDestinationBuildingId = 0;
	[SerializeField] private bool outboundQualityControlEnabled;
	[SerializeField] [Range(0f, 100f)] private float minimumOutboundFreshnessPercent = QualityControlPolicy.DefaultMinimumFreshnessPercent;
	[SerializeField] [Range(0f, 100f)] private float maximumOutboundDamagePercent = QualityControlPolicy.DefaultMaximumDamagePercent;

	private float timeSinceLastOrder = 0.0f;
	private readonly HashSet<OutboundCargoPort> queuedCargoTransferPorts = new();
	private readonly Dictionary<OutboundCargoPort, InboundCargoPort> queuedCargoTransferTargets = new();
	private readonly Dictionary<PickingManifestKey, PickingManifest> pickingManifests = new();
	private readonly List<PickingDispatchCandidate> pickingDispatchCandidates = new();

	public PackingStationService PackingStationService => packingStationService;
	public LaunchStationService LaunchStationService => launchStationService;
	public IReadOnlyDictionary<PickingManifestKey, PickingManifest> PickingManifests => pickingManifests;
	public PickingPolicyType PickingPolicyType => defaultPickingPolicyType;
	public CollectingPolicyType PickingCollectingPolicyType => defaultPickingCollectingPolicyType;
	public float PickingBoxFillLimitPercent => pickingBoxFillLimitPercent;
	public float CargoPortThresholdPercent => cargoPortThresholdPercent;
	public uint LoadingDestinationBuildingId => loadingDestinationBuildingId;
	public bool OutboundQualityControlEnabled => outboundQualityControlEnabled && IsResearchCompleted(ResearchIds.QualityControl);
	public float MinimumOutboundFreshnessPercent => minimumOutboundFreshnessPercent;
	public float MaximumOutboundDamagePercent => maximumOutboundDamagePercent;
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

	public QualityInspectionResult InspectOutboundQuality(ItemStack stack)
	{
		return QualityControlPolicy.Inspect(
			stack,
			minimumOutboundFreshnessPercent,
			maximumOutboundDamagePercent);
	}

	public bool TrySetOutboundQualityControlEnabled(bool enabled)
	{
		if (IsResearchCompleted(ResearchIds.QualityControl) == false)
			return false;

		outboundQualityControlEnabled = enabled;
		EvaluateLaunchSortWork();
		return true;
	}

	public bool TrySetOutboundQualityThresholds(float minimumFreshnessPercent, float maximumDamagePercent)
	{
		if (IsResearchCompleted(ResearchIds.QualityControl) == false)
			return false;

		minimumOutboundFreshnessPercent = Mathf.Clamp(minimumFreshnessPercent, 0.0f, 100.0f);
		maximumOutboundDamagePercent = Mathf.Clamp(maximumDamagePercent, 0.0f, 100.0f);
		EvaluateLaunchSortWork();
		return true;
	}

	private void EvaluateLaunchSortWork()
	{
		if (BuildingManager == null)
			return;

		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			if (buildings[i] is LaunchBuilding launchBuilding)
				launchBuilding.EvaluateLaunchSortWork();
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

	public void OnTaskInvalidated(WorkerTask task)
	{
		if (task == null)
			return;

		switch (task.Type)
		{
			case TaskType.CargoTransfer:
				if (task is CargoTransferTask cargoTransferTask)
				{
					OnCargoTransferTaskCompleted(cargoTransferTask);
					ReleaseRelocationReservation(cargoTransferTask.SourcePort, cargoTransferTask.TargetPort);
				}
				else if (task is CapsuleRelocationTask capsuleTransferTask)
				{
					OnCargoTransferTaskCompleted(capsuleTransferTask);
					ReleaseRelocationReservation(capsuleTransferTask.SourceDock, capsuleTransferTask.TargetDock);
				}
				break;

			case TaskType.Packing:
				if (task is PackingTask packingTask)
				{
					PackingStation station = packingTask.TargetStation;
					FacilityManager facilityManager = GameContext.HasInstance ? GameContext.Instance.FacilityMgr : null;
					if (station != null && (facilityManager == null || facilityManager.IsInvalidating(station) == false))
						PackingStationService?.OnPackingTaskCompleted(station);
				}
				break;
		}
	}

	public void OnFacilityInvalidating(IFacility facility, in FacilityInvalidationContext context)
	{
		if (facility == null)
			return;

		if (facility is ShelfBase shelf && BuildingManager != null)
		{
			IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
			for (int i = 0; i < buildings.Count; ++i)
			{
				if (buildings[i] is StorageBuilding storageBuilding)
					storageBuilding.PickingPlanner?.CancelRequestsForSource(shelf);
			}
		}

		switch (facility)
		{
			case OutboundCargoPort outboundPort:
				queuedCargoTransferPorts.Remove(outboundPort);
				queuedCargoTransferTargets.Remove(outboundPort);
				break;

			case InboundCargoPort inboundPort:
				RemoveQueuedCargoTransferTarget(inboundPort);
				break;
		}
	}

	private static void ReleaseRelocationReservation(CapsuleDock sourceDock, CapsuleDock targetDock)
	{
		if (GameContext.HasInstance == false)
			return;

		GameContext.Instance.CapsuleRelocateCoordinator?.ReleaseReservation(sourceDock, targetDock);
	}

	public void MakeOrder()
	{
		OrderMgr.CreateRandomOrder();
	}

	public bool CanUsePickingPolicy(PickingPolicyType policyType)
	{
		if (IsResearchCompleted(ResearchIds.WorkflowPolicyManagement) == false)
			return false;

		return policyType == PickingPolicyType.ManualShelfScan ||
			(policyType == PickingPolicyType.InventoryGuided &&
			 IsResearchCompleted(ResearchIds.InventoryDigitization));
	}

	public bool TrySetPickingPolicy(PickingPolicyType policyType)
	{
		if (CanUsePickingPolicy(policyType) == false)
			return false;

		SetPickingPolicy(policyType);
		return true;
	}

	private void SetPickingPolicy(PickingPolicyType policyType)
	{
		defaultPickingPolicyType = policyType;
		if (BuildingManager == null)
			return;

		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			if (buildings[i] is StorageBuilding storageBuilding)
				storageBuilding.PickingPlanner?.SetPickingPolicy(policyType);
		}
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

	public bool CanUsePickingCollectingPolicy(CollectingPolicyType policyType)
	{
		if (IsResearchCompleted(ResearchIds.WorkflowPolicyManagement) == false)
			return false;

		return policyType == CollectingPolicyType.Nearest ||
			(policyType == CollectingPolicyType.LargestQuantityNearest &&
			 IsResearchCompleted(ResearchIds.WorkflowPolicyOptimization));
	}

	public bool TrySetPickingCollectingPolicy(CollectingPolicyType policyType)
	{
		if (CanUsePickingCollectingPolicy(policyType) == false)
			return false;

		SetPickingCollectingPolicy(policyType);
		return true;
	}

	public bool TrySetPickingBoxFillLimitPercent(float value)
	{
		if (IsResearchCompleted(ResearchIds.WorkflowPolicyOptimization) == false)
			return false;

		SetPickingBoxFillLimitPercent(value);
		return true;
	}

	private void SetPickingBoxFillLimitPercent(float value)
	{
		pickingBoxFillLimitPercent = Mathf.Clamp(value, 1.0f, 100.0f);
		if (BuildingManager == null)
			return;

		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			if (buildings[i] is StorageBuilding storageBuilding)
				storageBuilding.PickingPlanner?.SetBoxFillLimitPercent(pickingBoxFillLimitPercent);
		}
	}

	private static bool IsResearchCompleted(string researchId)
	{
		return GameContext.HasInstance &&
			GameContext.Instance.ResearchService?.IsResearched(researchId) == true;
	}

	public bool TryGetLoadingDestinationBuilding(out Building building)
	{
		building = null;
		return loadingDestinationBuildingId != 0
			&& GameContext.HasInstance
			&& GameContext.Instance.BuildingMgr != null
			&& GameContext.Instance.BuildingMgr.TryGetBuilding(loadingDestinationBuildingId, out building)
			&& building != null;
	}

	public void SetLoadingDestinationBuilding(Building building)
	{
		SetLoadingDestinationBuilding(building != null ? building.RuntimeBuildingId : 0);
	}

	public void SetLoadingDestinationBuilding(uint buildingId)
	{
		loadingDestinationBuildingId = buildingId;
	}

	public void ClearLoadingDestinationBuilding()
	{
		loadingDestinationBuildingId = 0;
	}

	public PickingManifest GetPickingManifest(BoxBase box)
	{
		return box != null ? GetPickingManifest(PickingManifestKey.From(box)) : null;
	}

	private PickingManifest GetPickingManifest(PickingManifestKey key)
	{
		if (key.IsValid == false)
			return null;

		if (pickingManifests.TryGetValue(key, out PickingManifest manifest) == false)
		{
			manifest = new PickingManifest();
			pickingManifests.Add(key, manifest);
		}

		return manifest;
	}

	public bool TryGetPickingManifest(BoxBase box, out PickingManifest manifest)
	{
		manifest = null;
		return box != null && TryGetPickingManifest(PickingManifestKey.From(box), out manifest);
	}

	private bool TryGetPickingManifest(PickingManifestKey key, out PickingManifest manifest)
	{
		manifest = null;
		return key.IsValid && pickingManifests.TryGetValue(key, out manifest);
	}

	public void ClearPickingManifest(BoxBase box)
	{
		if (box != null)
			ClearPickingManifest(PickingManifestKey.From(box));
	}

	private void ClearPickingManifest(PickingManifestKey key)
	{
		if (key.IsValid)
			pickingManifests.Remove(key);
	}

	public void OnBoxReleased(BoxBase box, bool destroyed)
	{
		if (box == null || TryGetPickingManifest(box, out PickingManifest manifest) == false)
			return;

		if (destroyed)
		{
			IReadOnlyList<PickingManifestLine> lines = manifest.Lines;
			for (int i = 0; i < lines.Count; ++i)
			{
				PickingManifestLine line = lines[i];
				if (line?.OrderLine == null || line.PickedQuantity <= 0)
					continue;

				if (line.OutboundStage == PackageOutboundStage.Completed)
				{
					Debug.LogWarning($"[OutboundWorkflowService] Completed cargo cannot be rolled back by box destruction. box={box.BoxId}, item={line.ItemId}");
					continue;
				}

				int rolledBack = OrderMgr.RollbackDestroyedCargo(
					line.OrderLine,
					line.PickedQuantity,
					line.PackedQuantity,
					line.OutboundStage);
				if (rolledBack != line.PickedQuantity)
					Debug.LogWarning($"[OutboundWorkflowService] Destroyed cargo rollback mismatch. box={box.BoxId}, item={line.ItemId}, requested={line.PickedQuantity}, applied={rolledBack}");
			}
		}

		ClearPickingManifest(box);
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
		return TransferPickingManifest(from, to, orderLine, itemId, quantity, false);
	}

	public int TransferPickingManifest(
		BoxBase from,
		BoxBase to,
		OrderLine orderLine,
		uint itemId,
		int quantity,
		bool packed)
	{
		if (from == null || to == null || PickingManifestKey.From(from) == PickingManifestKey.From(to) || quantity <= 0)
			return 0;

		if (TryGetPickingManifest(from, out PickingManifest sourceManifest) == false)
			return 0;

		PickingManifest targetManifest = GetPickingManifest(to);
		if (targetManifest == null)
			return 0;

		int moved = packed
			? sourceManifest.RemovePacked(orderLine, itemId, quantity)
			: sourceManifest.RemovePicked(orderLine, itemId, quantity);
		if (moved <= 0)
			return 0;

		int added = packed
			? targetManifest.AddPacked(orderLine, itemId, moved)
			: targetManifest.AddPicked(orderLine, itemId, moved);
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
		if (from == null || to == null || PickingManifestKey.From(from) == PickingManifestKey.From(to) || quantity <= 0)
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

	public int RejectPackedCargo(
		CapsuleBuffer sourceBuffer,
		OrderLine orderLine,
		uint itemId,
		int quantity)
	{
		return sourceBuffer?.DockedCapsule != null
			? RejectPackedCargo(sourceBuffer, sourceBuffer.DockedCapsule, orderLine, itemId, quantity)
			: 0;
	}

	public int RejectPackedCargo(
		BoxBase sourceBox,
		OrderLine orderLine,
		uint itemId,
		int quantity)
	{
		return sourceBox != null
			? RejectPackedCargo(sourceBox, sourceBox, orderLine, itemId, quantity)
			: 0;
	}

	private int RejectPackedCargo(
		IItemContainer sourceContainer,
		BoxBase manifestOwner,
		OrderLine orderLine,
		uint itemId,
		int quantity)
	{
		if (sourceContainer == null ||
			manifestOwner == null ||
			orderLine == null ||
			itemId == 0 ||
			quantity <= 0 ||
			OrderMgr == null ||
			TryGetPickingManifest(manifestOwner, out PickingManifest manifest) == false)
		{
			return 0;
		}

		PickingManifestLine manifestLine = manifest.FindLine(orderLine, itemId);
		if (manifestLine == null || manifestLine.PackedQuantity <= 0)
			return 0;

		int requested = Mathf.Min(quantity, manifestLine.PackedQuantity);
		if (CanRollbackRejectedCargo(orderLine, requested, manifestLine.OutboundStage) == false)
		{
			Debug.LogWarning(
				$"[OutboundQualityControl] Reject rollback precondition failed. item={itemId}, quantity={requested}, stage={manifestLine.OutboundStage}");
			return 0;
		}

		int rejectedQuantity = MarkRejectedPackedStacksAsWaste(
			sourceContainer,
			itemId,
			requested);
		if (rejectedQuantity <= 0)
			return 0;

		PackageOutboundStage outboundStage = manifestLine.OutboundStage;
		int removed = manifest.RemovePacked(orderLine, itemId, rejectedQuantity);
		if (removed <= 0)
			return 0;

		int rolledBack = OrderMgr.RollbackDestroyedCargo(orderLine, removed, removed, outboundStage);
		if (rolledBack != removed)
		{
			Debug.LogWarning(
				$"[OutboundQualityControl] Reject rollback mismatch. item={itemId}, rejected={removed}, rolledBack={rolledBack}");
		}

		if (manifest.IsEmpty)
			ClearPickingManifest(manifestOwner);

		BuildingManager?.RefreshItemContainerState(sourceContainer);
		return removed;
	}

	public int RejectInvalidPackedCargo(CapsuleBuffer sourceBuffer)
	{
		if (sourceBuffer?.DockedCapsule == null ||
			TryGetPickingManifest(sourceBuffer.DockedCapsule, out PickingManifest manifest) == false)
		{
			return 0;
		}

		List<PickingManifestLine> lines = new(manifest.Lines);
		int rejected = 0;
		for (int i = 0; i < lines.Count; ++i)
		{
			PickingManifestLine line = lines[i];
			if (line?.OrderLine == null || line.PackedQuantity <= 0)
				continue;

			int rejectedQuantity = GetRejectedPackedQuantity(
				sourceBuffer,
				line.ItemId,
				line.PackedQuantity,
				excludeReserved: false);
			if (rejectedQuantity > 0)
				rejected += RejectPackedCargo(sourceBuffer, line.OrderLine, line.ItemId, rejectedQuantity);
		}

		return rejected;
	}

	internal int GetRejectedPackedQuantity(
		CapsuleBuffer sourceBuffer,
		uint itemId,
		int limit,
		bool excludeReserved)
	{
		if (sourceBuffer == null || itemId == 0 || limit <= 0)
			return 0;

		int reserved = excludeReserved
			? sourceBuffer.ItemToBePicked.GetValueOrDefault(itemId)
			: 0;
		return GetRejectedPackedQuantity(sourceBuffer, itemId, limit, reserved);
	}

	internal int GetRejectedPackedQuantity(
		IItemContainer sourceContainer,
		uint itemId,
		int limit)
	{
		return GetRejectedPackedQuantity(sourceContainer, itemId, limit, 0);
	}

	private int GetRejectedPackedQuantity(
		IItemContainer sourceContainer,
		uint itemId,
		int limit,
		int reserved)
	{
		if (sourceContainer == null || itemId == 0 || limit <= 0)
			return 0;

		int physicalLimit = limit + Mathf.Max(0, reserved);
		int rejected = 0;
		for (int i = sourceContainer.Stacks.Count - 1; i >= 0 && rejected < physicalLimit; --i)
		{
			ItemStack stack = sourceContainer.Stacks[i];
			if (stack == null ||
				stack.Quantity <= 0 ||
				stack.ItemID != itemId ||
				ShouldRejectOutboundStack(stack) == false)
			{
				continue;
			}

			rejected += Mathf.Min(stack.Quantity, physicalLimit - rejected);
		}

		return Mathf.Clamp(rejected - Mathf.Max(0, reserved), 0, limit);
	}

	internal bool HasDispatchBlockingCargo(CapsuleBuffer sourceBuffer)
	{
		return HasDispatchBlockingCargo((IItemContainer)sourceBuffer);
	}

	internal bool HasDispatchBlockingCargo(IItemContainer sourceContainer)
	{
		if (sourceContainer == null)
			return true;

		for (int i = 0; i < sourceContainer.Stacks.Count; ++i)
		{
			ItemStack stack = sourceContainer.Stacks[i];
			if (stack == null || stack.Quantity <= 0)
				continue;

			if (stack.Status != ItemStatus.Packed ||
				stack.HasQuality(ItemQuality.Waste) ||
				ShouldRejectOutboundStack(stack))
			{
				return true;
			}
		}

		return false;
	}

	internal bool HasCompleteDispatchManifest(CapsuleBuffer sourceBuffer)
	{
		return sourceBuffer?.DockedCapsule != null &&
			HasCompleteDispatchManifest(sourceBuffer, sourceBuffer.DockedCapsule);
	}

	internal bool HasCompleteDispatchManifest(BoxBase sourceBox)
	{
		return sourceBox != null && HasCompleteDispatchManifest(sourceBox, sourceBox);
	}

	private bool HasCompleteDispatchManifest(IItemContainer sourceContainer, BoxBase manifestOwner)
	{
		if (sourceContainer == null ||
			manifestOwner == null ||
			TryGetPickingManifest(manifestOwner, out PickingManifest manifest) == false)
		{
			return false;
		}

		Dictionary<uint, int> physicalPackedQuantityByItemId = new();
		for (int i = 0; i < sourceContainer.Stacks.Count; ++i)
		{
			ItemStack stack = sourceContainer.Stacks[i];
			if (stack == null || stack.Quantity <= 0)
				continue;

			if (stack.ItemID == 0 || stack.Status != ItemStatus.Packed)
				return false;

			physicalPackedQuantityByItemId[stack.ItemID] =
				physicalPackedQuantityByItemId.GetValueOrDefault(stack.ItemID) + stack.Quantity;
		}

		if (physicalPackedQuantityByItemId.Count <= 0)
			return false;

		Dictionary<uint, int> manifestPackedQuantityByItemId = new();
		for (int i = 0; i < manifest.Lines.Count; ++i)
		{
			PickingManifestLine line = manifest.Lines[i];
			if (line?.OrderLine == null ||
				line.ItemId == 0 ||
				line.OrderLine.ItemID != line.ItemId ||
				line.PackedQuantity <= 0 ||
				line.PickedQuantity != line.PackedQuantity)
			{
				return false;
			}

			manifestPackedQuantityByItemId[line.ItemId] =
				manifestPackedQuantityByItemId.GetValueOrDefault(line.ItemId) + line.PackedQuantity;
		}

		if (manifestPackedQuantityByItemId.Count != physicalPackedQuantityByItemId.Count)
			return false;

		foreach (var entry in physicalPackedQuantityByItemId)
		{
			if (manifestPackedQuantityByItemId.TryGetValue(entry.Key, out int manifestQuantity) == false ||
				manifestQuantity != entry.Value)
			{
				return false;
			}
		}

		return true;
	}

	internal bool ShouldRejectOutboundStack(ItemStack stack)
	{
		return stack != null &&
			stack.Quantity > 0 &&
			stack.Status == ItemStatus.Packed &&
			(stack.HasQuality(ItemQuality.Waste) ||
			 (OutboundQualityControlEnabled && InspectOutboundQuality(stack).Accepted == false));
	}

	private static bool CanRollbackRejectedCargo(
		OrderLine orderLine,
		int quantity,
		PackageOutboundStage outboundStage)
	{
		if (orderLine == null ||
			quantity <= 0 ||
			outboundStage == PackageOutboundStage.Completed ||
			orderLine.PickingCompletedQuantity < quantity ||
			orderLine.PackagingCompletedQuantity < quantity)
		{
			return false;
		}

		if (outboundStage >= PackageOutboundStage.WaitingForShipping && orderLine.WaitingForShippingQuantity < quantity)
			return false;
		if (outboundStage >= PackageOutboundStage.Shipping && orderLine.ShippingQuantity < quantity)
			return false;
		if (outboundStage >= PackageOutboundStage.InDelivery && orderLine.InDeliveryQuantity < quantity)
			return false;

		return true;
	}

	private int MarkRejectedPackedStacksAsWaste(IItemContainer sourceContainer, uint itemId, int quantity)
	{
		int remaining = quantity;
		int rejected = 0;
		for (int i = sourceContainer.Stacks.Count - 1; i >= 0 && remaining > 0; --i)
		{
			ItemStack stack = sourceContainer.Stacks[i];
			if (stack == null ||
				stack.Quantity <= 0 ||
				stack.ItemID != itemId ||
				ShouldRejectOutboundStack(stack) == false)
			{
				continue;
			}

			int targetQuantity = Mathf.Min(remaining, stack.Quantity);
			if (targetQuantity == stack.Quantity)
			{
				stack.AddQuality(ItemQuality.Waste);
				stack.SetStatus(ItemStatus.None);
			}
			else
			{
				if (sourceContainer.TryRemoveFromStack(stack, targetQuantity, out ItemStack rejectedStack) == false || rejectedStack == null)
					continue;

				rejectedStack.AddQuality(ItemQuality.Waste);
				rejectedStack.SetStatus(ItemStatus.None);
				if (sourceContainer.AddStack(rejectedStack) == false)
				{
					Debug.LogError($"[OutboundQualityControl] Failed to restore rejected split. item={itemId}, quantity={targetQuantity}");
					continue;
				}
			}

			rejected += targetQuantity;
			remaining -= targetQuantity;
		}

		return rejected;
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
		if (cargoPort is not OutboundCargoPort outboundCargoPort ||
			outboundCargoPort.DockedCapsule?.RouteKind != CargoRouteKind.Standard)
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
		return ResolveLoadingTargetStation(sourcePort, sourcePort.DockedCapsule, sourcePoint);
	}

	internal LaunchStation ResolveLoadingTargetStation(CargoPort sourcePort, BoxBase payload, in int3 origin)
	{
		if (payload == null || launchStationService == null)
			return null;

		FacilityFilter facilityFilter = FacilityFilter.ForContainer(payload);
		if (loadingDestinationBuildingId != 0)
		{
			return launchStationService.TryFindDestination(loadingDestinationBuildingId, origin, InteractionKind.Put, facilityFilter, out LaunchStation selectedStation)
				? selectedStation
				: null;
		}

		if (ResolveSourceBuilding(sourcePort, out Building sourceBuilding) &&
			launchStationService.TryFindDestination(sourceBuilding.RuntimeBuildingId, origin, InteractionKind.Put, facilityFilter, out LaunchStation localStation))
		{
			return localStation;
		}

		return launchStationService.TryFindDestination(0, origin, InteractionKind.Put, facilityFilter, out LaunchStation globalStation)
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
