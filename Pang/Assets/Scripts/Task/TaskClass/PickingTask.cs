using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed class PickingTask : WorkerTask
{
	public class PickJob
	{
		public class PickLine
		{
			private ItemLocation location;
			private int quantity;

			public ItemLocation Location => location;
			public int Quantity => quantity;
			public int3 GoalPosition => Location.Container.InteractionPoints[0];
			public uint ItemID => Location.ItemID;

			public PickLine(ItemLocation location, int quantity)
			{
				this.location = location;
				this.quantity = quantity;
			}
		}

		private int jobID;
		private int currentLine = 0;
		public List<PickLine> lines = new();

		public int JobID => jobID;
		public int CurrentLineIndex => currentLine;
		public List<PickLine> Lines => lines;

		public PickJob(int jobId)
		{
			this.jobID = jobId;
		}

		public void AddLine(ItemLocation line, int quantity)
		{
			lines.Add(new PickLine(line, quantity));
		}

		public bool IsPickingEnd()
		{
			return currentLine >= Lines.Count;
		}

		public void MoveToLextLine()
		{
			++currentLine;
		}
	}

	public PickJob PickingData { get; private set; }
	public PickJob.PickLine CurrentLine => PickingData.Lines[PickingData.CurrentLineIndex];
	public PickingTask(PickJob pickJob) : base(TaskType.Picking)
	{
		PickingData = pickJob;
	}

	protected override void OnTaskAssigned()
	{
		carryBox = OccupyWorker.GetComponent<CarryBoxAbility>();
	}

	protected override void BuildTaskNode()
	{
		// todo


		// local bt에 토트의 사이즈를 확인하는 단계도 넣어야함
		// 토트 용량이 넘치면 시마이치고 토트를 보내야함
		// 해당 과정을 거친 후 본인의 작업을 하게 만들어야함
		// 일단은 대충 싸갈기자
		SelectorNode root = new SelectorNode();

		// check is picking work is fulfilled
		SequenceNode checkingFulfilled = new SequenceNode();
		checkingFulfilled.Add(new ActionNode(CheckFulfilled));
		checkingFulfilled.Add(new ActionNode(AIWorker.TaskCompleted));

		// work node
		// checking tote size over capacity
		// actual work
		SequenceNode work = new SequenceNode();
		work.Add(AIWorker.GetBox(BoxType.Personal));
		work.Add(AIWorker.MoveToTarget(SetTarget));
		work.Add(new ActionNode(PickItems));
		// todo 애니메이션을 재생해야한다 곧 지우자
		// picking중인지 확실히 보기 위해 대기한다
		work.Add(new WaitNode(1.0f));
		// 여기에 토트 반납 알고리즘을 차려야함

		// for root
		root.Add(checkingFulfilled);
		root.Add(work);

		baseNode = root;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"Picking Task: {PickingData.CurrentLineIndex} / {PickingData.Lines.Count}, Goal: {CurrentLine.GoalPosition}";
	}
#endif

	public override NodeState UpdateTaskNode(in BTContext ctx)
	{
		// 본인의 static bt를 돌려야 한다
		return baseNode.Evaluate(ctx);
	}

	//
	public static NodeState SetTarget(in BTContext ctx)
	{
		// test code
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;

		if (task.PickingData.IsPickingEnd())
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
		int removed = task.CurrentLine.Location.RemoveItem(task.CurrentLine.Quantity);

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
			// 갯수가 다르기 때문에 다른곳에서 동일 물품을 줏어야 한다. 새로운 위치로 이동해야하지 않을까?
			Debug.LogError("Reserve까지 해줬는데도 0이라고? 난 이거 인정 못해");
			return Failure;
		}

		task.PickingData.MoveToLextLine();

		return Success;
	}

	public static NodeState CheckFulfilled(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;

		if (task.PickingData.IsPickingEnd())
			return Success;

		return Failure;
	}

}
