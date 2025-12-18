using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed class PickingTask : WorkerTask
{
	private WorkJob pickJob;

	public WorkJob PickingData => pickJob;
	public WorkLine CurrentLine => PickingData.Lines[PickingData.CurrentLineIndex];
	
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
		SequenceNode work = AIWorker.BuildCarryMoveInteract(
			boxRequirement: BoxType.Personal,
			setGoal: SetTarget,
			interact: PickItems
		);

		// todo 애니메이션을 재생해야한다 곧 지우자
		// picking중인지 확실히 보기 위해 대기한다
		work.Add(new WaitNode(1.0f));
		// 여기에 토트 반납 알고리즘을 차려야함

		// for root
		root.Add(work);

		return root;
	}

	public override bool CheckTaskEnd()
	{
		return PickingData.IsJobEnd;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"Picking Task: {PickingData.CurrentLineIndex} / {PickingData.Lines.Count}, Goal: {CurrentLine.GoalPosition}";
	}
#endif

	public static NodeState SetTarget(in BTContext ctx)
	{
		// test code
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;

		if (task.PickingData.IsJobEnd)
		{
			// should not hit here
			Debug.Log("공이 웃으면?\n풋볼");
			Debug.Log("자가용의 반댓말은?\n커용");
			Debug.Log("푸가 넘어지면?\n쿵푸");
			Debug.Log("문신하면 무시할 수 없는 이유는?");
			Debug.Log("무시");
			Debug.Log("ㄴㄴ");
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
		//if (task.CurrentLine.Quantity != realAdded)
		if (false)
		{
			// 갯수가 다르기 때문에 다른곳에서 동일 물품을 줏어야 한다. 새로운 위치로 이동해야하지 않을까?
			Debug.LogError("Reserve까지 해줬는데도 0이라고? 난 이거 인정 못해");
			return Failure;
		}

		task.PickingData.MoveToLextLine();

		return Success;
	}
}
