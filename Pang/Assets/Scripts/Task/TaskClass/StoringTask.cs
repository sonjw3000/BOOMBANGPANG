
// storing 전략에 따라 sorting 목적지를 다르게 설정해야 할 수도 있음
// 쿠팡 입고 했던 애들 말 들어보면 이제
// 실제 물건 저장은 지들이 알아서 한다는데
// 내가 구상한 방식은 아이템을 직접 선반에 지정하는 방식이라
// 이거에 맞게 구현해야 할 듯

using UnityEngine;
using System;
using static IBaseNode;
using static IBaseNode.NodeState;

public partial class StoringTask : WorkerTask
{
	private WorkJob storeJob;

	private WorkLine placingLine = null;

	public bool IsJobEnd = false;

	// todo
	// task 분리 전 임시 코드
	public Phase CurrentPhase = Phase.Collect;

	public enum Phase
	{
		Collect,
		Place
	}


	public WorkLine CurrentLine => storeJob?.CurrentLine;
	private static StoringPlanner Planner => GameContext.Instance.IBWorkflowSvc.StoringPlanner;

	public StoringTask(WorkJob job) : base(TaskType.Storing)
	{
		storeJob = job;
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
		collect.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CargoPort, InteractionKind.Pick, SetCollectingPosition));
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
		return $"[StoringTask] CurrentIndex: {storeJob.CurrentLineIndex}";
	}
#endif

	public override string GetStatusSummary()
	{
		if (IsJobEnd)
			return "Storing complete.";

		if (CurrentPhase == Phase.Collect)
		{
			string sourceName = CurrentLine?.Source != null ? CurrentLine.Source.name : "None";
			return $"Phase: Collect\nSource: {sourceName}";
		}

		string placeName = placingLine?.Source != null ? placingLine.Source.name : "None";
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
		if (task.CurrentLine == null)
		{
			if (Planner != null && Planner.TryAllocateNextCollectLine(ctx.Worker, out var nextLine))
			{
				task.storeJob.Lines.Add(nextLine);
			}
			else if (task.CarryingAbility?.CarryingBox != null && task.CarryingAbility.CarryingBox.Stacks.Count > 0)
			{
				task.CurrentPhase = Phase.Place;
				return Failure;
			}
			else
			{
				task.IsJobEnd = true;
				return Failure;
			}
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.CurrentLine.Source);

		return Success;
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

		int remainingQuantity = task.CurrentLine.Quantity - task.CurrentLine.CompleteQuantity;
		ItemTransferResult result = ItemTransferUtility.MoveItem(new(task.CurrentLine.Source, box, task.CurrentLine.ItemID, remainingQuantity, consumeSourcePickReservation: true));
		task.CurrentLine.CompleteQuantity += result.Moved;

		if (task.CurrentLine.IsComplete == false)
		{
			Debug.Log($"Quantity: {task.CurrentLine.Quantity}, real picked: {task.CurrentLine.CompleteQuantity}");
			Debug.LogError("Reserve까지 해줬는데도 0이라고? 난 이거 인정 못해");
			return Failure;
		}

		task.storeJob.MoveToNextLine();

		return Success;
	}

	public static NodeState CheckPhasePlace(in BTContext ctx)
	{
		StoringTask task = (StoringTask)ctx.Worker.CurrentTask;
		return task.CurrentPhase == Phase.Place ? Success : Failure;
	}

	public static NodeState SetPlacingPosition(in BTContext ctx)
	{
		StoringTask task = (StoringTask)ctx.Worker.CurrentTask;
		if (task.placingLine == null && (Planner == null || Planner.TryDecideNextPlacingLine(ctx.Worker, out task.placingLine) == false))
		{
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Shelf);
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			// 현재는 standby에서 task를 계속 재평가한다.
			// 이후 shelf/storage manager가 worker를 disable 후 가능해질 때 enable하는 패턴으로 교체할 수 있다.
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Shelf);
		ctx.LocalBlackBoard.SetTargetBuilding(task.placingLine.Source);

		return Success;
	}

	public static NodeState PlaceItems(in BTContext ctx)
	{
		StoringTask task = (StoringTask)ctx.Worker.CurrentTask;

		// place items to target
		WorkLine line = task.placingLine;
		BoxBase box = task.WorkerCarryBox.CarryingBox;

		if (line == null || box == null)
		{
			// todo worker를 off 후 대기시켜야함
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
			return Running;
		}
		
		ItemTransferResult result = ItemTransferUtility.MoveItem(new(box, line.Source, line.ItemID, line.Quantity));
		line.CompleteQuantity += result.Moved;

		if (result.Kind == TransferResultKind.Complete)
		{
			task.placingLine = null;
		}
		else
		{
			// 현재 목적지가 더 받을 수 없으면 다음 평가에서 placing policy에게 새 위치를 요청한다.
			// 새 위치가 없다면 SetPlacingPosition에서 standby로 이동한다.
			task.placingLine = null;
			return Failure;
		}

		// if no items in box, end job
		if (box.Stacks.Count == 0)
		{
			//Debug.Log("Box End!");
			task.IsJobEnd = true;
		}

		return Success;
	}
}
