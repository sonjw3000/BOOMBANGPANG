using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static UnityEditor.PlayerSettings;
using static UnityEditor.Progress;

public sealed class PickingTask : WorkerTask
{
	public struct PickingLine
	{
		public int3 GoalPosition;
		public int ItemID;
		public int Quantity;
	}

	public class PickJob
	{
		public int JobID;
		public int CurrentLine = 0;
		public List<PickingLine> Lines;

		public bool IsPickingEnd()
		{
			return CurrentLine >= Lines.Count;
		}

		public PickingLine GetNextLine()
		{
			return Lines[CurrentLine++];
		}
	}

	private IBaseNode baseNode;
	
	public PickJob PickingData { get; private set; }

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

		work.Add(setTarget);
		work.Add(setDestination);
		work.Add(moveTo);

		// for root
		root.Add(checkingFulfilled);
		root.Add(work);

		baseNode = root;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"Picking Task: {PickingData.CurrentLine} / {PickingData.Lines.Count}, Goal: {PickingData.Lines[PickingData.CurrentLine].GoalPosition}";
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

			//task.CurrentStatus = ;
			//task.EndTask();
			//return IBaseNode.NodeState.Failure;
		}

		// set goalPosition
		var line = task.PickingData.GetNextLine();
		ctx.LocalBlackBoard.Set<int3>("goalPos", line.GoalPosition);

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
