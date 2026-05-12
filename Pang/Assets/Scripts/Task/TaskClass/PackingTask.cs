using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public class PackingTask : WorkerTask
{
	private static PackingStationService PackingService => GameContext.Instance.OBWorkflowMgr.PackingStations;

	private readonly PackingStation targetStation;
	private bool isTaskEnd = false;

	public PackingStation TargetStation => targetStation;

	public PackingTask(PackingStation targetStation) : base(TaskType.Packing)
	{
		this.targetStation = targetStation;
	}

	protected override void OnTaskAssigned()
	{
		if (targetStation == null)
			return;

		if (targetStation.CurrentPackingWorker == null)
			targetStation.CurrentPackingWorker = OccupyWorker;

		PackingService.OnPackingTaskAssigned(targetStation);
	}

	public override bool TryGetPreferredWorker(out AIWorker worker)
	{
		worker = targetStation != null ? targetStation.CurrentPackingWorker : null;
		return worker != null;
	}

	protected override IBaseNode BuildWorkNode()
	{
		SequenceNode root = new();
		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.PackingStation, InteractionKind.Work, SetPackingStation));

		SelectorNode work = new();

		SequenceNode packEnd = new();
		packEnd.Add(new ActionNode(CheckPackedBoxEnd));
		packEnd.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.MoveBox, PackEnd));
		packEnd.Add(new ActionNode(MarkTaskComplete));

		SequenceNode packItems = new();
		packItems.Add(new ActionNode(CheckBoxToPack));
		packItems.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PackItem, PackItems));

		SequenceNode prepareBox = new();
		prepareBox.Add(new ActionNode(CheckWaitingBoxMoveable));
		prepareBox.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.MoveBox, PrepareBox));

		work.Add(packEnd);
		work.Add(packItems);
		work.Add(prepareBox);

		root.Add(work);
		return root;
	}

	public override bool CheckTaskEnd()
	{
		return isTaskEnd;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[PackingTask] : {targetStation?.name}";
	}
#endif

	private static PackingTask GetTask(in BTContext ctx) => ctx.Worker.CurrentTask as PackingTask;

	private static PackingStation GetStation(in BTContext ctx) => GetTask(ctx)?.TargetStation;

	public static NodeState SetPackingStation(in BTContext ctx)
	{
		var station = GetStation(ctx);
		if (station == null)
			return Failure;

		ctx.LocalBlackBoard.SetTargetBuilding(station);
		return Success;
	}

	public static NodeState CheckPackedBoxEnd(in BTContext ctx)
	{
		var station = GetStation(ctx);
		if (station != null && station.CurrentPackingBox?.IsFullyPacked == true)
		{
			if (station.IsBoxMoveableToEnd)
				return Success;

			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Box);
			ctx.Worker.enabled = false;
			return Running;
		}

		return Failure;
	}

	public static NodeState PackEnd(in BTContext ctx)
	{
		var station = GetStation(ctx);
		return station != null && station.EndWorkingBox() ? Success : Failure;
	}

	public static NodeState MarkTaskComplete(in BTContext ctx)
	{
		var task = GetTask(ctx);
		if (task == null)
			return Failure;

		task.isTaskEnd = true;
		return Success;
	}

	public static NodeState CheckBoxToPack(in BTContext ctx)
	{
		var station = GetStation(ctx);
		if (station != null && station.CurrentPackingBox?.IsFullyPacked == false)
			return Success;

		return Failure;
	}

	public static NodeState PackItems(in BTContext ctx)
	{
		var station = GetStation(ctx);
		if (station == null)
			return Failure;

		BoxWithOrder box = station.CurrentPackingBox;
		WorkLine line = box?.Job.CurrentLine;
		if (box == null || line == null)
			return Failure;

		int res = box.Box.RemoveItem(line.ItemID, line.CompleteQuantity);
		ItemPackage package = new(PackingType.Box, line.RelatedOrderLine, line.ItemID, res);

		if (station.AddStack(package) == false)
		{
			Debug.Log("Station Stack Is Full");
			return Success;
		}

		box.Job.MoveToNextLine();
		return Success;
	}

	public static NodeState CheckWaitingBoxMoveable(in BTContext ctx)
	{
		var station = GetStation(ctx);
		if (station == null)
			return Failure;

		if (station.IsBoxMoveableToPack)
			return Success;

		if (station.HasWaitingBox == false)
			return Failure;

		ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Box);
		ctx.Worker.enabled = false;
		return Running;
	}

	public static NodeState PrepareBox(in BTContext ctx)
	{
		var station = GetStation(ctx);
		return station != null && station.PrepareBox() ? Success : Failure;
	}
}
