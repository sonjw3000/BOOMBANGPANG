using System;
using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;

public class UnloadingTask : WorkerTask
{
	private int3 unloadingZone;

	public UnloadingTask(int3 unloadingZone) : base(TaskType.Unloading)
	{
		this.unloadingZone = unloadingZone;
	}

	protected override void BuildTaskNode()
	{

	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[UnloadingTask] GoalPos: {unloadingZone}";
	}
#endif

	public override IBaseNode.NodeState UpdateTaskNode(in BTContext ctx)
	{

		return IBaseNode.NodeState.Success;
	}
}