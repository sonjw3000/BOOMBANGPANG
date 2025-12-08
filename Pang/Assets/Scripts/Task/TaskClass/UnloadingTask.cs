using System;
using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;

public class UnloadingTask : WorkerTask
{
	private int3 targetZone;

	public UnloadingTask(int3 targetZone) : base(TaskType.Unloading)
	{
		this.targetZone = targetZone;
	}

	protected override void BuildTaskNode()
	{

	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[UnloadingTask] GoalPos: {targetZone}";
	}
#endif

	public override IBaseNode.NodeState UpdateTaskNode(in BTContext ctx)
	{

		return IBaseNode.NodeState.Success;
	}
}