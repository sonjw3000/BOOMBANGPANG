using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public sealed class PickingTask : WorkerTask
{
	public struct PickingLine
	{
		public int3 GoalPosition;
		public int ItemID;
		public int RequestedMount;
		public int ContainerID;
	}

	public class PickJob
	{
		public int JobID;
		public List<PickingLine> Lines;

		public bool IsPickingEnd()
		{
			return JobID >= Lines.Count;
		}

		public PickingLine GetNextLine()
		{
			return Lines[JobID++];
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

		// checking tote size over capacity
		// set destination
		ActionNode setTarget = new ActionNode(SetTarget);
		ActionNode setDestination = new ActionNode(AIWorker.SetDestination);
		
		// move to destination
		ActionNode moveTo = new ActionNode(AIWorker.MoveTo);

		root.Add(setTarget);
		root.Add(setDestination);
		root.Add(moveTo);

		baseNode = root;
	}

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
			return IBaseNode.NodeState.Failure;

		// set goalPosition
		var line = task.PickingData.GetNextLine();
		ctx.LocalBlackBoard.Set<int3>("goalPos", line.GoalPosition);

		return IBaseNode.NodeState.Success;
	}

}
