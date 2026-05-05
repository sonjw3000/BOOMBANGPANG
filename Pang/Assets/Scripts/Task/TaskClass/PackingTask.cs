using Unity.VisualScripting;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public class PackingTask : WorkerTask
{
	private static PackingStationService PackingService => GameContext.Instance.OBWorkflowMgr.PackingStations;

	public PackingTask() : base(TaskType.Packing)
	{

	}
	protected override void OnTaskAssigned()
	{
	}
	protected override IBaseNode BuildWorkNode()
	{
		// root selector
		SelectorNode root = new();

		SequenceNode packEnd = new();
		packEnd.Add(new ActionNode(CheckPackedBoxEnd));
		packEnd.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.MoveBox, PackEnd));

		SequenceNode packItems = new();
		packItems.Add(new ActionNode(CheckBoxToPack));
		packItems.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.MoveBox, PackItems));

		SequenceNode prepareBox = new();
		prepareBox.Add(new ActionNode(CheckWaitingBoxMoveable));
		prepareBox.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.MoveBox, PrepareBox));

		SequenceNode findPackingStation = new();
		findPackingStation.Add(new ActionNode(CheckPackingStation));
		findPackingStation.Add(AIWorker.MoveToTarget(WorkerStatusTarget.PackingStation, 
			InteractionKind.Work, FindPackingStation));

		root.Add(packEnd);
		root.Add(packItems);
		root.Add(prepareBox);
		root.Add(findPackingStation);

		return root;
	}

	public override bool CheckTaskEnd()
	{
		return false;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[PackingTask] : ";
	}

#endif

	// --------------------------------------------------
	// Pack End
	public static NodeState CheckPackedBoxEnd(in BTContext ctx)
	{
		var station = ctx.Worker.CurrentWorkingBuilding as PackingStation;
		if (station != null && station.CurrentPackingBox?.IsFullyPacked == true)
		{
			if (station.IsBoxMoveableToEnd)
				return Success;

			// packed tote is not removed!!!!
			// wait till tote removed
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Box);
			ctx.Worker.enabled = false;
			return Running;
		}

		return Failure;

	}

	public static NodeState PackEnd(in BTContext ctx)
	{
		var station = ctx.Worker.CurrentWorkingBuilding as PackingStation;
		if (station != null && station.EndWorkingBox())
			return Success;

		return Failure;
	}

	// --------------------------------------------------
	// Pack Items
	public static NodeState CheckBoxToPack(in BTContext ctx)
	{
		var station = ctx.Worker.CurrentWorkingBuilding as PackingStation;
		if (station != null && station.CurrentPackingBox?.IsFullyPacked == false)
			return Success;

		return Failure;
	}

	public static NodeState PackItems(in BTContext ctx)
	{
		var station = ctx.Worker.CurrentWorkingBuilding as PackingStation;
		if (station == null)
			return Failure;

		// pack item here
		BoxWithOrder box = station.CurrentPackingBox;
		WorkLine line = box.Job.CurrentLine;

		// todo
		// Item Packing에 관련해서 뭔가를 더 해주어야하는데 일단은 박스로 고정하겠다
		ItemPackage package = new(PackingType.Box, line.RelatedOrderLine, line.ItemID, 
			box.Box.RemoveItem(line.ItemID, line.CompleteQuantity));

		if (station.AddStack(package) == false)
		{
			Debug.Log("Station Stack Is Full");
			return Success;
		}

		box.Job.MoveToNextLine();

		return Success;
	}

	// --------------------------------------------------
	// Prepare Box
	public static NodeState CheckWaitingBoxMoveable(in BTContext ctx)
	{
		var station = ctx.Worker.CurrentWorkingBuilding as PackingStation;
		if (station != null)
		{
			if (station.IsBoxMoveableToPack)
				return Success;

			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Box);
			ctx.Worker.enabled = false;
			return Running;
		}

		return Failure;
	}

	public static NodeState PrepareBox(in BTContext ctx)
	{
		var station = ctx.Worker.CurrentWorkingBuilding as PackingStation;
		if (station != null && station.PrepareBox())
			return Success;

		return Failure;
	}

	// --------------------------------------------------
	// Find Station
	public static NodeState CheckPackingStation(in BTContext ctx)
	{
		if (ctx.Worker.CurrentWorkingBuilding is PackingStation)
			return Failure;

		return Success;
	}

	public static NodeState FindPackingStation(in BTContext ctx)
	{
		var worker = ctx.Worker;
		var packingStation = PackingService.GetAvailableStationToWork(worker.GridPosition);

		if (packingStation != null)
			packingStation.CurrentPackingWorker = worker;

		ctx.LocalBlackBoard.SetTargetBuilding(packingStation);
		return Success;
	}
}
