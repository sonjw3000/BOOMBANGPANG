using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

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
			public int3 GoalPosition => Location.Container.PickingPosition;
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

	private IBaseNode baseNode;
	
	public PickJob PickingData { get; private set; }
	public PickJob.PickLine CurrentLine => PickingData.Lines[PickingData.CurrentLineIndex];
	public PickingTask(PickJob pickJob) : base(TaskType.Picking) => PickingData = pickJob;

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
		ActionNode checkFulfilled = new ActionNode(CheckFulfilled);
		ActionNode endTask = new ActionNode(AIWorker.TaskCompleted);

		checkingFulfilled.Add(checkFulfilled);
		checkingFulfilled.Add(endTask);

		// work node
		SequenceNode work = new SequenceNode();
		// checking tote size over capacity
		
		// set destination
		ActionNode setTarget = new ActionNode(SetTarget);
		ActionNode setDestination = new ActionNode(AIWorker.SetDestination);
		
		// move to destination
		ActionNode moveTo = new ActionNode(AIWorker.MoveTo);

		// actual work
		ActionNode pickItems = new ActionNode(PickItems);

		work.Add(setTarget);
		work.Add(setDestination);
		work.Add(moveTo);
		work.Add(pickItems);

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

	public override IBaseNode.NodeState UpdateTaskNode(in BTContext ctx)
	{
		// 본인의 static bt를 돌려야 한다
		return baseNode.Evaluate(ctx);
	}

	//
	public static IBaseNode.NodeState SetTarget(in BTContext ctx)
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

		return IBaseNode.NodeState.Success;
	}

	public static IBaseNode.NodeState PickItems(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;
		task.CurrentLine.Location.RemoveItem(task.CurrentLine.Quantity);

		task.PickingData.MoveToLextLine();

		return IBaseNode.NodeState.Success;
	}

	public static IBaseNode.NodeState CheckFulfilled(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;

		if (task.PickingData.IsPickingEnd())
			return IBaseNode.NodeState.Success;

		return IBaseNode.NodeState.Failure;
	}

}
