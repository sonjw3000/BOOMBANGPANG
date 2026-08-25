using UnityEngine;

public sealed class PackingOutputPlanner : IItemTransferPlanner, IItemTransferTaskInvalidationHandler
{
	private readonly PackingBuilding building;

	public PackingOutputPlanner(PackingBuilding building)
	{
		this.building = building;
	}

	public WorkPlanResult TryGetCollectLine(AIWorker worker, uint buildingId, out WorkLine line)
	{
		line = null;
		if (building == null)
			return WorkPlanResult.Completed;

		while (building.TryTakeDirtyPackingOutputStation(out PackingStation station))
		{
			if (building.CanBuildPackingOutputTask(station) == false)
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
			PackingStation sourceStation = line?.Target as PackingStation;
			if (sourceStation != null && building.CanBuildPackingOutputTask(sourceStation))
				building.MarkPackingOutputDirty(sourceStation);

			return WorkPlanResult.Completed;
		}

		return WorkPlanResult.SwitchPhase;
	}

	public WorkPlanResult TryGetPlaceLine(AIWorker worker, uint buildingId, WorkLine collectedLine, int remainingQuantity, out WorkLine line)
	{
		line = null;
		BoxBase sourceBox = worker?.CarryingAbility?.CarryingBox;
		if (sourceBox == null)
			return WorkPlanResult.Completed;

		PackingStation sourceStation = collectedLine?.Target as PackingStation;
		OutboundWorkflowService outbound = GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null;
		PackingStationService stationService = outbound?.PackingStationService;
		if (stationService == null)
			return WorkPlanResult.Waiting;
		outbound.TryGetPickingManifest(sourceBox, out PickingManifest manifest);

		bool hasPackedPayload = false;
		for (int i = 0; i < sourceBox.Stacks.Count; ++i)
		{
			ItemStack stack = sourceBox.Stacks[i];
			if (stack == null || stack.Quantity <= 0 || stack.HasStatus(ItemStatus.Packed) == false)
				continue;

			hasPackedPayload = true;
			FacilityFilter filter = FacilityFilter.WithCapsuleBufferState(
				FacilityFilter.WithCargoProcessStage(
					FacilityFilter.ForManifestTransfer(
						sourceBox,
						manifest,
						stack.ItemID,
						stack.Quantity,
						candidate => candidate.HasStatus(ItemStatus.Packed),
						worker),
					CargoProcessStage.Packed),
				CapsuleBufferStateRequirement.Inside);
			if (stationService.TryResolveOutboundBuffer(sourceStation, filter, out CapsuleBuffer targetBuffer) == false)
				continue;

			int movable = ItemTransferUtility.GetMovableQuantity(
				sourceBox,
				targetBuffer,
				stack.ItemID,
				stack.Quantity,
				candidate => candidate.HasStatus(ItemStatus.Packed));
			if (movable <= 0)
				continue;

			line = new WorkLine(
				WorkLineAction.Put,
				targetBuffer,
				targetBuffer,
				stack.ItemID,
				movable,
				null,
				ItemStatus.Packed);
			return WorkPlanResult.Issued;
		}

		return hasPackedPayload ? WorkPlanResult.Waiting : WorkPlanResult.Completed;
	}

	public WorkPlanResult OnPlaceCompleted(AIWorker worker, WorkLine collectedLine, WorkLine placeLine, ItemTransferResult result)
	{
		if (result.Moved <= 0)
			return WorkPlanResult.Waiting;

		TransferPackedManifest(worker?.CarryingAbility?.CarryingBox, placeLine);
		if (IsWorkerCarryBoxEmpty(worker) == false)
			return WorkPlanResult.Issued;

		return WorkPlanResult.Completed;
	}

	public void OnTaskInvalidated(ItemTransferTask task)
	{
		if (task?.Phase != ItemTransferPhase.Collect || task.CurrentLine?.Target is not PackingStation station)
			return;

		FacilityManager facilityManager = GameContext.HasInstance ? GameContext.Instance.FacilityMgr : null;
		if (building != null &&
			(facilityManager == null || facilityManager.IsInvalidating(station) == false) &&
			building.CanBuildPackingOutputTask(station))
		{
			building.MarkPackingOutputDirty(station);
		}
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
