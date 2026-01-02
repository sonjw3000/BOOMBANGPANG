using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed class PickingTask : WorkerTask
{
	private WorkJob pickJob;

	private ShelfBase targetCargoPos = null;

	private bool isTaskEnd = false;

	public WorkJob PickingData => pickJob;
	public WorkLine CurrentLine => PickingData.Lines[PickingData.CurrentLineIndex];
	
	public ShelfBase TargetCargo => targetCargoPos;

	static private CargoPortService CargoPorts => GameContext.Instance.OBWorkflowMgr.CargoPorts;

	public PickingTask(WorkJob pickJob) : base(TaskType.Picking)
	{
		this.pickJob = pickJob;
	}

	protected override void OnTaskAssigned()
	{
		carryBox = OccupyWorker.GetComponent<CarryBoxAbility>();
	}

	protected override IBaseNode BuildWorkNode()
	{
		// todo


		// local bt에 토트의 사이즈를 확인하는 단계도 넣어야함
		// 토트 용량이 넘치면 시마이치고 토트를 보내야함
		// 해당 과정을 거친 후 본인의 작업을 하게 만들어야함
		// 일단은 대충 싸갈기자
		SelectorNode root = new SelectorNode();

		// work node
		// checking tote size over capacity
		// actual work
		SelectorNode pickAfterPut = new SelectorNode();

		SequenceNode put = new SequenceNode();
		put.Add(new ActionNode(CheckPickingEnd));
		put.Add(AIWorker.MoveToTarget(GetAvailableOBCargoPort));
		put.Add(new WaitNode(1.0f));
		put.Add(new ActionNode(PickingEndAction));

		SequenceNode pick = new SequenceNode();
		pick.Add(new ActionNode(CheckIsPickingState));
		pick.Add(AIWorker.BuildCarryMoveInteract(
			boxRequirement: BoxType.Personal,
			setGoal: SetTarget,
			interact: PickItems
		));
		// todo 애니메이션을 재생해야한다 곧 지우자
		// picking중인지 확실히 보기 위해 대기한다
		pick.Add(new WaitNode(1.0f));



		// 여기에 토트 반납 알고리즘을 차려야함

		pickAfterPut.Add(put);
		pickAfterPut.Add(pick);

		// for root
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
		return $"Picking Task: {PickingData.CurrentLineIndex} / {PickingData.Lines.Count}, Goal: {CurrentLine.GoalPosition}";
	}
#endif

	public static NodeState CheckPickingEnd(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;
		if (task.PickingData.IsJobEnd)
		{
			//Debug.Log("Picking Job Ended");
			return Success;
		}
		return Failure;
	}

	public static NodeState CheckIsPickingState(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;
		if (task.PickingData.IsJobEnd == false)
		{
			return Success;
		}
		return Failure;
	}

	public static NodeState GetAvailableOBCargoPort(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;

		ShelfBase targetPos = null;

		targetPos = CargoPorts.GetClosestAvailablePort(ctx.Worker.GridPosition);

		if (targetPos == null)
		{
			Debug.Log("No Available OB cargo port!");
			return Failure;
		}

		task.targetCargoPos = targetPos;
		ctx.LocalBlackBoard.Set<int3>("goalPos", targetPos.InteractionPoints[0]);
		return Success;
	}

	public static NodeState PickingEndAction(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;
		// todo
		// 포트에 토트를 내려놓는 액션을 추가해야함
		//Debug.Log("Picking Task: Put on cargo port!");

		// todo
		// 고쳐야한다
		Dictionary<uint, int> moved = new();
		foreach ((var itemID, var quantity) in task.carryBox.CarringBox.ItemTotals)
		{
			moved[itemID] = task.targetCargoPos.AddItem(itemID, quantity);
		}

		foreach((var itemID, var quantity) in moved)
		{
			task.carryBox.CarringBox.RemoveItem(itemID, quantity);
		}


		task.isTaskEnd = true;
		return Success;
	}

	public static NodeState SetTarget(in BTContext ctx)
	{
		// test code
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;

		if (task.PickingData.IsJobEnd)
		{
			int cnt = task.PickingData.Lines.Count;
			Debug.Log($"task line idx: {task.PickingData.CurrentLineIndex}, task lines: {cnt}");
			// should not hit here
			Debug.Log("공이 웃으면?\n풋볼");
			Debug.Log("자가용의 반댓말은?\n커용");
			Debug.Log("푸가 넘어지면?\n쿵푸");
			Debug.Log("문신하면 무시할 수 없는 이유는?");
			Debug.Log("무시");
			Debug.Log("ㄴㄴ");

			return Failure;
		}

		// set goalPosition
		var line = task.CurrentLine;
		ctx.LocalBlackBoard.Set<int3>("goalPos", line.GoalPosition);

		return Success;
	}

	public static NodeState PickItems(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;
		var curLine = task.CurrentLine;
		int removed = curLine.Source.RemoveItem(curLine.ItemID, curLine.Quantity);

		BoxBase box = ctx.Worker.GetComponent<CarryBoxAbility>().CarringBox;

		if (box == null)
		{
			Debug.Log("NO BOX??? WHY?");
			return Failure;
		}

		int realAdded = box.AddItem(task.CurrentLine.ItemID, removed);

		// todo
		// 갯수를 체크해야한다
		// 중요함!
		if (task.CurrentLine.Quantity != realAdded)
		{
			Debug.Log($"Requested: {task.CurrentLine.Quantity}, Added: {realAdded}, RemovedFromShelf: {removed}");
			// 갯수가 다르기 때문에 다른곳에서 동일 물품을 줏어야 한다. 새로운 위치로 이동해야하지 않을까?
			Debug.LogError("Reserve까지 해줬는데도 0이라고? 난 이거 인정 못해");
			return Failure;
		}

		task.PickingData.MoveToLextLine();

		return Success;
	}


}
