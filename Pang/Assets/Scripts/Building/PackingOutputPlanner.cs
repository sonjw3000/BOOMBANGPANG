using UnityEngine;

public sealed class PackingOutputPlanner : IItemTransferPlanner
{
	private readonly PackingBuilding building;
	private readonly PackingStation sourceStation;

	public PackingOutputPlanner(PackingBuilding building, PackingStation sourceStation)
	{
		this.building = building;
		this.sourceStation = sourceStation;
	}

	public WorkPlanResult TryGetCollectLine(AIWorker worker, uint buildingId, out WorkLine line)
	{
		line = null;
		if (building == null || sourceStation == null || sourceStation.EndPackingBox == null)
			return WorkPlanResult.Completed;

		line = new WorkLine(WorkLineAction.Pick, sourceStation, sourceStation, 0, 1);
		return WorkPlanResult.Issued;
	}

	public WorkPlanResult OnCollectCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		return result.Moved > 0 ? WorkPlanResult.SwitchPhase : WorkPlanResult.Completed;
	}

	public WorkPlanResult TryGetPlaceLine(AIWorker worker, uint buildingId, WorkLine collectedLine, int remainingQuantity, out WorkLine line)
	{
		line = null;
		BoxBase sourceBox = worker?.CarryingAbility?.CarryingBox;
		if (sourceBox == null)
			return WorkPlanResult.Completed;

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
		return IsWorkerCarryBoxEmpty(worker) ? WorkPlanResult.Completed : WorkPlanResult.Issued;
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
