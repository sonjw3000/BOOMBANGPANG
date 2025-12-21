
// storing 전략에 따라 sorting 목적지를 다르게 설정해야 할 수도 있음
// 쿠팡 입고 했던 애들 말 들어보면 이제
// 실제 물건 저장은 지들이 알아서 한다는데
// 내가 구상한 방식은 아이템을 직접 선반에 지정하는 방식이라
// 이거에 맞게 구현해야 할 듯

using UnityEngine;
using Unity.Mathematics;
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


	public WorkLine CurrentLine => storeJob.Lines[storeJob.CurrentLineIndex];

	static public IPlacingPolicy PlacingPolicy => GameContext.Instance.IBWorkflowMgr.PlacingPolicy;

	public StoringTask(WorkJob job) : base(TaskType.Storing)
	{
		storeJob = job;
	}

	protected override void OnTaskAssigned()
	{
		carryBox = OccupyWorker.GetComponent<CarryBoxAbility>();

		if (carryBox == null)
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
		collect.Add(AIWorker.BuildCarryMoveInteract(
			boxRequirement: BoxType.Personal,
			setGoal: SetCollectingPosition,
			interact: PickItems
		));
		collect.Add(new WaitNode(1.0f));

		// phase: placing
		SequenceNode place = new SequenceNode();
		collect.Add(new ActionNode(CheckPhasePlace));
		collect.Add(AIWorker.BuildCarryMoveInteract(
			boxRequirement: BoxType.Personal,
			setGoal: SetPlacingPosition,
			interact: PlaceItems
		));
		collect.Add(new WaitNode(1.0f));

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

		var line = task.CurrentLine;
		ctx.LocalBlackBoard.Set<int3>("goalPos", line.GoalPosition);

		return Success;
	}

	public static NodeState PickItems(in BTContext ctx)
	{
		StoringTask task = (StoringTask)ctx.Worker.CurrentTask;
		int removed = task.CurrentLine.Source.RemoveItem(task.CurrentLine.ItemID, task.CurrentLine.Quantity);

		BoxBase box = task.CarryingAbility.CarringBox;

		if (box == null)
		{
			Debug.LogError("NO BOX??? WHY?");
			return Failure;
		}

		int realAdded = box.AddItem(task.CurrentLine.ItemID, removed);

		if (task.CurrentLine.Quantity != realAdded)
		{
			Debug.LogError("Reserve까지 해줬는데도 0이라고? 난 이거 인정 못해");
			return Failure;
		}

		task.storeJob.MoveToLextLine();

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
		BoxBase box = task.CarryingAbility.CarringBox;
		PlacingPolicy.TryDecide(ctx.Worker.GridPosition, box, out var decision);

		task.placingLine = new WorkLine(decision.shelf, decision.ItemID, decision.Quantity);

		Debug.Log("Got Destination!");

		if (decision.shelf == null)
		{
			// todo
			// 가능한 placingLine을 받지 못했다는 것을 어디선가 알려야 한다
			Debug.Log("No shelf");
			return Failure;
		}

		ctx.LocalBlackBoard.Set<int3>("goalPos", task.placingLine.Source.InteractionPoints[0]);
		return Success;
	}

	public static NodeState PlaceItems(in BTContext ctx)
	{
		StoringTask task = (StoringTask)ctx.Worker.CurrentTask;

		// place items to target
		WorkLine line = task.placingLine;
		BoxBase box = task.carryBox.CarringBox;
		
		int addedItem = line.Source.AddItem(line.ItemID, line.Quantity);
		box.RemoveItem(line.ItemID, addedItem);

		Debug.Log("PlacingItem!");
		
		// if fully removed, delete line
		if (addedItem == line.Quantity)
		{
			Debug.Log("Fully Moved item!");
			task.placingLine = null;
		}
	
		// if no items in box, end job
		if (box.Stacks.Count == 0)
		{
			Debug.Log("Box End!");
			task.IsJobEnd = true;
		}

		return Success;
	}

}
