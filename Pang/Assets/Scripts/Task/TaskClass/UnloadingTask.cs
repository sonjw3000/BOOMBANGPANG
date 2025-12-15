using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static IBaseNode;
using static IBaseNode.NodeState;

public class UnloadingTask : WorkerTask
{
	private Rocket targetRocket;
	private CargoPort cargoPort;

	static private InboundWorkflowManager IBManager => GameContext.Instance.IBWorkflowMgr;
	static private CargoPortService PortService => GameContext.Instance.WMSys.CargoPorts;

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
		ActionNode unload = new ActionNode(UnloadFromRocket);
		ActionNode setZone = new ActionNode(SetZoneTarget);
		ActionNode puton = new ActionNode(PutOnBuffer);
		ActionNode endTask = new ActionNode(AIWorker.TaskCompleted);

		SelectorNode getBox = AIWorker.GetBox(BoxType.Cargo);
		SequenceNode moveToRocket = AIWorker.MoveToTarget(SetRocketTarget);
		SequenceNode moveToUnloadingZone = AIWorker.MoveToTarget(SetZoneTarget);

		root.Add(getBox);
		root.Add(moveToRocket);
		root.Add(unload);

		root.Add(moveToUnloadingZone);
		root.Add(puton);
		root.Add(endTask);

		baseNode = root;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[UnloadingTask] RocketPos: {targetRocket.InteractionPoints[0]}";
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

		ctx.LocalBlackBoard.Set<int3>("goalPos", task.targetRocket.InteractionPoints[0]);
		Debug.Log($"Moving to: {task.targetRocket.InteractionPoints[0]}");

		return Success;
	}

	public static NodeState UnloadFromRocket(in BTContext ctx)
	{
		UnloadingTask task = (UnloadingTask)ctx.Worker.CurrentTask;
		Rocket rocket = task.targetRocket;

		if (rocket == null)
		{
			// todo 여기서 task를 end 해야함 failled로
			Debug.Log("No rocket here!!!!!!");
			return Failure;
		}

		// items를 worker에게 건내줘야함
		AIWorker worker = ctx.Worker;

		BoxBase box = worker.GetComponent<CarryBoxAbility>().CarringBox;
		if (box == null)
		{
			Debug.Log("No Box OMG!!");
			return Failure;
		}
		
		var items = rocket.GetPayload();
		box.AddItem(items);

		// todo
		// 새로운 작업이 필요할것이다
		
		// disable rocket
		if (items.Count == 0)
			GameContext.Instance.RocketMgr.DisableRocket(task.targetRocket);
		
		Debug.Log("Unloading!!");

		return Success;
	}

	public static NodeState SetZoneTarget(in BTContext ctx)
	{
		UnloadingTask task = (UnloadingTask)ctx.Worker.CurrentTask;

		task.cargoPort = PortService.GetClosestAvailablePort(ctx.Worker.GridPosition);

		if (task.cargoPort == null)
		{
			Debug.Log("No Cargoport Available!!");
			return Failure;
		}
		
		ctx.LocalBlackBoard.Set<int3>("goalPos", task.cargoPort.GridPosition);

		Debug.Log($"Moving to: {IBManager.InboundBufferZone}");

		return Success;
	}

	public static NodeState PutOnBuffer(in BTContext ctx)
	{
		UnloadingTask task = (UnloadingTask)ctx.Worker.CurrentTask;

		if (task.cargoPort == null)
		{
			Debug.Log("No Cargoport Available!!");
			return Failure;
		}



		Debug.Log("BufferLoading!");

		return Success;
	}


}