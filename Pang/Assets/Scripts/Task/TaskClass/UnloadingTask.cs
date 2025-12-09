using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static IBaseNode;
using static IBaseNode.NodeState;

public class UnloadingTask : WorkerTask
{
	private Rocket targetRocket;

	static private InboundWorkflowManager IBManager => GameContext.Instance.IBWorkflowMgr;

	public UnloadingTask(Rocket rocket) : base(TaskType.Unloading)
	{
		targetRocket = rocket;
	}

	protected override void BuildTaskNode()
	{
		// 1. 로켓 이동
		// 2. 화물 하역
		// 3. inboundmanager의 bufferzone으로 이동
		// 4. payload를 zone에 올리기
		// 5. 완료

		SequenceNode root = new();
		ActionNode setRocketTarget = new ActionNode(SetRocketTarget);
		ActionNode moveTo = new ActionNode(AIWorker.MoveTo);
		ActionNode unload = new ActionNode(UnloadFromRocket);
		ActionNode setZone = new ActionNode(SetZoneTarget);
		ActionNode puton = new ActionNode(PutOnBuffer);
		ActionNode endTask = new ActionNode(AIWorker.TaskCompleted);
		
		root.Add(setRocketTarget);
		root.Add(new ActionNode(AIWorker.SetDestination));
		root.Add(moveTo);
		root.Add(unload);
		root.Add(setZone);
		root.Add(new ActionNode(AIWorker.SetDestination));
		root.Add(moveTo);
		root.Add(puton);
		root.Add(endTask);

		baseNode = root;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[UnloadingTask] RocketPos: {targetRocket.PickingPosition}";
	}
#endif

	public override NodeState UpdateTaskNode(in BTContext ctx)
	{
		return baseNode.Evaluate(ctx);
	}

	// 
	public static NodeState SetRocketTarget(in BTContext ctx)
	{
		UnloadingTask task = (UnloadingTask)ctx.Worker.CurrentTask;

		Debug.Log("UnloadingStart!!");

		if (task.targetRocket == null)
		{
			// todo
			// rocket이 파괴되었을수도 있다 이 때 task를 파괴한다

			return Failure;
		}

		ctx.LocalBlackBoard.Set<int3>("goalPos", task.targetRocket.PickingPosition);
		Debug.Log($"Moving to: {task.targetRocket.PickingPosition}");

		return Success;
	}

	public static NodeState UnloadFromRocket(in BTContext ctx)
	{
		UnloadingTask task = (UnloadingTask)ctx.Worker.CurrentTask;

		if (task.targetRocket == null)
		{
			// todo 여기서 task를 end 해야함 failled로
			Debug.Log("No rocket here!!!!!!");
			return Failure;
		}

		Debug.Log("Unloading!!");

		return Success;
	}

	public static NodeState SetZoneTarget(in BTContext ctx)
	{
		ctx.LocalBlackBoard.Set<int3>("goalPos", IBManager.InboundBufferZone);

		Debug.Log($"Moving to: {IBManager.InboundBufferZone}");

		return Success;
	}

	public static NodeState PutOnBuffer(in BTContext ctx)
	{
		Debug.Log("BufferLoading!");

		return Success;
	}


}