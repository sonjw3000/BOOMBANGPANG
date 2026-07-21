using System.Collections.Generic;
using UnityEngine;

public sealed class PickingRequest
{
	public readonly OrderLine OrderLine;
	public readonly ShelfBase Source;
	public readonly uint ItemId;

	private int reservedQuantity;
	private int allocatedQuantity;

	public int AllocatedQuantity => allocatedQuantity;
	public int RequestedQuantity => reservedQuantity;
	public int RemainingQuantity => Mathf.Max(0, reservedQuantity - allocatedQuantity);
	public bool IsComplete => RemainingQuantity <= 0 || OrderLine == null || OrderLine.IsFinal;

	public PickingRequest(OrderLine orderLine, ShelfBase source, int reservedQuantity)
	{
		OrderLine = orderLine;
		Source = source;
		ItemId = orderLine != null ? orderLine.ItemID : 0;
		this.reservedQuantity = Mathf.Max(0, reservedQuantity);
	}

	public int GetAllocatableQuantity()
	{
		if (OrderLine == null || OrderLine.IsFinal)
			return 0;

		return RemainingQuantity;
	}

	public int ReportAllocated(int quantity)
	{
		int actual = Mathf.Clamp(quantity, 0, RemainingQuantity);
		allocatedQuantity += actual;
		return actual;
	}

	public int ReleaseAllocated(int quantity)
	{
		int actual = Mathf.Clamp(quantity, 0, allocatedQuantity);
		allocatedQuantity -= actual;
		return actual;
	}

	public int ReleaseReserved(int quantity)
	{
		int releasable = Mathf.Max(0, reservedQuantity - allocatedQuantity);
		int actual = Mathf.Clamp(quantity, 0, releasable);
		reservedQuantity -= actual;
		return actual;
	}
}

public sealed class PickingPlanner : IItemTransferPlanner, IItemTransferTaskInvalidationHandler
{
	private const string InventoryDigitizationResearchId = "inventory_digitization";
	private static int jobID = 1;

	private readonly StorageBuilding ownerBuilding;
	private readonly PickingRequestSource requestSource = new();
	private readonly Dictionary<AIWorker, ManualPickingSession> manualSessions = new();
	private readonly HashSet<PickingRequest> claimedManualRequests = new();
	private PickingPolicyType pickingPolicyType;
	private ICollectingPolicy<PickingRequest> requestCollectingPolicy;
	private CollectingPolicyType collectingPolicyType;
	private float boxFillLimitPercent;

	public static int GetNextJobId() => jobID;
	public static void SetNextJobId(int nextJobId) => jobID = nextJobId;
	public PickingPolicyType PickingPolicyType => pickingPolicyType;
	public CollectingPolicyType CollectingPolicyType => collectingPolicyType;
	private uint BuildingId => ownerBuilding != null ? ownerBuilding.RuntimeBuildingId : 0;

	private CapsuleBufferService CapsuleBufferService => GameContext.Instance.CapsuleBufferSvc;

	public PickingPlanner(
		StorageBuilding ownerBuilding,
		float boxFillLimitPercent,
		CollectingPolicyType collectingPolicyType = CollectingPolicyType.Nearest,
		PickingPolicyType pickingPolicyType = PickingPolicyType.ManualShelfScan)
	{
		this.ownerBuilding = ownerBuilding;
		this.boxFillLimitPercent = boxFillLimitPercent;
		SetCollectingPolicy(collectingPolicyType);
		SetPickingPolicy(pickingPolicyType);
	}

	public void SetPickingPolicy(PickingPolicyType policyType)
	{
		pickingPolicyType = policyType;
	}

	public void SetCollectingPolicy(CollectingPolicyType policyType)
	{
		collectingPolicyType = policyType;
		requestCollectingPolicy = CollectingPolicyFactory.Create<PickingRequest>(policyType);
	}

	public void SetBoxFillLimitPercent(float value)
	{
		boxFillLimitPercent = value;
	}

	public bool HasPendingCollectWork()
	{
		return HasPendingCollectWork(BuildingId);
	}

	public bool HasPendingCollectWork(uint buildingId)
	{
		uint targetBuildingId = ResolveBuildingId(buildingId);
		return HasPendingCollect(targetBuildingId);
	}

	public bool HasPendingCollect(uint buildingId)
	{
		foreach (PickingRequest request in requestSource.GetRequests())
		{
			if (request == null || request.GetAllocatableQuantity() <= 0)
				continue;

			if (request.Source != null || claimedManualRequests.Contains(request) == false)
				return true;
		}

		return false;
	}

	public int AcceptPickingRequest(OrderLine orderLine, int quantity, out PickingRequest firstRequest)
	{
		return CanUseInventoryGuidance()
			? AcceptLocatedPickingRequest(orderLine, quantity, out firstRequest)
			: AcceptManualPickingRequest(orderLine, quantity, out firstRequest);
	}

	public bool AddReservedPickingRequest(OrderLine orderLine, ShelfBase source, int reservedQuantity, out PickingRequest request)
	{
		request = null;
		if (BuildingId == 0 ||
			orderLine == null ||
			source == null ||
			reservedQuantity <= 0 ||
			orderLine.IsFinal)
		{
			return false;
		}

		request = requestSource.Add(orderLine, source, reservedQuantity);
		return request != null;
	}

	public bool BuildItemTransferTask(AIWorker preferredWorker, out ItemTransferTask task)
	{
		task = null;
		uint targetBuildingId = BuildingId;
		if (preferredWorker == null ||
			(TryClaimManualRequest(preferredWorker) == false && HasPendingLocatedRequest() == false))
			return false;

		ItemTransferJob job = new(
			this,
			TransferObjectType.Item,
			TransferObjectType.Item,
			targetBuildingId,
			preferredWorker);
		task = new ItemTransferTask(WorkerTask.TaskType.Picking, job);
		return true;
	}

	public bool TryAllocateNextCollectLine(AIWorker worker, out WorkLine line)
	{
		return TryAllocateNextCollectLine(worker, BuildingId, out line);
	}

	public bool TryAllocateNextCollectLine(AIWorker worker, uint buildingId, out WorkLine line)
	{
		return TryGetCollectLine(worker, buildingId, out line) == WorkPlanResult.Issued;
	}

	public WorkPlanResult TryGetCollectLine(AIWorker worker, uint buildingId, out WorkLine line)
	{
		uint targetBuildingId = ResolveBuildingId(buildingId);
		line = null;
		if (worker == null)
			return WorkPlanResult.Waiting;

		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (box == null)
			return WorkPlanResult.Waiting;

		if (HasReachedBoxFillLimit(box))
		{
			ReleaseManualSession(worker, keepRequest: true);
			return WorkPlanResult.SwitchPhase;
		}

		if (manualSessions.TryGetValue(worker, out ManualPickingSession manualSession))
			return TryGetManualCollectLine(worker, box, manualSession, out line);

		if (requestSource.HasAny())
		{
			if (TryAllocateNextCollect(worker, targetBuildingId, out line))
				return WorkPlanResult.Issued;

			return box.Stacks.Count > 0 ? WorkPlanResult.SwitchPhase : WorkPlanResult.Completed;
		}

		return box.Stacks.Count > 0 ? WorkPlanResult.SwitchPhase : WorkPlanResult.Completed;
	}

	public WorkPlanResult TryGetCollectLine(AIWorker worker, out WorkLine line)
	{
		return TryGetCollectLine(worker, BuildingId, out line);
	}

	public WorkPlanResult OnCollectCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		if (line != null && line.ConsumeSourcePickReservation == false)
			return OnManualCollectCompleted(worker, line, result);

		ReleaseUnmovedReservation(line, result);

		if (line == null || result.Moved <= 0)
			return WorkPlanResult.Waiting;

		ReportCollected(worker, line, result.Moved);

		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		if (HasReachedBoxFillLimit(box))
			return WorkPlanResult.SwitchPhase;

		return WorkPlanResult.Issued;
	}

	public void OnTaskInvalidated(ItemTransferTask task)
	{
		if (task == null)
			return;

		if (task.TryGetPreferredWorker(out AIWorker preferredWorker))
			ReleaseManualSession(preferredWorker, keepRequest: true);

		WorkLine line = task.Phase == ItemTransferPhase.Collect ? task.CurrentLine : null;
		if (line != null && line.ConsumeSourcePickReservation)
		{
			int remaining = Mathf.Max(0, line.Quantity - line.CompleteQuantity);
			if (remaining > 0)
			{
				int released = requestSource.ReleaseAllocated(line.RelatedOrderLine, line.Container as ShelfBase, line.ItemID, remaining);
				if (released != remaining)
					Debug.LogWarning($"[PickingPlanner] Task allocation rollback mismatch. requested={remaining}, released={released}");
			}
		}

		if (HasPendingCollect(BuildingId))
			GameContext.Instance.ItemTransferTaskScheduler?.MarkDirty(BuildingId, ItemTransferScheduleMode.Picking);
	}

	public int CancelRequestsForSource(ShelfBase source)
	{
		if (source == null)
			return 0;

		OrderManager orderManager = GameContext.HasInstance ? GameContext.Instance.OrderMgr : null;
		List<PickingRequest> requests = new(requestSource.GetAllRequests());
		int cancelledQuantity = 0;
		for (int i = 0; i < requests.Count; ++i)
		{
			PickingRequest request = requests[i];
			if (request == null || request.Source != source)
				continue;

			int releasable = request.GetAllocatableQuantity();
			if (releasable > 0)
			{
				int releasedReservation = source.ReleaseReservedPick(request.ItemId, releasable);
				int releasedAllocation = orderManager != null
					? orderManager.ReleasePickingAllocation(request.OrderLine, releasedReservation)
					: 0;
				if (orderManager != null && releasedAllocation != releasedReservation)
				{
					Debug.LogWarning($"[PickingPlanner] Facility invalidation allocation rollback mismatch. requested={releasedReservation}, released={releasedAllocation}");
				}

				request.ReleaseReserved(releasedReservation);
				cancelledQuantity += releasedReservation;
			}

			requestSource.Remove(request);
		}

		return cancelledQuantity;
	}

	public WorkPlanResult TryGetPlaceLine(AIWorker worker, uint buildingId, WorkLine pickedLine, out WorkLine line)
	{
		return TryGetPlaceLine(worker, buildingId, pickedLine, pickedLine != null ? pickedLine.Quantity : 0, out line);
	}

	public WorkPlanResult TryGetPlaceLine(AIWorker worker, WorkLine pickedLine, out WorkLine line)
	{
		return TryGetPlaceLine(worker, BuildingId, pickedLine, out line);
	}

	public WorkPlanResult TryGetPlaceLine(AIWorker worker, uint buildingId, WorkLine pickedLine, int remainingQuantity, out WorkLine line)
	{
		uint targetBuildingId = ResolveBuildingId(buildingId);
		line = null;
		if (worker == null || pickedLine == null || remainingQuantity <= 0)
			return WorkPlanResult.Waiting;

		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (box == null)
			return WorkPlanResult.Waiting;

		OutboundWorkflowService outboundWorkflowService = GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null;
		if (outboundWorkflowService == null ||
			outboundWorkflowService.GetPackableManifestQuantity(box, pickedLine.RelatedOrderLine, pickedLine.ItemID) < remainingQuantity ||
			ItemTransferUtility.GetMovableQuantity(box, box, pickedLine.ItemID, remainingQuantity) < remainingQuantity)
		{
			return WorkPlanResult.Completed;
		}

		CapsuleBuffer bestBuffer = null;
		int bestDistance = int.MaxValue;
		FacilityFilter filter = FacilityFilter.ForTransfer(
			box,
			pickedLine.ItemID,
			remainingQuantity,
			stack => pickedLine.RequiredStatus.HasValue == false || stack.HasStatus(pickedLine.RequiredStatus.Value),
			worker);
		foreach (CapsuleBuffer buffer in EnumeratePlaceBuffers(targetBuildingId))
		{
			if (buffer == null)
				continue;

			if (filter.MatchesCurrentRules(buffer) == false)
				continue;

			int movable = ItemTransferUtility.GetMovableQuantity(box, buffer, pickedLine.ItemID, remainingQuantity);
			if (movable < remainingQuantity)
				continue;

			if (InteractionPointSelector.TryGetInteractionPoint(
				buffer,
				InteractionKind.Put,
				worker.GridPosition,
				out _,
				out int distance) == false)
			{
				continue;
			}

			if (distance >= bestDistance)
				continue;

			bestBuffer = buffer;
			bestDistance = distance;
		}

		if (bestBuffer == null)
			return WorkPlanResult.Waiting;

		line = new WorkLine(WorkLineAction.Put, bestBuffer, bestBuffer, pickedLine.ItemID, remainingQuantity, pickedLine.RelatedOrderLine);
		return WorkPlanResult.Issued;
	}

	public WorkPlanResult OnPlaceCompleted(AIWorker worker, WorkLine collectedLine, WorkLine placeLine, ItemTransferResult result)
	{
		if (result.Kind == TransferResultKind.None)
			return WorkPlanResult.Waiting;

		TransferPickingManifest(worker?.CarryingAbility?.CarryingBox, placeLine, result.Moved);
		return OnPlaceLineCompleted(worker, placeLine, result);
	}

	public WorkPlanResult OnPlaceLineCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		if (result.Kind == TransferResultKind.None)
			return WorkPlanResult.Waiting;

		return WorkPlanResult.Issued;
	}

	private int AcceptLocatedPickingRequest(OrderLine orderLine, int quantity, out PickingRequest firstRequest)
	{
		firstRequest = null;
		if (BuildingId == 0 || orderLine == null || quantity <= 0 || orderLine.CanAllocatePicking == false)
			return 0;

		OrderManager orderManager = GameContext.HasInstance ? GameContext.Instance.OrderMgr : null;
		ShelfStorageService storageService = GameContext.HasInstance ? GameContext.Instance.StorageService : null;
		if (orderManager == null || storageService == null)
			return 0;

		int remaining = Mathf.Min(quantity, orderLine.GetPickingAllocatableQuantity());
		int accepted = 0;
		foreach (ShelfBase source in storageService.GetSources(BuildingId, orderLine.ItemID))
		{
			if (source == null || remaining <= 0)
				continue;

			int reserved = source.ReservePicking(orderLine.ItemID, remaining);
			if (reserved <= 0)
				continue;

			int allocated = orderManager.AllocatePicking(orderLine, reserved);
			if (allocated <= 0)
			{
				source.ReleaseReservedPick(orderLine.ItemID, reserved);
				continue;
			}

			if (allocated < reserved)
			{
				source.ReleaseReservedPick(orderLine.ItemID, reserved - allocated);
				reserved = allocated;
			}

			if (AddReservedPickingRequest(orderLine, source, reserved, out PickingRequest request) == false)
			{
				source.ReleaseReservedPick(orderLine.ItemID, reserved);
				orderManager.ReleasePickingAllocation(orderLine, reserved);
				continue;
			}

			firstRequest ??= request;
			accepted += reserved;
			remaining -= reserved;
		}

		return accepted;
	}

	private int AcceptManualPickingRequest(OrderLine orderLine, int quantity, out PickingRequest request)
	{
		request = null;
		if (BuildingId == 0 || orderLine == null || quantity <= 0 || orderLine.CanAllocatePicking == false)
			return 0;

		OrderManager orderManager = GameContext.HasInstance ? GameContext.Instance.OrderMgr : null;
		if (orderManager == null)
			return 0;

		int requested = Mathf.Min(quantity, orderLine.GetPickingAllocatableQuantity());
		int allocated = orderManager.AllocatePicking(orderLine, requested);
		if (allocated <= 0)
			return 0;

		request = requestSource.Add(orderLine, null, allocated);
		if (request != null)
			return allocated;

		orderManager.ReleasePickingAllocation(orderLine, allocated);
		return 0;
	}

	private bool TryClaimManualRequest(AIWorker worker)
	{
		if (worker == null)
			return false;

		if (manualSessions.ContainsKey(worker))
			return true;

		foreach (PickingRequest request in requestSource.GetRequests())
		{
			if (request == null ||
				request.Source != null ||
				request.GetAllocatableQuantity() <= 0 ||
				claimedManualRequests.Add(request) == false)
			{
				continue;
			}

			manualSessions[worker] = new ManualPickingSession(request);
			return true;
		}

		return false;
	}

	private WorkPlanResult TryGetManualCollectLine(
		AIWorker worker,
		BoxBase box,
		ManualPickingSession session,
		out WorkLine line)
	{
		line = null;
		PickingRequest request = session?.Request;
		if (request == null || request.GetAllocatableQuantity() <= 0)
			return FinishManualScan(worker, box, session);

		int quantity = box.GetAcceptableQuantity(request.ItemId, request.GetAllocatableQuantity());
		if (quantity <= 0)
		{
			if (box.Stacks.Count > 0)
			{
				ReleaseManualSession(worker, keepRequest: true);
				return WorkPlanResult.SwitchPhase;
			}

			return FinishManualScan(worker, box, session);
		}

		ShelfBase shelf = FindNextManualShelf(worker, session);
		if (shelf == null)
			return FinishManualScan(worker, box, session);

		session.VisitedShelves.Add(shelf);
		line = new WorkLine(
			WorkLineAction.Pick,
			shelf,
			shelf,
			request.ItemId,
			quantity,
			request.OrderLine,
			consumeSourcePickReservation: false);
		return WorkPlanResult.Issued;
	}

	private WorkPlanResult OnManualCollectCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		if (worker == null ||
			manualSessions.TryGetValue(worker, out ManualPickingSession session) == false ||
			session.Request == null)
		{
			return box != null && box.Stacks.Count > 0
				? WorkPlanResult.SwitchPhase
				: WorkPlanResult.Completed;
		}

		if (result.Moved > 0)
		{
			int applied = session.Request.ReportAllocated(result.Moved);
			if (applied != result.Moved)
				Debug.LogWarning($"[PickingPlanner] Manual pick request mismatch. moved={result.Moved}, applied={applied}");

			ReportCollected(worker, line, result.Moved);
		}

		if (session.Request.IsComplete)
		{
			requestSource.Remove(session.Request);
			ReleaseManualSession(worker, keepRequest: false);
			return box != null && box.Stacks.Count > 0
				? WorkPlanResult.SwitchPhase
				: WorkPlanResult.Completed;
		}

		if (HasReachedBoxFillLimit(box))
		{
			ReleaseManualSession(worker, keepRequest: true);
			return WorkPlanResult.SwitchPhase;
		}

		if (FindNextManualShelf(worker, session) != null)
			return WorkPlanResult.Issued;

		return FinishManualScan(worker, box, session);
	}

	private ShelfBase FindNextManualShelf(AIWorker worker, ManualPickingSession session)
	{
		if (worker == null || session == null || ownerBuilding?.ItemIndex == null)
			return null;

		ShelfBase bestShelf = null;
		int bestDistance = int.MaxValue;
		foreach (IItemContainer container in ownerBuilding.ItemIndex.Containers)
		{
			if (container is not ShelfBase shelf || session.VisitedShelves.Contains(shelf))
				continue;

			if (InteractionPointSelector.TryGetInteractionPoint(
				shelf,
				InteractionKind.Pick,
				worker.GridPosition,
				out _,
				out int distance) == false ||
				distance >= bestDistance)
			{
				continue;
			}

			bestShelf = shelf;
			bestDistance = distance;
		}

		return bestShelf;
	}

	private WorkPlanResult FinishManualScan(AIWorker worker, BoxBase box, ManualPickingSession session)
	{
		PickingRequest request = session?.Request;
		if (request != null)
		{
			int remaining = request.RemainingQuantity;
			OrderManager orderManager = GameContext.HasInstance ? GameContext.Instance.OrderMgr : null;
			int released = orderManager != null
				? orderManager.ReleasePickingAllocation(request.OrderLine, remaining)
				: 0;
			if (orderManager != null && released != remaining)
				Debug.LogWarning($"[PickingPlanner] Manual scan allocation rollback mismatch. requested={remaining}, released={released}");

			request.ReleaseReserved(remaining);
			requestSource.Remove(request);
		}

		ReleaseManualSession(worker, keepRequest: false);
		return box != null && box.Stacks.Count > 0
			? WorkPlanResult.SwitchPhase
			: WorkPlanResult.Completed;
	}

	private bool ReleaseManualSession(AIWorker worker, bool keepRequest)
	{
		if (worker == null || manualSessions.Remove(worker, out ManualPickingSession session) == false)
			return false;

		if (session?.Request != null)
			claimedManualRequests.Remove(session.Request);

		if (keepRequest && session?.Request?.GetAllocatableQuantity() > 0)
			MarkPickingDirty();

		return true;
	}

	private bool HasPendingLocatedRequest()
	{
		foreach (PickingRequest request in requestSource.GetRequests())
		{
			if (request?.Source != null && request.GetAllocatableQuantity() > 0)
				return true;
		}

		return false;
	}

	private bool CanUseInventoryGuidance()
	{
		return pickingPolicyType == PickingPolicyType.InventoryGuided &&
			GameContext.HasInstance &&
			GameContext.Instance.ResearchService?.IsResearched(InventoryDigitizationResearchId) == true;
	}

	private static void ReportCollected(AIWorker worker, WorkLine line, int moved)
	{
		if (line == null || moved <= 0)
			return;

		OrderManager orderManager = GameContext.HasInstance ? GameContext.Instance.OrderMgr : null;
		int pickedQuantity = orderManager != null
			? orderManager.ReportPickingCompleted(line.RelatedOrderLine, moved)
			: 0;
		if (pickedQuantity != moved)
			Debug.LogWarning($"[PickingPlanner] Pick progress mismatch. requested={moved}, applied={pickedQuantity}");

		OutboundWorkflowService outboundWorkflowService = GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null;
		outboundWorkflowService?.AddPickedToManifest(
			worker?.CarryingAbility?.CarryingBox,
			line.RelatedOrderLine,
			line.ItemID,
			moved);
	}

	private void MarkPickingDirty()
	{
		if (BuildingId != 0 && GameContext.HasInstance)
			GameContext.Instance.ItemTransferTaskScheduler?.MarkDirty(BuildingId, ItemTransferScheduleMode.Picking);
	}

	private bool HasReachedBoxFillLimit(BoxBase box)
	{
		if (box == null || box.MaxSize <= 0.0f)
			return false;

		float filledPercent = (box.TotalSize / box.MaxSize) * 100.0f;
		return filledPercent >= boxFillLimitPercent;
	}

	private bool TryAllocateNextCollect(AIWorker worker, uint buildingId, out WorkLine line)
	{
		line = null;
		if (worker == null)
			return false;

		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (box == null)
			return false;

		List<CollectCandidate<PickingRequest>> candidates = GetCollectableTargets(box, buildingId);
		while (candidates.Count > 0)
		{
			if ((requestCollectingPolicy ?? new NearestCollectingPolicy<PickingRequest>())
				.TryDecide(worker.GridPosition, candidates, out var decision) == false)
			{
				return false;
			}

			if (decision.Source == null || decision.Quantity <= 0)
			{
				RemoveDispatchedCandidate(candidates, decision);
				continue;
			}

			int actualAllocated = requestSource.Allocate(decision.RequestLine, decision.Quantity);
			if (actualAllocated <= 0)
			{
				RemoveDispatchedCandidate(candidates, decision);
				continue;
			}

			if (actualAllocated != decision.Quantity)
			{
				int extraReservation = decision.Quantity - actualAllocated;
				if (extraReservation > 0)
				{
					decision.Source.ReleaseReservedPick(decision.ItemId, extraReservation);
					decision.RequestLine.ReleaseReserved(extraReservation);
				}

				Debug.LogWarning($"[PickingPlanner] Reservation/allocation mismatch for item {decision.ItemId}. reserved={decision.Quantity}, allocated={actualAllocated}");
			}

			line = requestSource.CreateWorkLine(decision.Source, decision.ItemId, actualAllocated, decision.RequestLine);
			return line != null;
		}

		return false;
	}

	private List<CollectCandidate<PickingRequest>> GetCollectableTargets(BoxBase box, uint buildingId)
	{
		List<CollectCandidate<PickingRequest>> candidates = new();
		foreach (PickingRequest request in requestSource.GetRequests())
		{
			if (request == null)
				continue;

			int allocatable = request.GetAllocatableQuantity();
			if (allocatable <= 0)
				continue;

			int acceptable = box.GetAcceptableQuantity(request.ItemId, allocatable);
			if (acceptable <= 0)
				continue;

			if (request.Source == null)
				continue;

			int quantity = Mathf.Min(acceptable, allocatable);
			if (quantity <= 0)
				continue;

			candidates.Add(new CollectCandidate<PickingRequest>(request.Source, request.ItemId, quantity, request));
		}

		return candidates;
	}

	private uint ResolveBuildingId(uint buildingId)
	{
		return BuildingId != 0 ? BuildingId : buildingId;
	}

	private static void RemoveDispatchedCandidate(
		List<CollectCandidate<PickingRequest>> candidates,
		CollectCandidate<PickingRequest> decision)
	{
		for (int i = 0; i < candidates.Count; ++i)
		{
			CollectCandidate<PickingRequest> candidate = candidates[i];
			if (candidate.RequestLine == decision.RequestLine &&
				candidate.Source == decision.Source &&
				candidate.ItemId == decision.ItemId &&
				candidate.Quantity == decision.Quantity)
			{
				candidates.RemoveAt(i);
				return;
			}
		}
	}

	private IEnumerable<CapsuleBuffer> EnumeratePlaceBuffers(uint buildingId)
	{
		if (CapsuleBufferService == null)
			yield break;

		foreach (CapsuleBuffer buffer in CapsuleBufferService.GetBuffers(buildingId))
			{
				if (buffer != null && buffer.CanReceiveOutboundItems())
					yield return buffer;
			}
	}

	private static void ReleaseUnmovedReservation(WorkLine line, ItemTransferResult result)
	{
		if (line == null ||
			line.ConsumeSourcePickReservation == false ||
			line.Container is not IItemPickReservable reservable)
			return;

		int remainingReservation = Mathf.Max(0, line.Quantity - result.Moved);
		if (remainingReservation > 0)
			reservable.ReleaseReservedPick(line.ItemID, remainingReservation);
	}

	private static void TransferPickingManifest(BoxBase sourceBox, WorkLine placeLine, int moved)
	{
		if (sourceBox == null ||
			placeLine?.Target is not CapsuleBuffer targetBuffer ||
			targetBuffer.DockedCapsule == null ||
			GameContext.HasInstance == false ||
			moved <= 0)
		{
			return;
		}

		int manifestMoved = GameContext.Instance.OBWorkflowSvc != null
			? GameContext.Instance.OBWorkflowSvc.TransferPickingManifest(
				sourceBox,
				targetBuffer.DockedCapsule,
				placeLine.RelatedOrderLine,
				placeLine.ItemID,
				moved)
			: 0;
		if (manifestMoved != moved)
			Debug.LogWarning($"[PickingPlanner] Picking manifest place mismatch. item={placeLine.ItemID}, moved={moved}, manifestMoved={manifestMoved}");
	}

	private sealed class ManualPickingSession
	{
		public readonly PickingRequest Request;
		public readonly HashSet<ShelfBase> VisitedShelves = new();

		public ManualPickingSession(PickingRequest request)
		{
			Request = request;
		}
	}

	private sealed class PickingRequestSource
	{
		private readonly List<PickingRequest> requests = new();
		private readonly Dictionary<uint, List<PickingRequest>> requestsByItem = new();

		public PickingRequest Add(OrderLine orderLine, ShelfBase source, int reservedQuantity)
		{
			PickingRequest request = new(orderLine, source, reservedQuantity);
			if (request.RequestedQuantity <= 0)
				return null;

			requests.Add(request);
			if (requestsByItem.TryGetValue(request.ItemId, out List<PickingRequest> itemRequests) == false)
			{
				itemRequests = new List<PickingRequest>();
				requestsByItem[request.ItemId] = itemRequests;
			}

			itemRequests.Add(request);
			return request;
		}

		public bool HasAny()
		{
			for (int i = 0; i < requests.Count; ++i)
			{
				PickingRequest request = requests[i];
				if (request != null && request.GetAllocatableQuantity() > 0)
					return true;
			}

			return false;
		}

		public IEnumerable<PickingRequest> GetRequests()
		{
			for (int i = 0; i < requests.Count; ++i)
			{
				PickingRequest request = requests[i];
				if (request != null && request.GetAllocatableQuantity() > 0)
					yield return request;
			}
		}

		public IReadOnlyList<PickingRequest> GetAllRequests()
		{
			return requests;
		}

		public bool Remove(PickingRequest request)
		{
			if (request == null || requests.Remove(request) == false)
				return false;

			if (requestsByItem.TryGetValue(request.ItemId, out List<PickingRequest> itemRequests))
			{
				itemRequests.Remove(request);
				if (itemRequests.Count == 0)
					requestsByItem.Remove(request.ItemId);
			}

			return true;
		}

		public int GetAllocatableQuantity(PickingRequest requestLine)
		{
			return requestLine != null ? requestLine.GetAllocatableQuantity() : 0;
		}

		public int Allocate(PickingRequest requestLine, int quantity)
		{
			if (requestLine == null || quantity <= 0)
				return 0;

			int requested = Mathf.Min(quantity, requestLine.GetAllocatableQuantity());
			return requestLine.ReportAllocated(requested);
		}

		public int ReleaseAllocated(OrderLine orderLine, ShelfBase source, uint itemId, int quantity)
		{
			if (orderLine == null || source == null || quantity <= 0)
				return 0;

			int remaining = quantity;
			for (int i = 0; i < requests.Count && remaining > 0; ++i)
			{
				PickingRequest request = requests[i];
				if (request == null ||
					request.OrderLine != orderLine ||
					request.Source != source ||
					request.ItemId != itemId)
				{
					continue;
				}

				remaining -= request.ReleaseAllocated(remaining);
			}

			return quantity - remaining;
		}

		public WorkLine CreateWorkLine(ShelfBase source, uint itemId, int quantity, PickingRequest requestLine)
		{
			ShelfBase requestSource = requestLine?.Source;
			ShelfBase resolvedSource = requestSource != null ? requestSource : source;
			return resolvedSource == null || requestLine?.OrderLine == null
				? null
				: new WorkLine(resolvedSource, itemId, quantity, requestLine.OrderLine);
		}

	}
}
