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
	private readonly Dictionary<uint, PickingPlanner> pickingPlannersByBuildingId = new();
	private readonly Dictionary<uint, PackingInputPlanner> packingInputPlannersByBuildingId = new();
	private readonly Dictionary<uint, PackingOutputPlanner> packingOutputPlannersByBuildingId = new();
	private readonly Dictionary<uint, LaunchSortPlanner> launchSortPlannersByBuildingId = new();
	private readonly HashSet<uint> taskScheduleBuildingIds = new();
	private readonly HashSet<uint> pendingLaunchSortEvaluationBuildingIds = new();
	private readonly HashSet<uint> evaluatingLaunchSortBuildingIds = new();
	private readonly List<uint> buildingIdScratch = new();
	private readonly List<uint> launchSortEvaluationScratch = new();
	private readonly List<PickingDispatchCandidate> pickingDispatchCandidates = new();
	private BuildingManager boundBuildingManager;
	private ItemTransferTaskScheduler boundItemTransferTaskScheduler;
	private CapsuleRelocateCoordinator boundCapsuleRelocateCoordinator;

	public PackingStationService PackingStationService => packingStationService;
	public LaunchStationService LaunchStationService => launchStationService;
	public IReadOnlyDictionary<PickingManifestKey, PickingManifest> PickingManifests => pickingManifests;
	public IEnumerable<PickingPlanner> PickingPlanners => pickingPlannersByBuildingId.Values;
	public IEnumerable<LaunchSortPlanner> LaunchSortPlanners => launchSortPlannersByBuildingId.Values;
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
		public readonly uint BuildingId;
		public readonly int PickableQuantity;

		public PickingDispatchCandidate(uint buildingId, int pickableQuantity)
		{
			BuildingId = buildingId;
			PickableQuantity = pickableQuantity;
		}
	}

	public bool TryGetPickingPlanner(uint buildingId, out PickingPlanner planner)
	{
		planner = null;
		return buildingId != 0 && pickingPlannersByBuildingId.TryGetValue(buildingId, out planner);
	}

	public bool TryGetPackingInputPlanner(uint buildingId, out PackingInputPlanner planner)
	{
		planner = null;
		return buildingId != 0 && packingInputPlannersByBuildingId.TryGetValue(buildingId, out planner);
	}

	public bool TryGetPackingOutputPlanner(uint buildingId, out PackingOutputPlanner planner)
	{
		planner = null;
		return buildingId != 0 && packingOutputPlannersByBuildingId.TryGetValue(buildingId, out planner);
	}

	public bool TryGetLaunchSortPlanner(uint buildingId, out LaunchSortPlanner planner)
	{
		planner = null;
		return buildingId != 0 && launchSortPlannersByBuildingId.TryGetValue(buildingId, out planner);
	}

	public int AcceptPickingRequest(
		uint buildingId,
		OrderLine orderLine,
		int quantity,
		out PickingRequest firstRequest)
	{
		firstRequest = null;
		if (orderLine == null || quantity <= 0 || orderLine.CanAllocatePicking == false ||
			TryGetPickingPlanner(buildingId, out PickingPlanner planner) == false)
		{
			return 0;
		}

		int accepted = planner.AcceptPickingRequest(orderLine, quantity, out firstRequest);
		if (accepted > 0 && GameContext.HasInstance)
		{
			GameContext.Instance.ItemTransferTaskScheduler?.MarkDirty(
				buildingId,
				ItemTransferScheduleMode.Picking);
		}

		return accepted;
	}

	public int GetPickableQuantity(uint buildingId, uint itemId)
	{
		return TryGetPickingPlanner(buildingId, out PickingPlanner planner)
			? planner.GetPickableQuantity(itemId)
			: 0;
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
			Building building = buildings[i];
			if (building?.RuntimeBuildingId == 0)
				continue;

			QueueLaunchSortEvaluation(building.RuntimeBuildingId);
			if (building.OutboundTargetStage == ItemProcessStage.LaunchReady && GameContext.HasInstance)
				GameContext.Instance.CapsuleRelocateCoordinator.MarkBuildingDirty(building.RuntimeBuildingId);
		}
	}

	public void QueueLaunchSortEvaluation(uint buildingId)
	{
		if (buildingId != 0)
			pendingLaunchSortEvaluationBuildingIds.Add(buildingId);
	}

	public void ProcessPendingLaunchSortEvaluations()
	{
		if (pendingLaunchSortEvaluationBuildingIds.Count <= 0)
			return;

		launchSortEvaluationScratch.Clear();
		launchSortEvaluationScratch.AddRange(pendingLaunchSortEvaluationBuildingIds);
		pendingLaunchSortEvaluationBuildingIds.Clear();
		for (int i = 0; i < launchSortEvaluationScratch.Count; ++i)
			EvaluateLaunchSortWork(launchSortEvaluationScratch[i]);
		launchSortEvaluationScratch.Clear();
	}

	private void EvaluateLaunchSortWork(uint buildingId)
	{
		ItemTransferTaskScheduler scheduler = boundItemTransferTaskScheduler ??
			(GameContext.HasInstance ? GameContext.Instance.ItemTransferTaskScheduler : null);
		if (buildingId == 0 ||
			scheduler == null ||
			evaluatingLaunchSortBuildingIds.Add(buildingId) == false)
		{
			return;
		}

		try
		{
			if (BuildingManager?.TryGetBuilding(buildingId, out Building building) != true ||
				building == null ||
				building.OutboundTargetStage != ItemProcessStage.LaunchReady ||
				TryGetLaunchSortPlanner(buildingId, out LaunchSortPlanner planner) == false)
			{
				scheduler.ClearDirty(buildingId, ItemTransferScheduleMode.LaunchSort);
				return;
			}

			RejectInvalidOutboundStandbyCargo(buildingId);
			if (planner.HasSortableWork())
				scheduler.MarkDirty(buildingId, ItemTransferScheduleMode.LaunchSort);
			else
				scheduler.ClearDirty(buildingId, ItemTransferScheduleMode.LaunchSort);
		}
		finally
		{
			evaluatingLaunchSortBuildingIds.Remove(buildingId);
		}
	}

	private void RejectInvalidOutboundStandbyCargo(uint buildingId)
	{
		CapsuleBufferService bufferService = GameContext.HasInstance
			? GameContext.Instance.CapsuleBufferSvc
			: null;
		if (bufferService == null)
			return;

		foreach (CapsuleBuffer buffer in bufferService.GetBuffers(buildingId))
		{
			if (buffer?.DockedCapsule == null ||
				(buffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.Inside &&
				 buffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.OB))
			{
				continue;
			}

			RejectInvalidPackedCargo(buffer);
			if ((HasDispatchBlockingCargo(buffer) || HasCompleteDispatchManifest(buffer) == false) &&
				buffer.DockedCapsule.LogisticsState == CapsuleLogisticsState.OB)
			{
				GameContext.Instance.CapsuleRelocateCoordinator.MarkDirty(buffer);
			}
		}
	}

	internal bool TryPrepareOutboundDispatch(Building building, CapsuleBuffer capsuleBuffer)
	{
		if (building == null || capsuleBuffer?.DockedCapsule == null)
			return false;

		if (building.OutboundTargetStage != ItemProcessStage.LaunchReady)
			return building.CanDispatchOutboundBuffer(capsuleBuffer);

		RejectInvalidPackedCargo(capsuleBuffer);
		if (HasDispatchBlockingCargo(capsuleBuffer) ||
			HasCompleteDispatchManifest(capsuleBuffer) == false)
		{
			if (capsuleBuffer.DockedCapsule.LogisticsState == CapsuleLogisticsState.OB)
				GameContext.Instance.CapsuleRelocateCoordinator.MarkDirty(capsuleBuffer);
			QueueLaunchSortEvaluation(building.RuntimeBuildingId);
			return false;
		}

		return building.CanDispatchOutboundBuffer(capsuleBuffer);
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

		if (facility is ShelfBase shelf)
		{
			foreach (PickingPlanner planner in pickingPlannersByBuildingId.Values)
				planner?.CancelRequestsForSource(shelf);
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
		foreach (PickingPlanner planner in pickingPlannersByBuildingId.Values)
			planner?.SetPickingPolicy(policyType);
	}

	public void SetPickingCollectingPolicy(CollectingPolicyType policyType)
	{
		defaultPickingCollectingPolicyType = policyType;
		foreach (PickingPlanner planner in pickingPlannersByBuildingId.Values)
			planner?.SetCollectingPolicy(policyType);
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
		foreach (PickingPlanner planner in pickingPlannersByBuildingId.Values)
			planner?.SetBoxFillLimitPercent(pickingBoxFillLimitPercent);
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
		{
			ClearPickingManifest(PickingManifestKey.From(box));
			MarkCapsuleRoutingDirty(box);
		}
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
		int added = manifest != null ? manifest.AddPicked(orderLine, itemId, quantity) : 0;
		if (added > 0)
			MarkCapsuleRoutingDirty(box);
		return added;
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
		MarkCapsuleRoutingDirty(from);
		MarkCapsuleRoutingDirty(to);

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
		if (movedTotal > 0)
		{
			MarkCapsuleRoutingDirty(from);
			MarkCapsuleRoutingDirty(to);
		}

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

		int packed = manifest.ReportPacked(orderLine, itemId, quantity);
		if (packed > 0)
			MarkCapsuleRoutingDirty(box);
		return packed;
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
		MarkCapsuleRoutingDirty(manifestOwner);
		return removed;
	}

	private static void MarkCapsuleRoutingDirty(BoxBase box)
	{
		if (box is CargoCapsule capsule && capsule.CurrentDock != null && GameContext.HasInstance)
			GameContext.Instance.CapsuleRelocateCoordinator.MarkDirty(capsule.CurrentDock);
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
		if (cargoPort is not OutboundCargoPort ||
			cargoPort.DockedCapsule?.RouteKind != CargoRouteKind.Standard ||
			cargoPort.CanGetBox() == false)
			return;

		uint requestedBuildingId = ResolveSourceBuilding(cargoPort, out Building sourceBuilding)
			? sourceBuilding.RuntimeBuildingId
			: 0;
		TaskMgr.EnqueueTaskBuildRequest(new LoadingTaskBuildRequest(cargoPort, requestedBuildingId));
	}

	private void Start()
	{
		SubscribeCargoPortEvents();
		BindBuildingEvents();
	}

	private void OnEnable()
	{
		SubscribeCargoPortEvents();
		BindBuildingEvents();
	}

	private void OnDisable()
	{
		UnsubscribeCargoPortEvents();
		UnbindBuildingEvents();
	}

	private void BindBuildingEvents()
	{
		if (boundBuildingManager == null && GameContext.HasInstance)
		{
			boundBuildingManager = GameContext.Instance.BuildingMgr;
			if (boundBuildingManager != null)
				boundBuildingManager.OnBuildingsChanged += HandleBuildingsChanged;
		}

		if (boundItemTransferTaskScheduler == null && GameContext.HasInstance)
			boundItemTransferTaskScheduler = GameContext.Instance.ItemTransferTaskScheduler;

		if (boundCapsuleRelocateCoordinator == null && GameContext.HasInstance)
		{
			boundCapsuleRelocateCoordinator = GameContext.Instance.ExistingCapsuleRelocateCoordinator;
			if (boundCapsuleRelocateCoordinator != null)
				boundCapsuleRelocateCoordinator.OnRuleRoutingEvaluated += HandleRuleRoutingEvaluated;
		}

		SyncBuildingTaskProducers();
	}

	private void UnbindBuildingEvents()
	{
		if (boundBuildingManager != null)
			boundBuildingManager.OnBuildingsChanged -= HandleBuildingsChanged;
		if (boundCapsuleRelocateCoordinator != null)
			boundCapsuleRelocateCoordinator.OnRuleRoutingEvaluated -= HandleRuleRoutingEvaluated;

		UnregisterBuildingTaskProducers();
		boundBuildingManager = null;
		boundItemTransferTaskScheduler = null;
		boundCapsuleRelocateCoordinator = null;
	}

	private void HandleBuildingsChanged()
	{
		SyncBuildingTaskProducers();
	}

	private void HandleRuleRoutingEvaluated(uint buildingId, CapsuleBuffer buffer, bool isRuleMatched)
	{
		EvaluatePackingInputWork(buildingId);
		QueueLaunchSortEvaluation(buildingId);
	}

	private void SyncBuildingTaskProducers()
	{
		BuildingManager buildingManager = boundBuildingManager ??
			(GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null);
		ItemTransferTaskScheduler scheduler = boundItemTransferTaskScheduler ??
			(GameContext.HasInstance ? GameContext.Instance.ItemTransferTaskScheduler : null);
		if (buildingManager == null || scheduler == null)
			return;

		buildingIdScratch.Clear();
		IReadOnlyList<Building> buildings = buildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			uint buildingId = buildings[i]?.RuntimeBuildingId ?? 0;
			if (buildingId == 0)
				continue;

			buildingIdScratch.Add(buildingId);
			if (pickingPlannersByBuildingId.ContainsKey(buildingId) == false)
			{
				pickingPlannersByBuildingId.Add(
					buildingId,
					new PickingPlanner(
						buildingId,
						pickingBoxFillLimitPercent,
						defaultPickingCollectingPolicyType,
						defaultPickingPolicyType));
			}
			if (packingInputPlannersByBuildingId.ContainsKey(buildingId) == false)
				packingInputPlannersByBuildingId.Add(buildingId, new PackingInputPlanner(buildingId));
			if (packingOutputPlannersByBuildingId.ContainsKey(buildingId) == false)
				packingOutputPlannersByBuildingId.Add(buildingId, new PackingOutputPlanner(buildingId));
			if (launchSortPlannersByBuildingId.ContainsKey(buildingId) == false)
				launchSortPlannersByBuildingId.Add(buildingId, new LaunchSortPlanner(buildingId));

			scheduler.Register(
				buildingId,
				ItemTransferScheduleMode.Picking,
				WorkerTask.TaskType.Picking,
				TryBuildPickingItemTransferTask);
			scheduler.Register(
				buildingId,
				ItemTransferScheduleMode.PackingInput,
				WorkerTask.TaskType.PackingInput,
				TryBuildPackingInputItemTransferTask);
			scheduler.Register(
				buildingId,
				ItemTransferScheduleMode.PackingOutput,
				WorkerTask.TaskType.PackingOutput,
				TryBuildPackingOutputItemTransferTask);
			scheduler.Register(
				buildingId,
				ItemTransferScheduleMode.LaunchSort,
				WorkerTask.TaskType.LaunchSort,
				TryBuildLaunchSortItemTransferTask);
			taskScheduleBuildingIds.Add(buildingId);
			EvaluatePickingWork(buildingId);
			EvaluatePackingInputWork(buildingId);
			EvaluatePackingOutputWork(buildingId);
			QueueLaunchSortEvaluation(buildingId);
		}

		uint[] registeredIds = new uint[taskScheduleBuildingIds.Count];
		taskScheduleBuildingIds.CopyTo(registeredIds);
		for (int i = 0; i < registeredIds.Length; ++i)
		{
			uint buildingId = registeredIds[i];
			if (buildingIdScratch.Contains(buildingId))
				continue;

			if (pickingPlannersByBuildingId.TryGetValue(buildingId, out PickingPlanner planner))
				planner?.CancelAllRequests();

			scheduler.Unregister(buildingId, ItemTransferScheduleMode.Picking);
			scheduler.Unregister(buildingId, ItemTransferScheduleMode.PackingInput);
			scheduler.Unregister(buildingId, ItemTransferScheduleMode.PackingOutput);
			scheduler.Unregister(buildingId, ItemTransferScheduleMode.LaunchSort);
			taskScheduleBuildingIds.Remove(buildingId);
			pickingPlannersByBuildingId.Remove(buildingId);
			packingInputPlannersByBuildingId.Remove(buildingId);
			packingOutputPlannersByBuildingId.Remove(buildingId);
			launchSortPlannersByBuildingId.Remove(buildingId);
			pendingLaunchSortEvaluationBuildingIds.Remove(buildingId);
		}
	}

	private void UnregisterBuildingTaskProducers()
	{
		ItemTransferTaskScheduler scheduler = boundItemTransferTaskScheduler ??
			(GameContext.HasInstance ? GameContext.Instance.ItemTransferTaskScheduler : null);
		if (scheduler != null)
		{
			foreach (uint buildingId in taskScheduleBuildingIds)
			{
				scheduler.Unregister(buildingId, ItemTransferScheduleMode.Picking);
				scheduler.Unregister(buildingId, ItemTransferScheduleMode.PackingInput);
				scheduler.Unregister(buildingId, ItemTransferScheduleMode.PackingOutput);
				scheduler.Unregister(buildingId, ItemTransferScheduleMode.LaunchSort);
			}
		}

		taskScheduleBuildingIds.Clear();
		pendingLaunchSortEvaluationBuildingIds.Clear();
		evaluatingLaunchSortBuildingIds.Clear();
	}

	private ItemTransferScheduleResult TryBuildPickingItemTransferTask(
		ItemTransferScheduleRequest request,
		out WorkerTask task)
	{
		task = null;
		if (request.Worker == null || request.Worker.CanAcceptGeneralTask(request.TaskType) == false)
			return ItemTransferScheduleResult.WorkerRejected;

		if (TryGetPickingPlanner(request.BuildingId, out PickingPlanner planner) == false ||
			planner.HasPendingCollect(request.BuildingId) == false ||
			planner.BuildItemTransferTask(request.Worker, out ItemTransferTask itemTransferTask) == false)
		{
			return ItemTransferScheduleResult.NoWork;
		}

		task = itemTransferTask;
		return ItemTransferScheduleResult.Scheduled;
	}

	private ItemTransferScheduleResult TryBuildPackingInputItemTransferTask(
		ItemTransferScheduleRequest request,
		out WorkerTask task)
	{
		task = null;
		if (request.Worker == null || request.Worker.CanAcceptGeneralTask(request.TaskType) == false)
			return ItemTransferScheduleResult.WorkerRejected;

		if (TryGetPackingInputPlanner(request.BuildingId, out PackingInputPlanner planner) == false ||
			planner.HasAvailableWork() == false)
		{
			return ItemTransferScheduleResult.NoWork;
		}

		if (planner.HasAvailableWork(request.Worker) == false)
			return ItemTransferScheduleResult.WorkerRejected;

		task = new ItemTransferTask(
			WorkerTask.TaskType.PackingInput,
			new ItemTransferJob(
				planner,
				TransferObjectType.Item,
				TransferObjectType.Box,
				request.BuildingId,
				request.Worker));
		return ItemTransferScheduleResult.Scheduled;
	}

	private ItemTransferScheduleResult TryBuildPackingOutputItemTransferTask(
		ItemTransferScheduleRequest request,
		out WorkerTask task)
	{
		task = null;
		if (request.Worker == null || request.Worker.CanAcceptGeneralTask(request.TaskType) == false)
			return ItemTransferScheduleResult.WorkerRejected;

		if (TryGetPackingOutputPlanner(request.BuildingId, out PackingOutputPlanner planner) == false ||
			planner.HasAvailableWork() == false)
		{
			return ItemTransferScheduleResult.NoWork;
		}

		task = new ItemTransferTask(
			WorkerTask.TaskType.PackingOutput,
			new ItemTransferJob(
				planner,
				TransferObjectType.Box,
				TransferObjectType.Item,
				request.BuildingId,
				request.Worker));
		return ItemTransferScheduleResult.Scheduled;
	}

	private ItemTransferScheduleResult TryBuildLaunchSortItemTransferTask(
		ItemTransferScheduleRequest request,
		out WorkerTask task)
	{
		task = null;
		if (request.Worker == null || request.Worker.CanAcceptGeneralTask(request.TaskType) == false)
			return ItemTransferScheduleResult.WorkerRejected;

		if (TryGetLaunchSortPlanner(request.BuildingId, out LaunchSortPlanner planner) == false ||
			planner.HasSortableWork() == false)
		{
			return ItemTransferScheduleResult.NoWork;
		}

		if (planner.HasSortableWork(request.Worker) == false)
			return ItemTransferScheduleResult.WorkerRejected;

		task = new ItemTransferTask(
			WorkerTask.TaskType.LaunchSort,
			new ItemTransferJob(
				planner,
				TransferObjectType.Item,
				TransferObjectType.Item,
				request.BuildingId,
				request.Worker));
		return ItemTransferScheduleResult.Scheduled;
	}

	private void EvaluatePickingWork(uint buildingId)
	{
		if (buildingId == 0)
			return;

		ItemTransferTaskScheduler scheduler = boundItemTransferTaskScheduler ??
			(GameContext.HasInstance ? GameContext.Instance.ItemTransferTaskScheduler : null);
		if (scheduler == null)
			return;
		if (TryGetPickingPlanner(buildingId, out PickingPlanner planner) &&
			planner.HasPendingCollect(buildingId))
		{
			scheduler.MarkDirty(buildingId, ItemTransferScheduleMode.Picking);
		}
		else
		{
			scheduler.ClearDirty(buildingId, ItemTransferScheduleMode.Picking);
		}
	}

	public void EvaluatePackingInputWork(uint buildingId)
	{
		ItemTransferTaskScheduler scheduler = boundItemTransferTaskScheduler ??
			(GameContext.HasInstance ? GameContext.Instance.ItemTransferTaskScheduler : null);
		if (buildingId == 0 || scheduler == null)
			return;

		if (TryGetPackingInputPlanner(buildingId, out PackingInputPlanner planner) &&
			planner.HasAvailableWork())
		{
			scheduler.MarkDirty(buildingId, ItemTransferScheduleMode.PackingInput);
		}
		else
		{
			scheduler.ClearDirty(buildingId, ItemTransferScheduleMode.PackingInput);
		}
	}

	public void EvaluatePackingOutputWork(uint buildingId)
	{
		ItemTransferTaskScheduler scheduler = boundItemTransferTaskScheduler ??
			(GameContext.HasInstance ? GameContext.Instance.ItemTransferTaskScheduler : null);
		if (buildingId == 0 || scheduler == null)
			return;

		if (TryGetPackingOutputPlanner(buildingId, out PackingOutputPlanner planner) &&
			planner.HasAvailableWork())
		{
			scheduler.MarkDirty(buildingId, ItemTransferScheduleMode.PackingOutput);
		}
		else
		{
			scheduler.ClearDirty(buildingId, ItemTransferScheduleMode.PackingOutput);
		}
	}

	public void GetPackingInputDemand(uint buildingId, out int sourceCount, out int itemQuantity)
	{
		if (TryGetPackingInputPlanner(buildingId, out PackingInputPlanner planner))
		{
			planner.GetPendingDemand(out sourceCount, out itemQuantity);
			return;
		}

		sourceCount = 0;
		itemQuantity = 0;
	}

	public void GetPackingInputDemand(out int sourceCount, out int itemQuantity)
	{
		sourceCount = 0;
		itemQuantity = 0;
		foreach (PackingInputPlanner planner in packingInputPlannersByBuildingId.Values)
		{
			planner.GetPendingDemand(out int plannerSources, out int plannerQuantity);
			sourceCount += plannerSources;
			itemQuantity += plannerQuantity;
		}
	}

	public void GetPackingOutputDemand(uint buildingId, out int sourceCount, out int itemQuantity)
	{
		if (packingStationService != null)
		{
			packingStationService.GetCompletedOutputDemand(buildingId, out sourceCount, out itemQuantity);
			return;
		}

		sourceCount = 0;
		itemQuantity = 0;
	}

	public void GetPackingOutputDemand(out int sourceCount, out int itemQuantity)
	{
		if (packingStationService != null)
		{
			packingStationService.GetCompletedOutputDemand(out sourceCount, out itemQuantity);
			return;
		}

		sourceCount = 0;
		itemQuantity = 0;
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
			if (candidate.BuildingId == 0 || candidate.PickableQuantity <= 0)
				continue;

			int quantity = Mathf.Min(remaining, candidate.PickableQuantity);
			if (quantity <= 0)
				continue;

			int accepted = AcceptPickingRequest(candidate.BuildingId, orderLine, quantity, out _);
			if (accepted <= 0)
				continue;

			remaining -= accepted;
		}
	}

	private void BuildPickingDispatchCandidates(uint itemId)
	{
		pickingDispatchCandidates.Clear();
		foreach (KeyValuePair<uint, PickingPlanner> entry in pickingPlannersByBuildingId)
		{
			uint buildingId = entry.Key;
			PickingPlanner planner = entry.Value;
			if (buildingId == 0 || planner == null)
				continue;

			int pickable = planner.GetPickableQuantity(itemId);
			if (pickable <= 0)
				continue;

			pickingDispatchCandidates.Add(new PickingDispatchCandidate(buildingId, pickable));
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
