
// storing 전략에 따라 sorting 목적지를 다르게 설정해야 할 수도 있음
// 쿠팡 입고 했던 애들 말 들어보면 이제
// 실제 물건 저장은 지들이 알아서 한다는데
// 내가 구상한 방식은 아이템을 직접 선반에 지정하는 방식이라
// 이거에 맞게 구현해야 할 듯

using UnityEngine;
using System;
using Unity.Mathematics;
using static IBaseNode;
using static IBaseNode.NodeState;

public partial class StoringTask : WorkerTask
{
	private WorkJob storeJob;
	private uint buildingId;
	private WorkLine currentLine = null;

	public bool IsJobEnd = false;

	// todo
	// task 분리 전 임시 코드
	public Phase CurrentPhase = Phase.Collect;

	public enum Phase
	{
		Collect,
		Place
	}


	public WorkLine CurrentLine => currentLine;
	private static StoringPlanner Planner => GameContext.Instance.IBWorkflowSvc.StoringPlanner;
	private static WorkerManager WorkerManager => GameContext.Instance.WorkerMgr;
	internal uint BuildingId => buildingId;

	public StoringTask(WorkJob job, uint buildingId = 0) : base(TaskType.Storing)
	{
		storeJob = job;
		this.buildingId = buildingId;
	}

	public override bool TryGetPreferredWorker(out AIWorker worker)
	{
		worker = null;
		if (buildingId == 0 || WorkerManager == null)
			return false;

		int bestDistance = int.MaxValue;
		foreach (AIWorker candidate in WorkerManager.Workers)
		{
			if (candidate == null ||
				candidate.PrimaryBuildingId != buildingId ||
				candidate.CanAcceptPreferredTask(this) == false)
			{
				continue;
			}

			int distance = math.abs(candidate.GridPosition.x - GetReferencePosition().x) + math.abs(candidate.GridPosition.z - GetReferencePosition().z);
			if (distance >= bestDistance)
				continue;

			bestDistance = distance;
			worker = candidate;
		}

		return true;
	}

	protected override void OnTaskAssigned()
	{
		if (WorkerCarryBox == null)
		{
			Debug.LogError("No carryBox ability but assigned to storing!!");
		}
	}

	protected override IBaseNode BuildWorkNode()
	{
		// 1) main
		SelectorNode workNode = new SelectorNode();

		// phase: collecting
		SequenceNode collect = new SequenceNode();
		collect.Add(new ActionNode(CheckPhaseCollect));
		collect.Add(AIWorker.CheckBoxAndGet(BoxType.Personal));
		collect.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CapsuleBuffer, InteractionKind.Pick, SetCollectingPosition));
		collect.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickItem, PickItems));

		// phase: placing
		SequenceNode place = new SequenceNode();
		place.Add(new ActionNode(CheckPhasePlace));
		place.Add(AIWorker.MoveToTarget(WorkerStatusTarget.Shelf, InteractionKind.Put, SetPlacingPosition));
		place.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutItem, PlaceItems));

		workNode.Add(collect);
		workNode.Add(place);

		return workNode;
	}

	public override bool CheckTaskEnd()
	{
		return IsJobEnd;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[StoringTask] Phase: {CurrentPhase}";
	}
#endif

	public override string GetStatusSummary()
	{
		if (IsJobEnd)
			return "Storing complete.";

		if (CurrentPhase == Phase.Collect)
		{
			string sourceName = CurrentLine?.TargetName ?? "None";
			return $"Phase: Collect\nSource: {sourceName}";
		}

		string placeName = CurrentLine?.TargetName ?? "None";
		return $"Phase: Place\nTarget: {placeName}";
	}

	public static NodeState CheckPhaseCollect(in BTContext ctx)
	{
		StoringTask task = (StoringTask)ctx.Worker.CurrentTask;
		return task.CurrentPhase == Phase.Collect ? Success : Failure;
	}

	public static NodeState SetCollectingPosition(in BTContext ctx)
	{
		StoringTask task = (StoringTask)ctx.Worker.CurrentTask;
		if (task.currentLine == null)
		{
			WorkLine nextLine = null;
			WorkPlanResult result = Planner != null
				? Planner.TryGetCollectLine(ctx.Worker, task.buildingId, out nextLine)
				: WorkPlanResult.Waiting;

			if (result == WorkPlanResult.Issued)
			{
				task.currentLine = nextLine;
			}
			else
				return task.ApplyPlanResult(ctx, result);
		}

		if (task.currentLine?.Target == null)
			return Failure;

		ctx.LocalBlackBoard.SetTargetBuilding(task.currentLine.Target);

		return Success;
	}

	private int3 GetReferencePosition()
	{
		if (CurrentLine?.Target != null)
			return CurrentLine.Target.GridPosition;

		return OccupyWorker != null ? OccupyWorker.GridPosition : default;
	}

	public static NodeState PickItems(in BTContext ctx)
	{
		StoringTask task = (StoringTask)ctx.Worker.CurrentTask;

		BoxBase box = task.CarryingAbility.CarryingBox;

		if (box == null)
		{
			Debug.LogError("NO BOX??? WHY?");
			return Failure;
		}

		WorkLine line = task.currentLine;
		if (line == null || line.Action != WorkLineAction.Pick)
			return Failure;

		int remainingQuantity = line.Quantity - line.CompleteQuantity;
		ItemTransferResult result = ItemTransferUtility.MoveItem(new(line.Container, box, line.ItemID, remainingQuantity));
		line.CompleteQuantity += result.Moved;

		if (line.IsComplete == false)
		{
			Debug.Log($"Quantity: {line.Quantity}, real picked: {line.CompleteQuantity}");
			Debug.LogError("[StoringTask] Planned pick quantity was not fully moved.");
			return Failure;
		}

		task.currentLine = null;
		return task.ApplyPlanResult(ctx, Planner != null ? Planner.OnCollectLineCompleted(ctx.Worker, line, result) : WorkPlanResult.Waiting);
	}

	public static NodeState CheckPhasePlace(in BTContext ctx)
	{
		StoringTask task = (StoringTask)ctx.Worker.CurrentTask;
		return task.CurrentPhase == Phase.Place ? Success : Failure;
	}

	public static NodeState SetPlacingPosition(in BTContext ctx)
	{
		StoringTask task = (StoringTask)ctx.Worker.CurrentTask;
		if (task.currentLine == null)
		{
			WorkLine nextLine = null;
			WorkPlanResult result = Planner != null
				? Planner.TryGetPlaceLine(ctx.Worker, task.buildingId, out nextLine)
				: WorkPlanResult.Waiting;

			if (result == WorkPlanResult.Issued)
			{
				task.currentLine = nextLine;
			}
			else
				return task.ApplyPlanResult(ctx, result);
		}

		if (task.currentLine?.Target == null)
			return Failure;

		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Shelf);
		ctx.LocalBlackBoard.SetTargetBuilding(task.currentLine.Target);

		return Success;
	}

	public static NodeState PlaceItems(in BTContext ctx)
	{
		StoringTask task = (StoringTask)ctx.Worker.CurrentTask;

		// place items to target
		WorkLine line = task.currentLine;
		BoxBase box = task.WorkerCarryBox.CarryingBox;

		if (line == null || box == null || line.Action != WorkLineAction.Put)
		{
			// todo worker를 off 후 대기시켜야함
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
			return Running;
		}
		
		int remainingQuantity = line.Quantity - line.CompleteQuantity;
		ItemTransferResult result = ItemTransferUtility.MoveItem(new(
			box,
			line.Container,
			line.ItemID,
			remainingQuantity,
			handlingWorker: ctx.Worker));
		line.CompleteQuantity += result.Moved;

		if (result.Kind != TransferResultKind.Complete)
		{
			// 현재 목적지가 더 받을 수 없으면 다음 평가에서 placing policy에게 새 위치를 요청한다.
			// 새 위치가 없다면 SetPlacingPosition에서 standby로 이동한다.
			task.currentLine = null;
			return Failure;
		}

		task.currentLine = null;
		return task.ApplyPlanResult(ctx, Planner != null ? Planner.OnPlaceLineCompleted(ctx.Worker, line, result) : WorkPlanResult.Waiting);
	}

	private NodeState ApplyPlanResult(in BTContext ctx, WorkPlanResult result)
	{
		switch (result)
		{
			case WorkPlanResult.Issued:
				return Success;

			case WorkPlanResult.SwitchPhase:
				CurrentPhase = CurrentPhase == Phase.Collect ? Phase.Place : Phase.Collect;
				currentLine = null;
				return Failure;

			case WorkPlanResult.Completed:
				IsJobEnd = true;
				currentLine = null;
				return Failure;

			case WorkPlanResult.Waiting:
			default:
				ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
				ctx.Worker.SetWorkerTarget(CurrentPhase == Phase.Collect ? WorkerStatusTarget.CapsuleBuffer : WorkerStatusTarget.Shelf);
				return AIWorker.KeepTaskWaiting(ctx);
		}
	}
}
