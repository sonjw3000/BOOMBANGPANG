using UnityEngine;

public sealed class PackingOutputPlanner : IItemTransferPlanner
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
		PackingStationService stationService = GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc?.PackingStationService : null;
		if (stationService == null || stationService.TryResolveOutboundBuffer(sourceStation, out CapsuleBuffer targetBuffer) == false)
			return WorkPlanResult.Waiting;

		for (int i = 0; i < sourceBox.Stacks.Count; ++i)
		{
			ItemStack stack = sourceBox.Stacks[i];
			if (stack == null || stack.Quantity <= 0 || stack.HasStatus(ItemStatus.Packed) == false)
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

		return WorkPlanResult.Completed;
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
