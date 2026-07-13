using UnityEngine;
using System;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed partial class PickingTask : WorkerTask
{
	private WorkJob pickJob;
	private uint buildingId;
	private bool isPickingPhaseEnd = false;
	private bool isTaskEnd = false;
	private BoxBase checkedPickingBox = null;
	private WorkLine currentPlaceLine = null;
	private int placingLineIndex = 0;

	public enum Phase
	{
		Collect,
		Place
	}

	public WorkJob PickingData => pickJob;
	public Phase CurrentPhase => isPickingPhaseEnd ? Phase.Place : Phase.Collect;
	public WorkLine CurrentLine
	{
		get
		{
			if (isPickingPhaseEnd)
				return currentPlaceLine;

			if (PickingData.CurrentLineIndex >= pickJob.Lines.Count)
				return null;

			return PickingData.Lines[PickingData.CurrentLineIndex];
		}
	}

	private static CargoPortService CargoPortService => GameContext.Instance.CargoPortSvc;
	private static OutboundWorkflowService OutboundWorkflowService => GameContext.Instance.OBWorkflowSvc;
	private static OrderManager OrderMgr => GameContext.Instance.OrderMgr;
	private static WorkerManager WorkerManager => GameContext.Instance.WorkerMgr;

	internal uint BuildingId => buildingId;
	private PickingPlanner Planner => ResolvePlanner(buildingId);

	public PickingTask(WorkJob pickJob, uint buildingId = 0) : base(TaskType.Picking)
	{
		this.pickJob = pickJob;
		this.buildingId = buildingId;
	}

	public override bool TryGetPreferredWorker(out AIWorker worker)
	{
		worker = null;
		if (WorkerManager == null)
			return true;

		foreach (AIWorker candidate in WorkerManager.Workers)
		{
			if (candidate == null || candidate.PrimaryBuildingId == 0)
				continue;

			if (buildingId != 0 && candidate.PrimaryBuildingId != buildingId)
				continue;

			if (candidate.CanAcceptPreferredTask(this) == false)
				continue;

			uint candidateBuildingId = buildingId != 0 ? buildingId : candidate.PrimaryBuildingId;
			PickingPlanner planner = ResolvePlanner(candidateBuildingId);
			if (planner == null || planner.HasPendingCollectWork() == false)
				continue;

			worker = candidate;
			if (buildingId != 0)
				break;
		}

		return true;
	}

	protected override void OnTaskAssigned()
	{
		if (buildingId == 0 && OccupyWorker != null)
			buildingId = OccupyWorker.PrimaryBuildingId;

		if (WorkerCarryBox == null)
			Debug.LogError("No carryBox ability but assigned to picking!!");
	}

	protected override IBaseNode BuildWorkNode()
	{
		SelectorNode root = new SelectorNode();

		SelectorNode pickAfterPut = new SelectorNode();

		SequenceNode put = new SequenceNode();
		put.Add(new ActionNode(CheckPickingEnd));
		put.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CapsuleBuffer, InteractionKind.Put, SetPlacingPosition));
		put.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutItem, PlaceItems));

		SequenceNode pick = new SequenceNode();
		pick.Add(new ActionNode(CheckIsPickingState));
		pick.Add(BuildEnsureEmptyPickingBox());
		pick.Add(AIWorker.MoveToTarget(WorkerStatusTarget.Shelf, InteractionKind.Pick, SetTarget));
		pick.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickItem, PickItems));

		pickAfterPut.Add(put);
		pickAfterPut.Add(pick);

		root.Add(pickAfterPut);

		return root;
	}

	private static SequenceNode BuildEnsureEmptyPickingBox()
	{
		SequenceNode node = new();
		node.Add(AIWorker.CheckBoxAndGet(BoxType.Personal));
		node.Add(new ActionNode(LogErrorIfNewPickingBoxHasItems));
		return node;
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

	public override string GetStatusSummary()
	{
		if (isTaskEnd)
			return "Picking complete.";

		if (isPickingPhaseEnd)
		{
			string targetName = CurrentLine?.TargetName ?? "None";
			return $"Phase: Place\nTarget: {targetName}";
		}

		string sourceName = CurrentLine?.TargetName ?? "None";
		return $"Phase: Pick\nSource: {sourceName}";
	}

	public void RestoreState(uint buildingId, bool isPickingPhaseEnd, bool isTaskEnd, WorkLine currentPlaceLine = null, int placingLineIndex = 0)
	{
		this.buildingId = buildingId;
		this.isPickingPhaseEnd = isPickingPhaseEnd;
		this.isTaskEnd = isTaskEnd;
		this.currentPlaceLine = currentPlaceLine;
		this.placingLineIndex = placingLineIndex;
	}

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

	public static NodeState LogErrorIfNewPickingBoxHasItems(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;
		BoxBase box = ctx.Worker.CarryingAbility?.CarryingBox;
		if (box == null || task.checkedPickingBox == box)
			return Success;

		task.checkedPickingBox = box;
		if (HasPickedAnyLine(task) || box.Stacks.Count <= 0)
			return Success;

		Debug.LogError($"[PickingTask] Picking worker received a non-empty box. worker={ctx.Worker.WorkerID}, box={box.name}, stacks={FormatStacks(box)}");
		return Success;
	}

	private static bool HasPickedAnyLine(PickingTask task)
	{
		if (task?.PickingData?.Lines == null)
			return false;

		if (task.PickingData.CurrentLineIndex > 0)
			return true;

		foreach (WorkLine line in task.PickingData.Lines)
		{
			if (line != null && line.CompleteQuantity > 0)
				return true;
		}

		return false;
	}

	public static NodeState GetAvailableOBCargoPort(in BTContext ctx)
	{
		CargoPort targetPos = CargoPortService.FindClosestAvailablePort(
			ctx.Worker.GridPosition,
			InteractionKind.Put,
			predicate: candidate => candidate is OutboundCargoPort);

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

	public static NodeState SetTarget(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;

		if (task.CurrentLine == null)
		{
			WorkLine nextLine = null;
			PickingPlanner planner = task.Planner;
			WorkPlanResult result = planner != null
				? planner.TryGetCollectLine(ctx.Worker, out nextLine)
				: WorkPlanResult.Waiting;

			if (result == WorkPlanResult.Issued)
			{
				task.PickingData.Lines.Add(nextLine);
			}
			else
				return task.ApplyPlanResult(ctx, result);
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.CurrentLine.Target);
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
		ItemStack pickedStack = ItemStack.Rent(curLine.ItemID);
		pickedStack.AddItem(remainingQuantity);
		ItemTransferResult result = ItemTransferUtility.MoveItemAsStack(curLine.Container, box, pickedStack, consumeSourcePickReservation: true);
		if (result.Kind != TransferResultKind.Complete && pickedStack.Quantity > 0)
			pickedStack.Recycle();

		int pickedQuantity = OrderMgr.ReportPickingCompleted(curLine.RelatedOrderLine, result.Moved);
		if (pickedQuantity != result.Moved)
		{
			Debug.LogWarning($"[PickingTask] Pick progress mismatch. requested={result.Moved}, applied={pickedQuantity}");
		}

		curLine.CompleteQuantity += result.Moved;
		OutboundWorkflowService?.AddPickedToManifest(box, curLine.RelatedOrderLine, curLine.ItemID, result.Moved);
		if (curLine.IsComplete == false)
		{
			Debug.LogError("Reserve까지 해줬는데도 0이라고? 난 이거 인정 못해");
			return Failure;
		}

		task.PickingData.MoveToNextLine();
		return Success;
	}

	public static NodeState SetPlacingPosition(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;
		if (task.currentPlaceLine == null)
		{
			if (task.TryGetNextPickedLine(out WorkLine pickedLine) == false)
			{
				task.isTaskEnd = true;
				return Failure;
			}

			WorkLine nextLine = null;
			PickingPlanner planner = task.Planner;
			WorkPlanResult result = planner != null
				? planner.TryGetPlaceLine(ctx.Worker, pickedLine, out nextLine)
				: WorkPlanResult.Waiting;

			if (result == WorkPlanResult.Issued)
				task.currentPlaceLine = nextLine;
			else
				return task.ApplyPlanResult(ctx, result);
		}

		if (task.currentPlaceLine?.Target == null)
			return Failure;

		ctx.LocalBlackBoard.SetTargetBuilding(task.currentPlaceLine.Target);
		return Success;
	}

	public static NodeState PlaceItems(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;
		WorkLine line = task.currentPlaceLine;
		BoxBase box = ctx.Worker.CarryingAbility?.CarryingBox;
		if (line == null || line.Action != WorkLineAction.Put || box == null)
			return Failure;

		int remainingQuantity = line.Quantity - line.CompleteQuantity;
		ItemTransferResult result = ItemTransferUtility.MoveItem(new(
			box,
			line.Container,
			line.ItemID,
			remainingQuantity,
			handlingWorker: ctx.Worker));
		line.CompleteQuantity += result.Moved;

		if (line.IsComplete == false)
		{
			Debug.LogError("[PickingTask] Planned place quantity was not fully moved.");
			return Failure;
		}

		if (line.Container is CapsuleBuffer targetBuffer && targetBuffer.DockedCapsule != null)
		{
			int manifestMoved = OutboundWorkflowService != null
				? OutboundWorkflowService.TransferPickingManifest(box, targetBuffer.DockedCapsule, line.RelatedOrderLine, line.ItemID, result.Moved)
				: 0;
			if (manifestMoved != result.Moved)
				Debug.LogWarning($"[PickingTask] Picking manifest place mismatch. item={line.ItemID}, moved={result.Moved}, manifestMoved={manifestMoved}");
		}

		task.currentPlaceLine = null;
		task.placingLineIndex += 1;
		if (task.placingLineIndex >= task.pickJob.Lines.Count)
		{
			task.isTaskEnd = true;
			return Success;
		}

		PickingPlanner planner = task.Planner;
		return task.ApplyPlanResult(ctx, planner != null ? planner.OnPlaceLineCompleted(ctx.Worker, line, result) : WorkPlanResult.Waiting);
	}

	private static PickingPlanner ResolvePlanner(uint buildingId)
	{
		if (buildingId == 0 ||
			GameContext.HasInstance == false ||
			GameContext.Instance.BuildingMgr == null ||
			GameContext.Instance.BuildingMgr.TryGetBuilding(buildingId, out Building building) == false ||
			building is not StorageBuilding storageBuilding)
		{
			return null;
		}

		return storageBuilding.PickingPlanner;
	}

	private bool TryGetNextPickedLine(out WorkLine pickedLine)
	{
		pickedLine = null;
		if (pickJob?.Lines == null)
			return false;

		while (placingLineIndex < pickJob.Lines.Count)
		{
			WorkLine candidate = pickJob.Lines[placingLineIndex];
			if (candidate != null && candidate.Quantity > 0)
			{
				pickedLine = candidate;
				return true;
			}

			placingLineIndex += 1;
		}

		return false;
	}

	private NodeState ApplyPlanResult(in BTContext ctx, WorkPlanResult result)
	{
		switch (result)
		{
			case WorkPlanResult.Issued:
				return Success;

			case WorkPlanResult.SwitchPhase:
				isPickingPhaseEnd = true;
				currentPlaceLine = null;
				return Failure;

			case WorkPlanResult.Completed:
				if (isPickingPhaseEnd)
					isTaskEnd = true;
				else
					isPickingPhaseEnd = true;

				currentPlaceLine = null;
				return Failure;

			case WorkPlanResult.Waiting:
			default:
				ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
				ctx.Worker.SetWorkerTarget(isPickingPhaseEnd ? WorkerStatusTarget.CapsuleBuffer : WorkerStatusTarget.Shelf);
				return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}
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
