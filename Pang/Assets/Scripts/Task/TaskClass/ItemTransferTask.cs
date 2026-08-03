using System.Collections.Generic;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public enum ItemTransferPhase
{
	Collect,
	Place,
}

public enum TransferObjectType
{
	Item,
	Box,
}

public sealed class ItemTransferJob
{
	public readonly IItemTransferPlanner Planner;
	public readonly TransferObjectType CollectType;
	public readonly TransferObjectType PlaceType;
	public readonly uint BuildingId;
	public readonly AIWorker PreferredWorker;

	public ItemTransferJob(
		IItemTransferPlanner planner,
		TransferObjectType collectType,
		TransferObjectType placeType,
		uint buildingId = 0,
		AIWorker preferredWorker = null)
	{
		Planner = planner;
		CollectType = collectType;
		PlaceType = placeType;
		BuildingId = buildingId;
		PreferredWorker = preferredWorker;
	}
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

public interface IItemTransferTaskInvalidationHandler
{
	void OnTaskInvalidated(ItemTransferTask task);
}

public sealed class ItemTransferTask : WorkerTask
{
	private readonly ItemTransferJob job;
	private readonly List<ItemTransferCollectedLine> collectedLines = new();

	private ItemTransferPhase phase = ItemTransferPhase.Collect;
	private WorkLine currentLine;
	private int placingLineIndex;
	private bool isTaskEnd;

	public uint BuildingId => job != null ? job.BuildingId : 0;
	public ItemTransferPhase Phase => phase;
	public WorkLine CurrentLine => currentLine;
	public IReadOnlyList<ItemTransferCollectedLine> CollectedLines => collectedLines;

	public ItemTransferTask(TaskType taskType, ItemTransferJob job) : base(taskType)
	{
		this.job = job;

		if (IsSupportedTaskType(taskType) == false)
			Debug.LogWarning($"[ItemTransferTask] {taskType} is not a standard item-transfer task type.");
	}

	protected override void OnTaskAssigned()
	{
		if (WorkerCarryBox == null)
			Debug.LogError($"[ItemTransferTask] No carry box ability but assigned to {Type}.");
	}

	protected override void OnTaskInvalidated()
	{
		if (job?.Planner is IItemTransferTaskInvalidationHandler handler)
			handler.OnTaskInvalidated(this);
	}

	protected override IBaseNode BuildWorkNode()
	{
		SelectorNode root = new();

		root.Add(BuildPlaceNode(TransferObjectType.Box, WorkActionType.PutBox));
		root.Add(BuildPlaceNode(TransferObjectType.Item, WorkActionType.PutItem));
		root.Add(BuildCollectNode(TransferObjectType.Box, WorkActionType.PickBox));
		root.Add(BuildCollectNode(TransferObjectType.Item, WorkActionType.PickItem));

		return root;
	}

	private static SequenceNode BuildCollectNode(TransferObjectType transferType, WorkActionType workActionType)
	{
		SequenceNode collect = new();
		collect.Add(new ActionNode(CheckPhaseCollect));
		collect.Add(new ActionNode((in BTContext ctx) => CheckTransferType(ctx, ItemTransferPhase.Collect, transferType)));

		if (transferType == TransferObjectType.Item)
			collect.Add(AIWorker.CheckBoxAndGet(BoxType.Personal));
		else
			collect.Add(AIWorker.ReturnBox());

		collect.Add(new ActionNode(SetCollectingPosition));
		collect.Add(AIWorker.MoveToTarget(WorkerStatusTarget.WorkTarget, InteractionKind.Pick));
		collect.Add(AIWorker.BuildWorkTimeInteract(workActionType, Collect));
		return collect;
	}

	private static SequenceNode BuildPlaceNode(TransferObjectType transferType, WorkActionType workActionType)
	{
		SequenceNode place = new();
		place.Add(new ActionNode(CheckPhasePlace));
		place.Add(new ActionNode(SetPlacingPosition));
		place.Add(new ActionNode((in BTContext ctx) => CheckTransferType(ctx, ItemTransferPhase.Place, transferType)));
		place.Add(AIWorker.MoveToTarget(WorkerStatusTarget.WorkTarget, InteractionKind.Put));
		place.Add(AIWorker.BuildWorkTimeInteract(workActionType, Place));
		return place;
	}

	public override bool CheckTaskEnd()
	{
		return isTaskEnd;
	}

	public override bool CanDispatchTo(AIWorker worker)
	{
		return worker != null &&
			(BuildingId == 0 || worker.PrimaryBuildingId == BuildingId) &&
			CanDispatchToWorkerZones(worker, currentLine?.Target);
	}

	public override bool DependsOnFacility(IFacility facility)
	{
		return ReferencesFacility(currentLine, facility);
	}

	internal override FacilityTaskInvalidationAction HandleFacilityInvalidating(
		IFacility facility,
		in FacilityInvalidationContext context)
	{
		if (ReferencesFacility(currentLine, facility) == false)
			return FacilityTaskInvalidationAction.None;

		if (phase == ItemTransferPhase.Collect)
			return FacilityTaskInvalidationAction.Invalidate;

		if (job?.Planner is IItemTransferTaskInvalidationHandler handler)
			handler.OnTaskInvalidated(this);

		currentLine = null;
		return FacilityTaskInvalidationAction.Reevaluate;
	}

	public override bool TryGetPreferredWorker(out AIWorker worker)
	{
		worker = job?.PreferredWorker;
		return worker != null;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[ItemTransferTask:{Type}] {job?.CollectType} -> {job?.PlaceType}, Phase={phase}";
	}
#endif

	public override string GetStatusSummary()
	{
		if (isTaskEnd)
			return $"{Type} item transfer complete.";

		string targetName = currentLine?.TargetName ?? "Pending";
		return $"Item Transfer ({Type})\n{job?.CollectType} -> {job?.PlaceType}\nPhase: {phase}\nTarget: {targetName}";
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

	private static NodeState CheckTransferType(in BTContext ctx, ItemTransferPhase phase, TransferObjectType transferType)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		if (task?.job == null)
			return Failure;

		TransferObjectType currentType = phase == ItemTransferPhase.Collect
			? task.job.CollectType
			: task.job.PlaceType;

		return currentType == transferType ? Success : Failure;
	}

	public static NodeState SetCollectingPosition(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		if (task?.job?.Planner == null)
			return Failure;

		if (task.currentLine == null)
		{
			WorkPlanResult result = task.job.Planner.TryGetCollectLine(ctx.Worker, task.BuildingId, out WorkLine nextLine);
			if (result == WorkPlanResult.Issued)
			{
				if (task.IsValidLine(nextLine, WorkLineAction.Pick, task.job.CollectType) == false)
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

	public static NodeState Collect(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		if (task?.job?.Planner == null || task.currentLine == null)
			return Failure;

		WorkLine line = task.currentLine;
		ItemTransferResult result = task.job.CollectType == TransferObjectType.Box
			? MoveCollectBox(ctx.Worker, line)
			: MoveCollectItem(ctx.Worker, line);
		if (task.job.CollectType == TransferObjectType.Item && result.Moved > 0)
			ctx.Worker.ReportItemHandling(result.ItemId, result.Moved, ctx.Worker.CarryingAbility?.CarryingBox);

		if (result.Moved > 0)
			task.collectedLines.Add(new ItemTransferCollectedLine(CopyLineWithQuantity(line, result.Moved)));

		line.CompleteQuantity += result.Moved;
		task.currentLine = null;

		WorkPlanResult next = task.job.Planner.OnCollectCompleted(ctx.Worker, line, result);
		return task.ApplyPlanResult(ctx, next);
	}

	public static NodeState SetPlacingPosition(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		if (task?.job?.Planner == null)
			return Failure;

		if (task.currentLine == null)
		{
			if (task.TryGetCurrentCollectedLine(out ItemTransferCollectedLine collectedLine) == false)
				return task.ApplyPlanResult(ctx, WorkPlanResult.Completed);

			int remainingQuantity = task.GetRemainingPlaceQuantity(collectedLine);
			WorkPlanResult result = task.job.Planner.TryGetPlaceLine(
				ctx.Worker,
				task.BuildingId,
				collectedLine.CollectLine,
				remainingQuantity,
				out WorkLine nextLine);

			if (result == WorkPlanResult.Issued)
			{
				if (task.IsValidLine(nextLine, WorkLineAction.Put, task.job.PlaceType) == false ||
					(task.job.CollectType == TransferObjectType.Item &&
						task.job.PlaceType == TransferObjectType.Item &&
						nextLine.Quantity > collectedLine.RemainingQuantity))
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

	public static NodeState Place(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		if (task?.job?.Planner == null || task.currentLine == null)
			return Failure;

		if (task.TryGetCurrentCollectedLine(out ItemTransferCollectedLine collectedLine) == false)
			return task.ApplyPlanResult(ctx, WorkPlanResult.Completed);

		WorkLine placeLine = task.currentLine;
		ItemTransferResult result = task.job.PlaceType == TransferObjectType.Box
			? MovePlaceBox(ctx.Worker, placeLine)
			: MovePlaceItem(ctx.Worker, placeLine);
		if (task.job.PlaceType == TransferObjectType.Item && result.Moved > 0)
			ctx.Worker.ReportItemHandling(result.ItemId, result.Moved, placeLine.Container);

		placeLine.CompleteQuantity += result.Moved;
		task.ReportPlaceProgress(ctx.Worker, collectedLine, result);

		task.currentLine = null;
		if (collectedLine.IsPlaceComplete)
			task.placingLineIndex += 1;

		WorkPlanResult next = task.job.Planner.OnPlaceCompleted(ctx.Worker, collectedLine.CollectLine, placeLine, result);
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
				return AIWorker.KeepTaskWaiting(ctx);
		}
	}

	private int GetRemainingPlaceQuantity(ItemTransferCollectedLine collectedLine)
	{
		if (job == null || collectedLine == null)
			return 0;

		if (job.CollectType == TransferObjectType.Item && job.PlaceType == TransferObjectType.Item)
			return collectedLine.RemainingQuantity;

		return int.MaxValue;
	}

	private static ItemTransferResult MoveCollectItem(AIWorker worker, WorkLine line)
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
			consumeSourcePickReservation: line.ConsumeSourcePickReservation && line.Container is IItemPickReservable,
			stackPredicate: stack => line.RequiredStatus.HasValue == false || stack.HasStatus(line.RequiredStatus.Value)));
	}

	private static ItemTransferResult MovePlaceItem(AIWorker worker, WorkLine line)
	{
		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		if (line == null || line.Container == null || box == null || line.Quantity <= 0)
			return new(new ItemTransferPayload(box, line?.Container, line != null ? line.ItemID : 0, 0), 0);

		int remainingQuantity = line.Quantity - line.CompleteQuantity;
		if (remainingQuantity <= 0)
			return new(new ItemTransferPayload(box, line.Container, line.ItemID, remainingQuantity), 0);

		return ItemTransferUtility.MoveItem(new(
			box,
			line.Container,
			line.ItemID,
			remainingQuantity,
			stackPredicate: stack => line.RequiredStatus.HasValue == false || stack.HasStatus(line.RequiredStatus.Value),
			handlingWorker: worker));
	}

	private static ItemTransferResult MoveCollectBox(AIWorker worker, WorkLine line)
	{
		ItemTransferPayload payload = CreateBoxPayload(line);
		if (worker?.CarryingAbility == null || line?.Target is not BoxInteraction boxInteraction)
			return new(payload, 0);

		if (worker.CarryingAbility.CarryingBox != null)
			return new(payload, 0);

		if (boxInteraction.GetBox(out BoxBase box) == false || worker.CarryingAbility.PutBox(box) == false)
		{
			if (box != null)
				boxInteraction.PutBox(box);

			return new(payload, 0);
		}

		worker.ReportBoxHandling(box);
		return new(payload, 1);
	}

	private static ItemTransferResult MovePlaceBox(AIWorker worker, WorkLine line)
	{
		ItemTransferPayload payload = CreateBoxPayload(line);
		if (worker?.CarryingAbility == null || line?.Target is not BoxInteraction boxInteraction)
			return new(payload, 0);

		if (worker.CarryingAbility.GetBox(out BoxBase box) == false)
			return new(payload, 0);

		if (boxInteraction.PutBox(box) == false)
		{
			worker.CarryingAbility.PutBox(box);
			return new(payload, 0);
		}

		worker.ReportBoxHandling(box);
		return new(payload, 1);
	}

	private static ItemTransferPayload CreateBoxPayload(WorkLine line)
	{
		return new(null, null, line != null ? line.ItemID : 0, line != null ? line.Quantity : 1);
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

	private void ReportPlaceProgress(AIWorker worker, ItemTransferCollectedLine collectedLine, ItemTransferResult result)
	{
		if (collectedLine == null || result.Moved <= 0 || job == null)
			return;

		if (job.PlaceType == TransferObjectType.Box)
		{
			ReportAllCollectedLinesPlaced();
			return;
		}

		if (job.CollectType == TransferObjectType.Box)
		{
			if (IsWorkerCarryBoxEmpty(worker))
				collectedLine.ReportPlaced(collectedLine.RemainingQuantity);

			return;
		}

		collectedLine.ReportPlaced(result.Moved);
	}

	private void ReportAllCollectedLinesPlaced()
	{
		for (int i = 0; i < collectedLines.Count; ++i)
			collectedLines[i].ReportPlaced(collectedLines[i].RemainingQuantity);
	}

	private static bool IsWorkerCarryBoxEmpty(AIWorker worker)
	{
		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		return box == null || box.Stacks.Count <= 0;
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
		WorkLine line = new(
			source.Action,
			source.Container,
			source.Target,
			source.ItemID,
			quantity,
			source.RelatedOrderLine,
			source.RequiredStatus,
			source.ConsumeSourcePickReservation);
		line.CompleteQuantity = quantity;
		return line;
	}

	private bool IsValidLine(WorkLine line, WorkLineAction action, TransferObjectType transferType)
	{
		if (line == null || line.Action != action || line.Target == null || line.Quantity <= 0)
			return false;

		return transferType switch
		{
			TransferObjectType.Item => line.Container != null,
			TransferObjectType.Box => line.Target is BoxInteraction,
			_ => false,
		};
	}

	private static bool IsSupportedTaskType(TaskType taskType)
	{
		return taskType is TaskType.Picking or TaskType.Storing or TaskType.PackingInput or TaskType.PackingOutput or TaskType.LaunchSort;
	}

	private static bool ReferencesFacility(WorkLine line, IFacility facility)
	{
		return facility != null && line != null &&
			(ReferenceEquals(line.Target, facility) || ReferenceEquals(line.Container, facility));
	}
}
