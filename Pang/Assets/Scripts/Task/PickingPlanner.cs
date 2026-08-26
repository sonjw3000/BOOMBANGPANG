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
	private static int jobID = 1;

	private readonly uint buildingId;
	private readonly PickingRequestSource requestSource = new();
	private readonly Dictionary<AIWorker, ManualPickingSession> manualSessions = new();
	private readonly HashSet<PickingRequest> claimedManualRequests = new();
	private bool cancelAllRequestsPending;
	private PickingPolicyType pickingPolicyType;
	private ICollectingPolicy<PickingRequest> requestCollectingPolicy;
	private CollectingPolicyType collectingPolicyType;
	private float boxFillLimitPercent;

	public static int GetNextJobId() => jobID;
	public static void SetNextJobId(int nextJobId) => jobID = nextJobId;
	public PickingPolicyType PickingPolicyType => pickingPolicyType;
	public CollectingPolicyType CollectingPolicyType => collectingPolicyType;
	public uint BuildingId => buildingId;

	private CapsuleBufferService CapsuleBufferService => GameContext.Instance.CapsuleBufferSvc;

	public PickingPlanner(
		uint buildingId,
		float boxFillLimitPercent,
		CollectingPolicyType collectingPolicyType = CollectingPolicyType.Nearest,
		PickingPolicyType pickingPolicyType = PickingPolicyType.ManualShelfScan)
	{
		this.buildingId = buildingId;
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

	public void GetPendingDemand(out int sourceCount, out int itemQuantity)
	{
		sourceCount = 0;
		itemQuantity = 0;

		foreach (PickingRequest request in requestSource.GetRequests())
		{
			int quantity = request != null ? request.GetAllocatableQuantity() : 0;
			if (quantity <= 0 ||
				(request.Source == null && claimedManualRequests.Contains(request)))
			{
				continue;
			}

			++sourceCount;
			itemQuantity += quantity;
		}
	}

	public int AcceptPickingRequest(OrderLine orderLine, int quantity, out PickingRequest firstRequest)
	{
		return CanUseInventoryGuidance()
			? AcceptLocatedPickingRequest(orderLine, quantity, out firstRequest)
			: AcceptManualPickingRequest(orderLine, quantity, out firstRequest);
	}

	public int GetPickableQuantity(uint itemId)
	{
		if (BuildingId == 0 || itemId == 0 || GameContext.HasInstance == false)
			return 0;

		ShelfStorageService storageService = GameContext.Instance.StorageService;
		if (storageService == null)
			return 0;

		int quantity = 0;
		foreach (ShelfBase source in storageService.GetSources(BuildingId, itemId))
		{
			if (source != null)
				quantity += GetLabeledPickableQuantity(source, itemId);
		}

		return quantity;
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
		ReleaseUnmovedPickingAllocation(line, result);

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

		if (cancelAllRequestsPending)
			CancelAllRequests();

		if (HasPendingCollect(BuildingId))
			GameContext.Instance.ItemTransferTaskScheduler?.MarkDirty(BuildingId, ItemTransferScheduleMode.Picking);
	}

	public int CancelAllRequests()
	{
		cancelAllRequestsPending = true;
		OrderManager orderManager = GameContext.HasInstance ? GameContext.Instance.OrderMgr : null;
		List<PickingRequest> requests = new(requestSource.GetAllRequests());
		int cancelledQuantity = 0;
		bool hasAllocatedRequest = false;

		for (int i = 0; i < requests.Count; ++i)
		{
			PickingRequest request = requests[i];
			if (request == null)
				continue;

			int releasable = request.RemainingQuantity;
			if (releasable > 0)
			{
				int releasedReservation = request.Source != null
					? request.Source.ReleaseReservedPick(request.ItemId, releasable)
					: releasable;
				int releasedAllocation = orderManager != null
					? orderManager.ReleasePickingAllocation(request.OrderLine, releasedReservation)
					: 0;
				if (orderManager != null && releasedAllocation != releasedReservation)
				{
					Debug.LogWarning($"[PickingPlanner] Building unregister allocation rollback mismatch. requested={releasedReservation}, released={releasedAllocation}");
				}

				request.ReleaseReserved(releasedReservation);
				cancelledQuantity += releasedReservation;
			}

			if (request.AllocatedQuantity > 0)
			{
				hasAllocatedRequest = true;
				continue;
			}

			claimedManualRequests.Remove(request);
			requestSource.Remove(request);
		}

		cancelAllRequestsPending = hasAllocatedRequest;
		return cancelledQuantity;
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
		outboundWorkflowService.TryGetPickingManifest(box, out PickingManifest manifest);
		FacilityFilter filter = FacilityFilter.WithContentState(
			FacilityFilter.WithItemProcessStage(
				FacilityFilter.ForManifestTransfer(
				box,
				manifest,
				pickedLine.ItemID,
				remainingQuantity,
				stack => pickedLine.RequiredStatus.HasValue == false || stack.HasStatus(pickedLine.RequiredStatus.Value),
				worker),
				ItemProcessStage.Picked),
			FacilityContentState.HasItems);

		ItemTransferTask activeTask = worker.CurrentTask as ItemTransferTask;
		Building targetBuilding = null;
		GameContext.Instance.BuildingMgr?.TryGetBuilding(targetBuildingId, out targetBuilding);
		if (activeTask != null &&
			activeTask.Type == WorkerTask.TaskType.Picking &&
			activeTask.BuildingId == targetBuildingId)
		{
			IReadOnlyList<CapsuleBuffer> retainedOutputs = activeTask.RetainedCapsuleOutputBuffers;
			for (int i = 0; i < retainedOutputs.Count; ++i)
			{
				CapsuleBuffer buffer = retainedOutputs[i];
				if (IsRetainedPickingOutputBufferWithBuilding(activeTask, buffer, targetBuildingId, targetBuilding, filter) == false)
					continue;

				TrySelectPlaceBuffer(
					worker,
					box,
					pickedLine.ItemID,
					remainingQuantity,
					buffer,
					ref bestBuffer,
					ref bestDistance);
			}
		}

		if (bestBuffer == null)
		{
			foreach (CapsuleBuffer buffer in EnumeratePlaceBuffers(targetBuildingId))
			{
				if (buffer == null ||
					IsPickingOutputBufferCandidate(activeTask, buffer, targetBuildingId, targetBuilding, filter) == false)
					continue;

				TrySelectPlaceBuffer(
					worker,
					box,
					pickedLine.ItemID,
					remainingQuantity,
					buffer,
					ref bestBuffer,
					ref bestDistance);
			}
		}

		if (bestBuffer == null)
			return WorkPlanResult.Waiting;

		line = new WorkLine(WorkLineAction.Put, bestBuffer, bestBuffer, pickedLine.ItemID, remainingQuantity, pickedLine.RelatedOrderLine);
		return WorkPlanResult.Issued;
	}

	public WorkPlanResult OnPlaceCompleted(AIWorker worker, WorkLine collectedLine, WorkLine placeLine, ItemTransferResult result)
	{
		ItemTransferTask activeTask = worker?.CurrentTask as ItemTransferTask;
		CapsuleBuffer targetBuffer = placeLine?.Target as CapsuleBuffer;
		if (result.Kind == TransferResultKind.None)
		{
			activeTask?.ReleaseRetainedCapsuleOutput(targetBuffer);
			return WorkPlanResult.Waiting;
		}

		TransferPickingManifest(worker?.CarryingAbility?.CarryingBox, placeLine, result.Moved);
		if (activeTask != null &&
			targetBuffer != null &&
			(result.Kind == TransferResultKind.Partial || IsOutboundThresholdReached(targetBuffer)))
		{
			activeTask.ReleaseRetainedCapsuleOutput(targetBuffer);
		}

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

			int available = GetLabeledPickableQuantity(source, orderLine.ItemID);
			int reserved = source.ReservePicking(orderLine.ItemID, Mathf.Min(remaining, available));
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

		int quantity = GetAcceptableQuantityWithinFillLimit(box, request.ItemId, request.GetAllocatableQuantity());
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
			Mathf.Min(quantity, GetLabeledQuantity(shelf, request.ItemId)),
			request.OrderLine,
			requiredStatus: ItemStatus.Labeled,
			consumeSourcePickReservation: false,
			excludedQuality: ItemQuality.Waste);
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
			return FinishManualScan(worker, box, session);

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
		ShelfStorageService storageService = GameContext.HasInstance ? GameContext.Instance.StorageService : null;
		if (worker == null || session?.Request == null || storageService == null || BuildingId == 0)
			return null;

		ShelfBase bestShelf = null;
		int bestDistance = int.MaxValue;
		foreach (ShelfBase shelf in storageService.GetSources(BuildingId, session.Request.ItemId))
		{
			if (shelf == null || session.VisitedShelves.Contains(shelf))
				continue;
			if (GetLabeledQuantity(shelf, session.Request.ItemId) <= 0)
				continue;

			if (InteractionPointSelector.TryGetInteractionPointInBuilding(
				shelf,
				InteractionKind.Pick,
				worker.GridPosition,
				worker.PrimaryBuildingId,
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

		if (HasReachedBoxFillLimit(box))
			return WorkPlanResult.SwitchPhase;

		if (TryClaimManualRequest(worker))
			return WorkPlanResult.Issued;

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
			GameContext.Instance.ResearchService?.IsResearched(ResearchIds.InventoryDigitization) == true;
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

	private void ReleaseUnmovedPickingAllocation(WorkLine line, ItemTransferResult result)
	{
		if (line == null ||
			line.ConsumeSourcePickReservation == false ||
			line.RelatedOrderLine == null)
		{
			return;
		}

		int unmoved = Mathf.Max(0, line.Quantity - result.Moved);
		if (unmoved <= 0)
			return;

		int released = requestSource.ReleaseFailedAllocation(
			line.RelatedOrderLine,
			line.Container as ShelfBase,
			line.ItemID,
			unmoved);
		OrderManager orderManager = GameContext.HasInstance ? GameContext.Instance.OrderMgr : null;
		int releasedOrderAllocation = orderManager != null
			? orderManager.ReleasePickingAllocation(line.RelatedOrderLine, released)
			: 0;
		if (released != unmoved ||
			(orderManager != null && releasedOrderAllocation != released))
		{
			Debug.LogWarning(
				$"[PickingPlanner] Failed collect rollback mismatch. requested={unmoved}, request={released}, order={releasedOrderAllocation}");
		}
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
				.TryDecide(worker.GridPosition, worker.PrimaryBuildingId, candidates, out var decision) == false)
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

			int acceptable = GetAcceptableQuantityWithinFillLimit(box, request.ItemId, allocatable);
			if (acceptable <= 0)
				continue;

			if (request.Source == null)
				continue;

			int quantity = Mathf.Min(
				Mathf.Min(acceptable, allocatable),
				GetLabeledQuantity(request.Source, request.ItemId));
			if (quantity <= 0)
				continue;

			candidates.Add(new CollectCandidate<PickingRequest>(request.Source, request.ItemId, quantity, request));
		}

		return candidates;
	}

	private int GetAcceptableQuantityWithinFillLimit(BoxBase box, uint itemId, int requested)
	{
		if (box == null || requested <= 0)
			return 0;

		int acceptable = box.GetAcceptableQuantity(itemId, requested);
		if (acceptable <= 0 || box.MaxSize <= 0.0f)
			return acceptable;

		ItemDatabase itemDatabase = GameContext.HasInstance ? GameContext.Instance.ItemDB : null;
		float itemSize = itemDatabase != null ? itemDatabase.GetItemSize(itemId) : 0.0f;
		if (itemSize <= 0.0f)
			return 0;

		float fillLimit = box.MaxSize * Mathf.Clamp(boxFillLimitPercent, 1.0f, 100.0f) / 100.0f;
		float remainingSize = fillLimit - box.TotalSize;
		if (remainingSize <= 0.0f)
			return 0;

		int fillLimitAcceptable = Mathf.FloorToInt((remainingSize / itemSize) + 0.0001f);
		return Mathf.Min(acceptable, fillLimitAcceptable);
	}

	private uint ResolveBuildingId(uint buildingId)
	{
		return BuildingId != 0 ? BuildingId : buildingId;
	}

	private static int GetLabeledPickableQuantity(ShelfBase source, uint itemId)
	{
		if (source == null || itemId == 0)
			return 0;

		int reserved = source.ItemToBePicked.TryGetValue(itemId, out int value) ? value : 0;
		return Mathf.Max(0, GetLabeledQuantity(source, itemId) - reserved);
	}

	private static int GetLabeledQuantity(IItemContainer container, uint itemId)
	{
		if (container == null || itemId == 0)
			return 0;

		int quantity = 0;
		for (int i = 0; i < container.Stacks.Count; ++i)
		{
			ItemStack stack = container.Stacks[i];
			if (stack != null &&
				stack.ItemID == itemId &&
				stack.Quantity > 0 &&
				stack.HasStatus(ItemStatus.Labeled) &&
				stack.HasQuality(ItemQuality.Waste) == false)
			{
				quantity += stack.Quantity;
			}
		}

		return quantity;
	}

	private static bool IsProjectedInputRuleMatchedBuffer(CapsuleBuffer buffer, FacilityFilter projectedInputFilter)
	{
		return IsPickingOutputBufferCandidate(
			task: null,
			buffer: buffer,
			buildingId: 0,
			building: null,
			projectedInputFilter: projectedInputFilter,
			requireEmpty: true);
	}

	private static bool IsPickingOutputBufferCandidate(
		ItemTransferTask task,
		CapsuleBuffer buffer,
		uint buildingId,
		Building building,
		FacilityFilter projectedInputFilter,
		bool requireEmpty = false)
	{
		if (buffer?.DockedCapsule is not CargoCapsule capsule ||
			GameContext.HasInstance == false ||
			capsule.RouteKind != CargoRouteKind.Standard ||
			projectedInputFilter.ItemProcessStage != ItemProcessStage.Picked ||
			projectedInputFilter.ContentState != FacilityContentState.HasItems)
		{
			return false;
		}

		bool isEmptyInput =
			capsule.LogisticsState == CapsuleLogisticsState.Empty &&
			buffer.IsCapsuleEmpty();
		bool isSharedPickedInput =
			requireEmpty == false &&
			capsule.LogisticsState == CapsuleLogisticsState.Inside &&
			buffer.IsCapsuleEmpty() == false;
		if (isEmptyInput == false && isSharedPickedInput == false)
			return false;

		CapsuleBufferService bufferService = GameContext.Instance.CapsuleBufferSvc;
		if (bufferService == null ||
			bufferService.IsExplicitRuleMatchedBuffer(
				buffer,
				projectedInputFilter,
				FacilityContentState.HasItems,
				ItemProcessStage.Picked) == false)
		{
			return false;
		}

		if (isSharedPickedInput &&
			bufferService.IsRuleMatchedBuffer(buffer, capsule, evaluateLaunchReadiness: false) == false)
		{
			return false;
		}

		if (buildingId != 0 &&
			(bufferService.TryGetRegisteredBuildingId(buffer, out uint ownerBuildingId) == false ||
			 ownerBuildingId != buildingId))
		{
			return false;
		}

		TaskManager taskManager = GameContext.Instance.TaskMgr;
		if (taskManager?.HasConflictingCapsuleContentDependency(buffer, WorkLineAction.Put) == true)
			return false;

		if (building != null &&
			building.OutboundTargetStage == ItemProcessStage.Picked &&
			building.CanDispatchOutboundBuffer(buffer))
		{
			return false;
		}

		return IsAvailableForPickingOutput(buffer);
	}

	private static bool IsRetainedPickingOutputBuffer(
		ItemTransferTask task,
		CapsuleBuffer buffer,
		uint buildingId,
		FacilityFilter projectedInputFilter)
	{
		Building building = null;
		if (GameContext.HasInstance)
			GameContext.Instance.BuildingMgr?.TryGetBuilding(buildingId, out building);

		return IsRetainedPickingOutputBufferWithBuilding(task, buffer, buildingId, building, projectedInputFilter);
	}

	private static bool IsRetainedPickingOutputBufferWithBuilding(
		ItemTransferTask task,
		CapsuleBuffer buffer,
		uint buildingId,
		Building building,
		FacilityFilter projectedInputFilter)
	{
		if (task == null ||
			task.RetainsCapsuleOutput(buffer) == false)
		{
			return false;
		}

		return IsPickingOutputBufferCandidate(task, buffer, buildingId, building, projectedInputFilter);
	}

	private static bool IsAvailableForPickingOutput(CapsuleBuffer buffer)
	{
		if (buffer == null || GameContext.HasInstance == false)
			return false;

		FacilityManager facilityManager = GameContext.Instance.FacilityMgr;
		if (facilityManager?.IsInvalidating(buffer) == true)
			return false;

		CapsuleRelocateCoordinator coordinator = GameContext.Instance.ExistingCapsuleRelocateCoordinator;
		return coordinator == null ||
			(coordinator.IsPlayerClaimed(buffer) == false &&
			 coordinator.IsReserved(buffer) == false &&
			 coordinator.IsRelocationSourceActive(buffer) == false &&
			 coordinator.IsRelocationTargetActive(buffer) == false);
	}

	private static void TrySelectPlaceBuffer(
		AIWorker worker,
		BoxBase source,
		uint itemId,
		int quantity,
		CapsuleBuffer candidate,
		ref CapsuleBuffer bestBuffer,
		ref int bestDistance)
	{
		if (worker == null ||
			candidate == null ||
			ItemTransferUtility.GetMovableQuantity(source, candidate, itemId, quantity) <= 0 ||
			InteractionPointSelector.TryGetInteractionPointInBuilding(
				candidate,
				InteractionKind.Put,
				worker.GridPosition,
				worker.PrimaryBuildingId,
				out _,
				out int distance) == false ||
			distance >= bestDistance)
		{
			return;
		}

		bestBuffer = candidate;
		bestDistance = distance;
	}

	private static bool IsOutboundThresholdReached(CapsuleBuffer buffer)
	{
		if (buffer == null || GameContext.HasInstance == false)
			return false;

		return GameContext.Instance.FacilityMgr?.TryGetBuildingId(buffer, out uint buildingId) == true &&
			GameContext.Instance.BuildingMgr?.TryGetBuilding(buildingId, out Building building) == true &&
			building.CanDispatchOutboundBuffer(buffer);
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

		public int ReleaseFailedAllocation(OrderLine orderLine, ShelfBase source, uint itemId, int quantity)
		{
			int remaining = Mathf.Max(0, quantity);
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

				int released = request.ReleaseAllocated(remaining);
				int releasedReservation = request.ReleaseReserved(released);
				if (releasedReservation != released)
					Debug.LogWarning($"[PickingPlanner] Request reservation rollback mismatch. allocation={released}, reservation={releasedReservation}");

				remaining -= releasedReservation;
			}

			return quantity - remaining;
		}

		public WorkLine CreateWorkLine(ShelfBase source, uint itemId, int quantity, PickingRequest requestLine)
		{
			ShelfBase requestSource = requestLine?.Source;
			ShelfBase resolvedSource = requestSource != null ? requestSource : source;
			return resolvedSource == null || requestLine?.OrderLine == null
				? null
				: new WorkLine(
					WorkLineAction.Pick,
					resolvedSource,
					resolvedSource,
					itemId,
					quantity,
					requestLine.OrderLine,
					requiredStatus: ItemStatus.Labeled,
					excludedQuality: ItemQuality.Waste);
		}

	}
}
