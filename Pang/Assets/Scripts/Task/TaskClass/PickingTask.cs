using UnityEngine;
using System;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed class PickingTask : WorkerTask
{
	private WorkJob pickJob;
	private bool isPickingPhaseEnd = false;
	private bool isTaskEnd = false;

	public WorkJob PickingData => pickJob;
	public WorkLine CurrentLine
	{
		get
		{
			if (PickingData.CurrentLineIndex >= pickJob.Lines.Count)
				return null;

			return PickingData.Lines[PickingData.CurrentLineIndex];
		}
	}

	private static CargoPortService CargoPorts => GameContext.Instance.OBWorkflowMgr.CargoPorts;
	private static PackingStationService PackingService => GameContext.Instance.OBWorkflowMgr.PackingStations;
	private static OrderManager OrderMgr => GameContext.Instance.OrderMgr;
	private static PickingPlanner Planner => GameContext.Instance.OBWorkflowMgr.PickingPlanner;

	public PickingTask(WorkJob pickJob) : base(TaskType.Picking)
	{
		this.pickJob = pickJob;
	}

	public PickingTaskSaveData CaptureState(Func<GameObject, int> getPlaceableId, Func<OrderLine, int> registerOrderLine)
	{
		return new PickingTaskSaveData
		{
			Job = pickJob?.CaptureState(getPlaceableId, registerOrderLine),
			IsPickingPhaseEnd = isPickingPhaseEnd,
			IsTaskEnd = isTaskEnd,
		};
	}

	public void RestoreState(bool isPickingPhaseEnd, bool isTaskEnd)
	{
		this.isPickingPhaseEnd = isPickingPhaseEnd;
		this.isTaskEnd = isTaskEnd;
	}

	protected override void OnTaskAssigned()
	{
		if (WorkerCarryBox == null)
			Debug.LogError("No carryBox ability but assigned to picking!!");
	}

	protected override IBaseNode BuildWorkNode()
	{
		SelectorNode root = new SelectorNode();

		SelectorNode pickAfterPut = new SelectorNode();

		SequenceNode put = new SequenceNode();
		put.Add(new ActionNode(CheckPickingEnd));
		put.Add(AIWorker.MoveToTarget(WorkerStatusTarget.PackingStation, InteractionKind.Put, GetAvailablePackingStation));
		put.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, PickingEndAction));

		SequenceNode pick = new SequenceNode();
		pick.Add(new ActionNode(CheckIsPickingState));
		pick.Add(AIWorker.CheckBoxAndGet(BoxType.Personal));
		pick.Add(new ActionNode(LogErrorIfPickingBoxHasItems));
		pick.Add(AIWorker.MoveToTarget(WorkerStatusTarget.Shelf, InteractionKind.Pick, SetTarget));
		pick.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickItem, PickItems));

		pickAfterPut.Add(put);
		pickAfterPut.Add(pick);

		root.Add(pickAfterPut);

		return root;
	}

	public override bool CheckTaskEnd()
	{
		return isTaskEnd;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"Picking Task: {PickingData.CurrentLineIndex} / {PickingData.Lines.Count}";
	}
#endif

	public static NodeState CheckPickingEnd(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;
		return task.isPickingPhaseEnd ? Success : Failure;
	}

	public static NodeState CheckIsPickingState(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;
		return task.isPickingPhaseEnd == false ? Success : Failure;
	}

	public static NodeState LogErrorIfPickingBoxHasItems(in BTContext ctx)
	{
		BoxBase box = ctx.Worker.CarryingAbility?.CarryingBox;
		if (box == null || box.Stacks.Count <= 0)
			return Success;

		Debug.LogError($"[PickingTask] Picking worker received a non-empty box. worker={ctx.Worker.WorkerID}, box={box.name}, stacks={FormatStacks(box)}");
		return Success;
	}

	public static NodeState GetAvailableOBCargoPort(in BTContext ctx)
	{
		ShelfBase targetPos = CargoPorts.GetClosestAvailableTarget(ctx.Worker.GridPosition, InteractionKind.Put);

		if (targetPos == null)
		{
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.CargoPort);
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			Debug.Log("No Available OB cargo port!");
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		ctx.LocalBlackBoard.SetTargetBuilding(targetPos);
		return Success;
	}

	public static NodeState GetAvailablePackingStation(in BTContext ctx)
	{
		PackingService.TryReserveWaitingStation(ctx.Worker, out var targetStation);

		ctx.LocalBlackBoard.SetTargetBuilding(targetStation);
		if (targetStation != null)
			return Success;

		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.PackingStation);
		ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
		return AIWorker.MoveToStandbyWhileWaiting(ctx);
	}

	public static NodeState PickingEndAction(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;

		if (ctx.LocalBlackBoard.TryGetTargetBuilding(out var placeable)
			&& placeable is PackingStation station)
		{
			if (task.WorkerCarryBox.GetBox(out var box) && station.PutBoxToPack(new BoxWithOrder(box, task.pickJob)))
			{
				task.isTaskEnd = true;
				return Success;
			}
			else if (box != null)
			{
				station.ClearIncomingBoxReservation(ctx.Worker);
				task.WorkerCarryBox.PutBox(box);
			}
		}

		return Failure;
	}

	public static NodeState SetTarget(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;

		if (task.CurrentLine == null)
		{
			if (Planner != null && Planner.TryAllocateNextCollectLine(ctx.Worker, out var nextLine))
			{
				task.PickingData.Lines.Add(nextLine);
			}
			else
			{
				task.isPickingPhaseEnd = true;
				return Failure;
			}
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.CurrentLine.Source);
		return Success;
	}

	public static NodeState PickItems(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;
		WorkLine curLine = task.CurrentLine;

		BoxBase box = ctx.Worker.CarryingAbility?.CarryingBox;
		if (box == null)
		{
			Debug.Log("NO BOX??? WHY?");
			return Failure;
		}

		int remainingQuantity = curLine.Quantity - curLine.CompleteQuantity;
		ItemTransferResult result = ItemTransferUtility.MoveItem(new(curLine.Source, box, curLine.ItemID, remainingQuantity));
		int pickedQuantity = OrderMgr.ReportPickingCompleted(curLine.RelatedOrderLine, result.Moved);
		if (pickedQuantity != result.Moved)
		{
			Debug.LogWarning($"[PickingTask] Pick progress mismatch. requested={result.Moved}, applied={pickedQuantity}");
		}

		curLine.CompleteQuantity += result.Moved;
		if (curLine.IsComplete == false)
		{
			Debug.LogError("Reserve까지 해줬는데도 0이라고? 난 이거 인정 못해");
			return Failure;
		}

		task.PickingData.MoveToNextLine();
		return Success;
	}

	private static string FormatStacks(BoxBase box)
	{
		if (box == null || box.Stacks.Count <= 0)
			return "empty";

		string result = "";
		for (int i = 0; i < box.Stacks.Count; ++i)
		{
			ItemStack stack = box.Stacks[i];
			if (i > 0)
				result += ", ";

			result += $"{stack.ItemID}x{stack.Quantity}";
		}

		return result;
	}
}
