using UnityEngine;

public sealed class PackingOutputPlanner : IItemTransferPlanner, IItemTransferTaskInvalidationHandler
{
	private readonly uint buildingId;

	public uint BuildingId => buildingId;

	private PackingStationService StationService =>
		GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc?.PackingStationService : null;
	private CapsuleBufferService BufferService =>
		GameContext.HasInstance ? GameContext.Instance.CapsuleBufferSvc : null;

	public PackingOutputPlanner(uint buildingId)
	{
		this.buildingId = buildingId;
	}

	public bool HasAvailableWork()
	{
		return buildingId != 0 && StationService?.HasCompletedOutput(buildingId) == true;
	}

	public WorkPlanResult TryGetCollectLine(AIWorker worker, uint requestedBuildingId, out WorkLine line)
	{
		line = null;
		if (requestedBuildingId != buildingId || StationService == null)
			return WorkPlanResult.Completed;

		while (StationService.TryClaimCompletedOutput(buildingId, out PackingStation station))
		{
			if (station?.EndPackingBox == null)
				continue;

			line = new WorkLine(WorkLineAction.Pick, station, station, 0, 1);
			return WorkPlanResult.Issued;
		}

		return WorkPlanResult.Completed;
	}

	public WorkPlanResult OnCollectCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		if (result.Moved <= 0)
		{
			if (line?.Target is PackingStation sourceStation && sourceStation.EndPackingBox != null)
				StationService?.ReturnCompletedOutput(buildingId, sourceStation);

			EvaluateWork();
			return WorkPlanResult.Completed;
		}

		EvaluateWork();
		return WorkPlanResult.SwitchPhase;
	}

	public WorkPlanResult TryGetPlaceLine(
		AIWorker worker,
		uint requestedBuildingId,
		WorkLine collectedLine,
		int remainingQuantity,
		out WorkLine line)
	{
		line = null;
		BoxBase sourceBox = worker?.CarryingAbility?.CarryingBox;
		if (worker == null ||
			sourceBox == null ||
			requestedBuildingId != buildingId ||
			BufferService == null)
		{
			return WorkPlanResult.Completed;
		}

		OutboundWorkflowService outbound = GameContext.Instance.OBWorkflowSvc;
		PickingManifest manifest = null;
		if (outbound != null)
			outbound.TryGetPickingManifest(sourceBox, out manifest);
		bool hasPackedPayload = false;
		ItemTransferTask activeTask = worker.CurrentTask as ItemTransferTask;
		Building targetBuilding = null;
		GameContext.Instance.BuildingMgr?.TryGetBuilding(buildingId, out targetBuilding);
		CapsuleBuffer bestBuffer = null;
		ItemStack bestStack = null;
		int bestMovable = 0;
		bool bestRetained = false;
		int bestPriority = int.MinValue;
		int bestDistance = int.MaxValue;
		for (int i = 0; i < sourceBox.Stacks.Count; ++i)
		{
			ItemStack stack = sourceBox.Stacks[i];
			if (stack == null ||
				stack.Quantity <= 0 ||
				stack.HasStatus(ItemStatus.Packed) == false ||
				stack.HasQuality(ItemQuality.Waste))
			{
				continue;
			}

			hasPackedPayload = true;
			FacilityFilter projectedInputFilter = FacilityFilter.WithContentState(
				FacilityFilter.WithItemProcessStage(
					FacilityFilter.ForManifestTransfer(
					sourceBox,
					manifest,
					stack.ItemID,
					stack.Quantity,
					candidate => candidate.HasStatus(ItemStatus.Packed) &&
						candidate.HasQuality(ItemQuality.Waste) == false,
					worker),
					ItemProcessStage.Packed),
				FacilityContentState.HasItems);

			foreach (CapsuleBuffer candidate in BufferService.GetBuffers(buildingId))
			{
				if (IsCapsuleOutputCandidate(
					activeTask,
					candidate,
					targetBuilding,
					projectedInputFilter) == false)
					continue;

				int movable = ItemTransferUtility.GetMovableQuantity(
					sourceBox,
					candidate,
					stack.ItemID,
					stack.Quantity,
					item => item.HasStatus(ItemStatus.Packed) &&
						item.HasQuality(ItemQuality.Waste) == false);
				if (movable <= 0 ||
					InteractionPointSelector.TryGetInteractionPointInBuilding(
						candidate,
						InteractionKind.Put,
						worker.GridPosition,
						buildingId,
						out _,
						out int distance) == false)
				{
					continue;
				}

				bool retained = activeTask?.RetainsCapsuleOutput(candidate) == true;
				int priority = GetRulePriority(candidate);
				bool isBetter = bestBuffer == null ||
					(retained && bestRetained == false) ||
					(retained == bestRetained && priority > bestPriority) ||
					(retained == bestRetained && priority == bestPriority && movable > bestMovable) ||
					(retained == bestRetained && priority == bestPriority && movable == bestMovable && distance < bestDistance);
				if (isBetter == false)
					continue;

				bestBuffer = candidate;
				bestStack = stack;
				bestMovable = movable;
				bestRetained = retained;
				bestPriority = priority;
				bestDistance = distance;
			}
		}

		if (bestBuffer == null || bestStack == null || bestMovable <= 0)
			return hasPackedPayload ? WorkPlanResult.Waiting : WorkPlanResult.Completed;

		line = new WorkLine(
			WorkLineAction.Put,
			bestBuffer,
			bestBuffer,
			bestStack.ItemID,
			bestMovable,
			null,
			ItemStatus.Packed,
			excludedQuality: ItemQuality.Waste);
		return WorkPlanResult.Issued;
	}

	public WorkPlanResult OnPlaceCompleted(
		AIWorker worker,
		WorkLine collectedLine,
		WorkLine placeLine,
		ItemTransferResult result)
	{
		ItemTransferTask activeTask = worker?.CurrentTask as ItemTransferTask;
		CapsuleBuffer targetBuffer = placeLine?.Target as CapsuleBuffer;
		if (result.Moved <= 0)
		{
			activeTask?.ReleaseRetainedCapsuleOutput(targetBuffer);
			return WorkPlanResult.Waiting;
		}

		TransferPackedManifest(worker?.CarryingAbility?.CarryingBox, placeLine);
		if (activeTask != null &&
			targetBuffer != null &&
			(result.Kind == TransferResultKind.Partial || IsOutboundThresholdReached(targetBuffer)))
		{
			activeTask.ReleaseRetainedCapsuleOutput(targetBuffer);
		}

		EvaluateWork();
		return IsWorkerCarryBoxEmpty(worker)
			? WorkPlanResult.Completed
			: WorkPlanResult.Issued;
	}

	public void OnTaskInvalidated(ItemTransferTask task)
	{
		if (task?.Phase == ItemTransferPhase.Collect &&
			task.CurrentLine?.Target is PackingStation station &&
			station.EndPackingBox != null)
		{
			FacilityManager facilityManager = GameContext.HasInstance
				? GameContext.Instance.FacilityMgr
				: null;
			if (facilityManager == null || facilityManager.IsInvalidating(station) == false)
				StationService?.ReturnCompletedOutput(buildingId, station);
		}

		EvaluateWork();
	}

	private bool IsProjectedInputRuleMatchedBuffer(
		CapsuleBuffer buffer,
		FacilityFilter projectedInputFilter)
	{
		return IsCapsuleOutputCandidate(
			task: null,
			buffer: buffer,
			building: null,
			projectedInputFilter: projectedInputFilter,
			requireEmpty: true);
	}

	private bool IsCapsuleOutputCandidate(
		ItemTransferTask task,
		CapsuleBuffer buffer,
		Building building,
		FacilityFilter projectedInputFilter,
		bool requireEmpty = false)
	{
		if (buffer?.DockedCapsule is not CargoCapsule capsule ||
			capsule.RouteKind != CargoRouteKind.Standard ||
			buffer.CanReceiveOutboundItems() == false ||
			projectedInputFilter.ContentState != FacilityContentState.HasItems ||
			projectedInputFilter.ItemProcessStage != ItemProcessStage.Packed ||
			BufferService?.IsExplicitRuleMatchedBuffer(
				buffer,
				projectedInputFilter,
				FacilityContentState.HasItems,
				ItemProcessStage.Packed) != true)
		{
			return false;
		}

		bool isEmptyInput =
			capsule.LogisticsState == CapsuleLogisticsState.Empty &&
			buffer.IsCapsuleEmpty();
		bool isSharedPackedInput =
			requireEmpty == false &&
			capsule.LogisticsState == CapsuleLogisticsState.Inside &&
			buffer.IsCapsuleEmpty() == false;
		if (isEmptyInput == false && isSharedPackedInput == false)
			return false;

		if (isSharedPackedInput &&
			BufferService.IsRuleMatchedBuffer(buffer, capsule, evaluateLaunchReadiness: false) == false)
		{
			return false;
		}

		FacilityManager facilityManager = GameContext.Instance.FacilityMgr;
		if (facilityManager?.IsInvalidating(buffer) == true ||
			GameContext.Instance.TaskMgr?.HasConflictingCapsuleContentDependency(
				buffer,
				WorkLineAction.Put) == true)
		{
			return false;
		}

		if (building != null &&
			building.OutboundTargetStage == ItemProcessStage.Packed &&
			building.CanDispatchOutboundBuffer(buffer))
		{
			return false;
		}

		CapsuleRelocateCoordinator coordinator = GameContext.Instance.ExistingCapsuleRelocateCoordinator;
		return coordinator == null ||
			(coordinator.IsPlayerClaimed(buffer) == false &&
			 coordinator.IsReserved(buffer) == false &&
			 coordinator.IsRelocationSourceActive(buffer) == false &&
			 coordinator.IsRelocationTargetActive(buffer) == false);
	}

	private static bool IsOutboundThresholdReached(CapsuleBuffer buffer)
	{
		if (buffer == null || GameContext.HasInstance == false)
			return false;

		return GameContext.Instance.FacilityMgr?.TryGetBuildingId(buffer, out uint buildingId) == true &&
			GameContext.Instance.BuildingMgr?.TryGetBuilding(buildingId, out Building building) == true &&
			building.CanDispatchOutboundBuffer(buffer);
	}

	private static int GetRulePriority(CapsuleBuffer buffer)
	{
		FacilityRuleManager manager = GameContext.HasInstance ? GameContext.Instance.FacilityRuleMgr : null;
		return buffer != null &&
			manager != null &&
			manager.TryGetPreset(buffer.FacilityRulePresetId, out FacilityRulePreset preset) &&
			preset?.Rule != null
			? preset.Rule.Priority
			: 0;
	}

	private void EvaluateWork()
	{
		if (GameContext.HasInstance)
			GameContext.Instance.OBWorkflowSvc?.EvaluatePackingOutputWork(buildingId);
	}

	private static void TransferPackedManifest(BoxBase sourceBox, WorkLine placeLine)
	{
		if (sourceBox == null ||
			placeLine?.Target is not CapsuleBuffer targetBuffer ||
			targetBuffer.DockedCapsule == null ||
			GameContext.HasInstance == false ||
			placeLine.CompleteQuantity <= 0)
		{
			return;
		}

		GameContext.Instance.OBWorkflowSvc?.TransferPickingManifest(
			sourceBox,
			targetBuffer.DockedCapsule,
			placeLine.ItemID,
			placeLine.CompleteQuantity,
			true);
	}

	private static bool IsWorkerCarryBoxEmpty(AIWorker worker)
	{
		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		return box == null || box.Stacks.Count <= 0;
	}
}
