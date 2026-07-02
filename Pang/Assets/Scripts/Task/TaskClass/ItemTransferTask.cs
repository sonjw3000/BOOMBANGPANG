using System.Collections.Generic;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public enum ItemTransferPhase
{
	Collect,
	Place,
}

public sealed class ItemTransferCollectedLine
{
	public readonly WorkLine CollectLine;
	public int PlacedQuantity { get; private set; }
	public int RemainingQuantity => Mathf.Max(0, CollectLine.Quantity - PlacedQuantity);
	public bool IsPlaceComplete => RemainingQuantity <= 0;

	public ItemTransferCollectedLine(WorkLine collectLine)
	{
		CollectLine = collectLine;
	}

	public void ReportPlaced(int quantity)
	{
		PlacedQuantity = Mathf.Clamp(PlacedQuantity + quantity, 0, CollectLine.Quantity);
	}
}

public interface IItemTransferPlanner
{
	WorkPlanResult TryGetCollectLine(AIWorker worker, uint buildingId, out WorkLine line);
	WorkPlanResult OnCollectCompleted(AIWorker worker, WorkLine line, ItemTransferResult result);

	WorkPlanResult TryGetPlaceLine(AIWorker worker, uint buildingId, WorkLine collectedLine, int remainingQuantity, out WorkLine line);
	WorkPlanResult OnPlaceCompleted(AIWorker worker, WorkLine collectedLine, WorkLine placeLine, ItemTransferResult result);
}

public sealed class ItemTransferTask : WorkerTask
{
	private readonly IItemTransferPlanner planner;
	private readonly uint buildingId;
	private readonly List<ItemTransferCollectedLine> collectedLines = new();

	private ItemTransferPhase phase = ItemTransferPhase.Collect;
	private WorkLine currentLine;
	private int placingLineIndex;
	private bool isTaskEnd;

	public uint BuildingId => buildingId;
	public ItemTransferPhase Phase => phase;
	public WorkLine CurrentLine => currentLine;
	public IReadOnlyList<ItemTransferCollectedLine> CollectedLines => collectedLines;

	public ItemTransferTask(TaskType taskType, IItemTransferPlanner planner, uint buildingId = 0) : base(taskType)
	{
		this.planner = planner;
		this.buildingId = buildingId;

		if (IsSupportedTaskType(taskType) == false)
			Debug.LogWarning($"[ItemTransferTask] {taskType} is not a standard item-transfer task type.");
	}

	protected override void OnTaskAssigned()
	{
		if (WorkerCarryBox == null)
			Debug.LogError($"[ItemTransferTask] No carry box ability but assigned to {Type}.");
	}

	protected override IBaseNode BuildWorkNode()
	{
		SelectorNode root = new();

		SequenceNode place = new();
		place.Add(new ActionNode(CheckPhasePlace));
		place.Add(AIWorker.MoveToTarget(WorkerStatusTarget.WorkTarget, InteractionKind.Put, SetPlacingPosition));
		place.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutItem, PlaceItems));

		SequenceNode collect = new();
		collect.Add(new ActionNode(CheckPhaseCollect));
		collect.Add(AIWorker.CheckBoxAndGet(BoxType.Personal));
		collect.Add(AIWorker.MoveToTarget(WorkerStatusTarget.WorkTarget, InteractionKind.Pick, SetCollectingPosition));
		collect.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickItem, CollectItems));

		root.Add(place);
		root.Add(collect);
		return root;
	}

	public override bool CheckTaskEnd()
	{
		return isTaskEnd;
	}

	public override bool CanDispatchTo(AIWorker worker)
	{
		return worker != null &&
			(buildingId == 0 || worker.PrimaryBuildingId == buildingId) &&
			CanDispatchToWorkerZones(worker, currentLine?.Target);
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[ItemTransferTask:{Type}] Phase={phase}, Collected={collectedLines.Count}, PlaceIndex={placingLineIndex}";
	}
#endif

	public override string GetStatusSummary()
	{
		if (isTaskEnd)
			return $"{Type} item transfer complete.";

		string targetName = currentLine?.TargetName ?? "Pending";
		return $"Item Transfer ({Type})\nPhase: {phase}\nTarget: {targetName}";
	}

	public static NodeState CheckPhaseCollect(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		return task != null && task.phase == ItemTransferPhase.Collect ? Success : Failure;
	}

	public static NodeState CheckPhasePlace(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		return task != null && task.phase == ItemTransferPhase.Place ? Success : Failure;
	}

	public static NodeState SetCollectingPosition(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		if (task == null || task.planner == null)
			return Failure;

		if (task.currentLine == null)
		{
			WorkPlanResult result = task.planner.TryGetCollectLine(ctx.Worker, task.buildingId, out WorkLine nextLine);
			if (result == WorkPlanResult.Issued)
			{
				if (nextLine == null || nextLine.Action != WorkLineAction.Pick || nextLine.Target == null)
				{
					Debug.LogError($"[ItemTransferTask] Invalid collect line for {task.Type}.");
					return Failure;
				}

				task.currentLine = nextLine;
			}
			else
			{
				return task.ApplyPlanResult(ctx, result);
			}
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.currentLine.Target);
		return Success;
	}

	public static NodeState CollectItems(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		if (task == null || task.planner == null || task.currentLine == null)
			return Failure;

		WorkLine line = task.currentLine;
		ItemTransferResult result = MoveCollectLine(ctx.Worker, line);
		if (result.Moved > 0)
		{
			WorkLine collectedLine = CopyLineWithQuantity(line, result.Moved);
			task.collectedLines.Add(new ItemTransferCollectedLine(collectedLine));
		}

		line.CompleteQuantity += result.Moved;
		task.currentLine = null;

		WorkPlanResult next = task.planner.OnCollectCompleted(ctx.Worker, line, result);
		return task.ApplyPlanResult(ctx, next);
	}

	public static NodeState SetPlacingPosition(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		if (task == null || task.planner == null)
			return Failure;

		if (task.currentLine == null)
		{
			if (task.TryGetCurrentCollectedLine(out ItemTransferCollectedLine collectedLine) == false)
				return task.ApplyPlanResult(ctx, WorkPlanResult.Completed);

			WorkPlanResult result = task.planner.TryGetPlaceLine(
				ctx.Worker,
				task.buildingId,
				collectedLine.CollectLine,
				collectedLine.RemainingQuantity,
				out WorkLine nextLine);

			if (result == WorkPlanResult.Issued)
			{
				if (nextLine == null ||
					nextLine.Action != WorkLineAction.Put ||
					nextLine.Target == null ||
					nextLine.Quantity <= 0 ||
					nextLine.Quantity > collectedLine.RemainingQuantity)
				{
					Debug.LogError($"[ItemTransferTask] Invalid place line for {task.Type}.");
					return Failure;
				}

				task.currentLine = nextLine;
			}
			else
			{
				return task.ApplyPlanResult(ctx, result);
			}
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.currentLine.Target);
		return Success;
	}

	public static NodeState PlaceItems(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		if (task == null || task.planner == null || task.currentLine == null)
			return Failure;

		if (task.TryGetCurrentCollectedLine(out ItemTransferCollectedLine collectedLine) == false)
			return task.ApplyPlanResult(ctx, WorkPlanResult.Completed);

		WorkLine placeLine = task.currentLine;
		ItemTransferResult result = MovePlaceLine(ctx.Worker, placeLine);
		placeLine.CompleteQuantity += result.Moved;
		collectedLine.ReportPlaced(result.Moved);

		task.currentLine = null;
		if (collectedLine.IsPlaceComplete)
			task.placingLineIndex += 1;

		WorkPlanResult next = task.planner.OnPlaceCompleted(ctx.Worker, collectedLine.CollectLine, placeLine, result);
		return task.ApplyPlanResult(ctx, next);
	}

	private NodeState ApplyPlanResult(in BTContext ctx, WorkPlanResult result)
	{
		switch (result)
		{
			case WorkPlanResult.Issued:
				return Success;

			case WorkPlanResult.SwitchPhase:
				SwitchPhase();
				return Failure;

			case WorkPlanResult.Completed:
				if (phase == ItemTransferPhase.Collect && HasPendingPlaceLines())
				{
					phase = ItemTransferPhase.Place;
					currentLine = null;
					return Failure;
				}

				isTaskEnd = true;
				currentLine = null;
				return Failure;

			case WorkPlanResult.Waiting:
			default:
				ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
				ctx.Worker.SetWorkerTarget(WorkerStatusTarget.WorkTarget);
				return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}
	}

	private static ItemTransferResult MoveCollectLine(AIWorker worker, WorkLine line)
	{
		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		if (line == null || line.Container == null || box == null || line.Quantity <= 0)
			return new(new ItemTransferPayload(line?.Container, box, line != null ? line.ItemID : 0, 0), 0);

		int remainingQuantity = line.Quantity - line.CompleteQuantity;
		if (remainingQuantity <= 0)
			return new(new ItemTransferPayload(line.Container, box, line.ItemID, remainingQuantity), 0);

		return ItemTransferUtility.MoveItem(new(
			line.Container,
			box,
			line.ItemID,
			remainingQuantity,
			consumeSourcePickReservation: line.Container is ShelfBase));
	}

	private static ItemTransferResult MovePlaceLine(AIWorker worker, WorkLine line)
	{
		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		if (line == null || line.Container == null || box == null || line.Quantity <= 0)
			return new(new ItemTransferPayload(box, line?.Container, line != null ? line.ItemID : 0, 0), 0);

		int remainingQuantity = line.Quantity - line.CompleteQuantity;
		if (remainingQuantity <= 0)
			return new(new ItemTransferPayload(box, line.Container, line.ItemID, remainingQuantity), 0);

		return ItemTransferUtility.MoveItem(new(box, line.Container, line.ItemID, remainingQuantity));
	}

	private void SwitchPhase()
	{
		currentLine = null;
		if (phase == ItemTransferPhase.Collect)
		{
			phase = ItemTransferPhase.Place;
			return;
		}

		phase = ItemTransferPhase.Collect;
		ClearPlacedLines();
	}

	private bool TryGetCurrentCollectedLine(out ItemTransferCollectedLine collectedLine)
	{
		while (placingLineIndex < collectedLines.Count && collectedLines[placingLineIndex].IsPlaceComplete)
			placingLineIndex += 1;

		collectedLine = placingLineIndex < collectedLines.Count ? collectedLines[placingLineIndex] : null;
		return collectedLine != null;
	}

	private bool HasPendingPlaceLines()
	{
		for (int i = 0; i < collectedLines.Count; ++i)
		{
			if (collectedLines[i].IsPlaceComplete == false)
				return true;
		}

		return false;
	}

	private void ClearPlacedLines()
	{
		for (int i = collectedLines.Count - 1; i >= 0; --i)
		{
			if (collectedLines[i].IsPlaceComplete)
				collectedLines.RemoveAt(i);
		}

		placingLineIndex = 0;
	}

	private static WorkLine CopyLineWithQuantity(WorkLine source, int quantity)
	{
		WorkLine line = new(source.Action, source.Container, source.Target, source.ItemID, quantity, source.RelatedOrderLine);
		line.CompleteQuantity = quantity;
		return line;
	}

	private static bool IsSupportedTaskType(TaskType taskType)
	{
		return taskType is TaskType.Picking or TaskType.Storing or TaskType.Water;
	}
}
