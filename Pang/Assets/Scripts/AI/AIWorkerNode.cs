using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed partial class AIWorker
{
	// AI's basic actions
	public static NodeState SetDestination(in BTContext context)
	{
		// for real
		context.LocalBlackBoard.TryGet<int3>("goalPos", out int3 goalPos);
		context.Worker.routeFinder.enabled = true;
		context.Worker.routeFinder.SetGoalPosition(goalPos);

		return Success;
	}

	public static NodeState MoveTo(in BTContext context)
	{
		if (context.Worker.routeFinder.IsGoal)
		{
			//Debug.Log("Goal Hit!");
			context.Worker.routeFinder.enabled = false;
			return Success;
		}
		context.Worker.enabled = false;

		return Running;
	}

	public static NodeState DoWork(in BTContext context)
	{
		if (context.Worker.CurrentTask == null)
			return Failure;

		return context.Worker.CurrentTask.UpdateTaskNode(context);
	}

	public static NodeState TaskCompleted(in BTContext ctx)
	{
		var task = ctx.Worker.CurrentTask;
		task.EndTask();

		Debug.Log("TaskCompleted!");

		WorkerMgr.AddIdleWorker(ctx.Worker);

		return Success;
	}

	public static NodeState TaskFailed(in BTContext ctx)
	{
		var task = ctx.Worker.CurrentTask;
		task.EndTask();

		Debug.Log("TaskFailed...");

		return Success;
	}
}