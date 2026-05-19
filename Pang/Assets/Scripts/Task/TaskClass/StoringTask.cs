
// storing 전략에 따라 sorting 목적지를 다르게 설정해야 할 수도 있음
// 쿠팡 입고 했던 애들 말 들어보면 이제
// 실제 물건 저장은 지들이 알아서 한다는데
// 내가 구상한 방식은 아이템을 직접 선반에 지정하는 방식이라
// 이거에 맞게 구현해야 할 듯

using UnityEngine;
using System;
using static IBaseNode;
using static IBaseNode.NodeState;

public class StoringTask : WorkerTask
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

	static public IPlacingPolicy PlacingPolicy => GameContext.Instance.IBWorkflowMgr.PlacingPolicy;

	public StoringTask(WorkJob job) : base(TaskType.Storing)
	{
		storeJob = job;
	}

	public StoringTaskSaveData CaptureState(Func<GameObject, int> getPlaceableId, Func<OrderLine, int> registerOrderLine)
	{
		return new StoringTaskSaveData
		{
			Job = storeJob?.CaptureState(getPlaceableId, registerOrderLine),
			CurrentPhase = CurrentPhase,
			IsJobEnd = IsJobEnd,
			PlacingLine = placingLine == null ? null : new WorkLineSaveData
			{
				SourcePlaceableId = getPlaceableId != null ? getPlaceableId(placingLine.Source.gameObject) : -1,
				ItemId = placingLine.ItemID,
				Quantity = placingLine.Quantity,
				CompleteQuantity = placingLine.CompleteQuantity,
				RelatedOrderLineId = registerOrderLine != null && placingLine.RelatedOrderLine != null ? registerOrderLine(placingLine.RelatedOrderLine) : -1,
			},
		};
	}

	public void RestoreState(Phase currentPhase, bool isJobEnd, WorkLine placingLine)
	{
		CurrentPhase = currentPhase;
		IsJobEnd = isJobEnd;
		this.placingLine = placingLine;
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

	public static NodeState CheckPhaseCollect(in BTContext ctx)
	{
		StoringTask task = (StoringTask)ctx.Worker.CurrentTask;
		return task.CurrentPhase == Phase.Collect ? Success : Failure;
	}

	public static NodeState SetCollectingPosition(in BTContext ctx)
	{
		StoringTask task = (StoringTask)ctx.Worker.CurrentTask;
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
		ItemTransferResult result = ItemTransferUtility.MoveItem(new(task.CurrentLine.Source, box, task.CurrentLine.ItemID, remainingQuantity));
		task.CurrentLine.CompleteQuantity += result.Moved;

		if (task.CurrentLine.IsComplete == false)
		{
			Debug.Log($"Quantity: {task.CurrentLine.Quantity}, real picked: {task.CurrentLine.CompleteQuantity}");
			Debug.LogError("Reserve까지 해줬는데도 0이라고? 난 이거 인정 못해");
			return Failure;
		}

		task.storeJob.MoveToNextLine();

		// 모두 모았다면
		if (task.storeJob.IsJobEnd)
		{
			task.CurrentPhase = Phase.Place;
		}

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
		BoxBase box = task.CarryingAbility.CarryingBox;
		if (PlacingPolicy.TryDecide(ctx.Worker.GridPosition, box, null, out var decision) == false)
		{
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Shelf);
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			// 현재는 standby에서 task를 계속 재평가한다.
			// 이후 shelf/storage manager가 worker를 disable 후 가능해질 때 enable하는 패턴으로 교체할 수 있다.
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Shelf);

		if (decision.shelf == null)
		{
			// todo
			// 가능한 placingLine을 받지 못했다는 것을 어디선가 알려야 한다
			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			Debug.Log("No shelf");
			return AIWorker.MoveToStandbyWhileWaiting(ctx);
		}

		// 너는 즉석으로 workline을 만들어서 이동하나보다
		// 내가 그렇게 짰나보다
		task.placingLine = new WorkLine(decision.shelf, decision.ItemID, decision.Quantity);
		ctx.LocalBlackBoard.SetTargetBuilding(decision.shelf);

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
